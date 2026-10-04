using System.Collections.Generic;
using UnityEngine;

namespace HaoxiKaiyan
{
    // An invisible active ragdoll. The procedural rig supplies muscle targets; Unity joints
    // preserve limb lengths and carry impulses between the wrist, torso and legs.
    public sealed class PuppetDynamics
    {
        private static readonly int[] Parents = { -1, 0, 1, 1, 3, 4, 1, 6, 7, 0, 9, 10, 0, 12, 13 };
        private static readonly float[] Radii = { .18f, .24f, .16f, .105f, .095f, .085f,
            .105f, .095f, .085f, .12f, .105f, .09f, .12f, .105f, .09f };
        private static readonly float[] SegmentLengths = { .53f, 0f, 0f, .48f, .44f, 0f,
            .48f, .44f, 0f, .52f, .50f, 0f, .52f, .50f, 0f };
        private readonly MarionetteController owner;
        private readonly PuppetRig rig;
        private readonly Rigidbody root;
        private readonly Transform[] visuals;
        private readonly Rigidbody[] bones;
        private readonly ConfigurableJoint[] joints;
        private readonly Collider[] colliders;
        private readonly List<Collider> allColliders;
        private readonly bool[] broken;
        private readonly Vector3[] targetPositions;
        private readonly Quaternion[] targetRotations;
        private readonly Vector3[] previousGoals, goalVelocities;
        private readonly Quaternion[] visualOffsets;
        private Vector3 visualPelvisOffset;
        private readonly Transform container;
        private readonly bool enemy;
        private readonly PhysicsMaterial lowFriction;
        private float relaxedUntil;
        private bool dead;

        public Collider[] Colliders => colliders;
        public Rigidbody Wrist => bones[5];
        public Rigidbody Chest => bones[1];
        public Rigidbody Head => bones[2];
        public float MaxJointError
        {
            get
            {
                float largest = 0f;
                for (int i = 1; i < joints.Length; i++)
                {
                    ConfigurableJoint joint = joints[i];
                    if (joint == null)
                    {
                        if (broken[i]) continue;
                        return float.PositiveInfinity;
                    }
                    if (joint.connectedBody == null) return float.PositiveInfinity;
                    Vector3 a = joint.transform.TransformPoint(joint.anchor);
                    Vector3 b = joint.connectedBody.transform.TransformPoint(joint.connectedAnchor);
                    largest = Mathf.Max(largest, Vector3.Distance(a, b));
                }
                return largest;
            }
        }

        public PuppetDynamics(MarionetteController actor, PuppetRig poseRig, Rigidbody rootBody, Collider rootCollider)
        {
            owner = actor;
            rig = poseRig;
            root = rootBody;
            enemy = actor.IsEnemy;
            visuals = rig.PhysicalNodes;
            bones = new Rigidbody[visuals.Length];
            joints = new ConfigurableJoint[visuals.Length];
            colliders = new Collider[visuals.Length];
            broken = new bool[visuals.Length];
            targetPositions = new Vector3[visuals.Length];
            targetRotations = new Quaternion[visuals.Length];
            previousGoals = new Vector3[visuals.Length];
            goalVelocities = new Vector3[visuals.Length];
            visualOffsets = new Quaternion[visuals.Length];
            container = new GameObject(actor.name + " physical joints").transform;
            lowFriction = new PhysicsMaterial("Joint contact without foot drag")
            {
                dynamicFriction = .08f,
                staticFriction = .08f,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };
            allColliders = new List<Collider>(bones.Length * 3);
            for (int i = 0; i < bones.Length; i++)
            {
                GameObject node = new GameObject(visuals[i].name + " mass");
                node.layer = MarionetteController.ActorLayer;
                node.transform.SetParent(container, false);
                node.transform.SetPositionAndRotation(visuals[i].position, visuals[i].rotation);
                Rigidbody bone = node.AddComponent<Rigidbody>();
                bone.mass = i == 0 ? .48f : i == 1 ? .48f : i == 2 ? .19f : .11f;
                bone.linearDamping = .55f;
                bone.angularDamping = 1.2f;
                bone.maxAngularVelocity = 18f;
                bone.solverIterations = 12;
                bone.solverVelocityIterations = 4;
                bone.maxDepenetrationVelocity = 3f;
                bone.interpolation = RigidbodyInterpolation.Interpolate;
                bone.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                SphereCollider shape = node.AddComponent<SphereCollider>();
                shape.radius = Radii[i];
                bones[i] = bone;
                colliders[i] = shape;
                Register(shape);
                CapsuleCollider shaft = null;
                if (SegmentLengths[i] > 0f)
                {
                    shaft = node.AddComponent<CapsuleCollider>();
                    shaft.direction = 1;
                    shaft.radius = Radii[i] * .88f;
                    shaft.height = SegmentLengths[i] + shaft.radius * 2f;
                    shaft.center = Vector3.up * (SegmentLengths[i] * .5f);
                    Register(shaft);
                }
                SphereCollider neck = null;
                if (i == 2)
                {
                    neck = node.AddComponent<SphereCollider>();
                    neck.center = Vector3.down * .19f;
                    neck.radius = .105f;
                    Register(neck);
                }
                PuppetBodyPart part = node.AddComponent<PuppetBodyPart>();
                part.Configure(owner, bone, i, shape, shaft, neck);
            }

            void Register(Collider shape)
            {
                shape.material = lowFriction;
                Physics.IgnoreCollision(shape, rootCollider, true);
                if (actor.WeaponCollider != null) Physics.IgnoreCollision(shape, actor.WeaponCollider, true);
                foreach (Collider other in allColliders) Physics.IgnoreCollision(shape, other, true);
                allColliders.Add(shape);
            }
            for (int i = 0; i < bones.Length; i++)
            {
                Rigidbody parent = Parents[i] < 0 ? root : bones[Parents[i]];
                ConfigurableJoint joint = bones[i].gameObject.AddComponent<ConfigurableJoint>();
                joint.connectedBody = parent;
                joint.autoConfigureConnectedAnchor = false;
                joint.anchor = Vector3.zero;
                joint.connectedAnchor = parent.transform.InverseTransformPoint(bones[i].position);
                ConfigureJoint(joint, i);
                joints[i] = joint;
            }
            CaptureTargets();
            ResetFilters();
        }

        private static void ConfigureJoint(ConfigurableJoint joint, int index)
        {
            // Puppet ball joints follow bounded procedural poses. An arbitrary
            // initial-frame +/-95 degree limit fought full turns and flipped IK
            // frames. Keep bone lengths constrained without fighting those poses.
            joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Free;
            float give = index == 0 ? .13f : index == 1 ? .05f : index == 3 || index == 6 ? .035f : 0f;
            joint.xMotion = joint.yMotion = joint.zMotion = give > 0f ?
                ConfigurableJointMotion.Limited : ConfigurableJointMotion.Locked;
            if (give > 0f) joint.linearLimit = new SoftJointLimit { limit = give };
            joint.projectionMode = JointProjectionMode.PositionAndRotation;
            joint.projectionDistance = .035f;
            joint.projectionAngle = 12f;
            joint.enableCollision = false;
            joint.enablePreprocessing = false;
        }

        private void ResetFilters()
        {
            visualPelvisOffset = Vector3.zero;
            for (int i = 0; i < bones.Length; i++)
            {
                previousGoals[i] = root.transform.TransformPoint(targetPositions[i]);
                goalVelocities[i] = Vector3.zero;
                visualOffsets[i] = Quaternion.identity;
            }
        }

        public void CaptureTargets()
        {
            for (int i = 0; i < visuals.Length; i++)
            {
                targetPositions[i] = root.transform.InverseTransformPoint(visuals[i].position);
                targetRotations[i] = Quaternion.Inverse(root.rotation) * visuals[i].rotation;
            }
        }

        public void Simulate(float dt)
        {
            if (dead) return;
            float relax = Time.time < relaxedUntil ? .38f : 1f;
            float strength = (enemy ? .76f : 1f) * relax;
            for (int i = 0; i < bones.Length; i++)
            {
                if (Detached(i)) continue;
                Rigidbody bone = bones[i];
                Vector3 goal = root.transform.TransformPoint(targetPositions[i]);
                // Feed forward target motion, not the noisy velocity of a parent
                // mass. The latter propagated one joint's oscillation down every limb.
                Vector3 measured = Vector3.ClampMagnitude((goal - previousGoals[i]) / Mathf.Max(.001f, dt), 12f);
                previousGoals[i] = goal;
                goalVelocities[i] = Vector3.Lerp(goalVelocities[i], measured, .45f);
                bool leg = i >= 9;
                Vector3 acceleration = (goal - bone.position) * (leg ? 180f : 150f) -
                    (bone.linearVelocity - goalVelocities[i]) * (leg ? 27f : 24f);
                bone.AddForce(-Physics.gravity * relax + Vector3.ClampMagnitude(acceleration * strength,
                    leg ? 120f : 100f), ForceMode.Acceleration);

                Quaternion goalRotation = root.rotation * targetRotations[i];
                Quaternion difference = goalRotation * Quaternion.Inverse(bone.rotation);
                difference.ToAngleAxis(out float degrees, out Vector3 axis);
                if (degrees > 180f) degrees -= 360f;
                if (axis.sqrMagnitude > .001f && !float.IsNaN(axis.x))
                {
                    Vector3 torque = axis * (degrees * Mathf.Deg2Rad * 95f) - bone.angularVelocity * 18f;
                    bone.AddTorque(Vector3.ClampMagnitude(torque * strength, 110f), ForceMode.Acceleration);
                }
            }
        }

        public void ApplyVisualPose(Vector3 grip, Quaternion bladeRotation)
        {
            float dt = Mathf.Min(Time.deltaTime, .05f);
            bool reacting = Time.time < relaxedUntil;
            float response = 1f - Mathf.Exp(-(reacting ? 18f : 11f) * dt);
            Vector3 pelvisOffset = bones[0].position - root.transform.TransformPoint(targetPositions[0]);
            visualPelvisOffset = Vector3.Lerp(visualPelvisOffset,
                Vector3.ClampMagnitude(pelvisOffset, reacting ? .12f : .035f), response);
            for (int i = 0; i < visuals.Length; i++)
            {
                if (dead || Detached(i))
                {
                    visuals[i].SetPositionAndRotation(bones[i].position, bones[i].rotation);
                    continue;
                }
                if (i > 2) continue; // Intact limbs are solved from this supported trunk below.
                Quaternion target = root.rotation * targetRotations[i];
                Quaternion offset = bones[i].rotation * Quaternion.Inverse(target);
                offset = Quaternion.RotateTowards(Quaternion.identity, offset, reacting ? 35f : 9f);
                visualOffsets[i] = Quaternion.Slerp(visualOffsets[i], offset, response);
                // Only the root receives a positional offset. Children retain their
                // authored local anchors, preserving torso and neck connections.
                if (i == 0) visuals[i].position = root.transform.TransformPoint(targetPositions[i]) + visualPelvisOffset;
                visuals[i].rotation = Quaternion.Slerp(Quaternion.identity, visualOffsets[i], reacting ? .8f : .30f) * visuals[i].rotation;
            }
            if (!dead) rig.ResolvePhysicalContacts(grip, bladeRotation,
                !Detached(8), !Detached(5), !Detached(11), !Detached(14));
            rig.RefreshThreads(grip, bladeRotation, Time.deltaTime);
        }

        public void Hit(PuppetBodyPart part, HitImpact impact)
        {
            if (dead) return;
            relaxedUntil = Mathf.Max(relaxedUntil, Time.time + impact.stun * .70f);
            Rigidbody hitBone = part != null && part.Owner == owner ? part.Body : Chest;
            Vector3 direction = impact.direction.normalized;
            Vector3 impulse = direction * Mathf.Max(.1f, impact.push) * .23f + Vector3.up * impact.lift * .12f;
            hitBone.AddForceAtPosition(impulse, impact.point, ForceMode.Impulse);
        }

        public void ReactToWeaponContact(Vector3 direction, Vector3 point, float strength)
        {
            if (dead || broken[5]) return;
            Wrist.AddForceAtPosition(direction * strength, point, ForceMode.Impulse);
            bones[4].AddForce(direction * strength * .27f, ForceMode.Impulse);
            Chest.AddForce(direction * strength * .08f, ForceMode.Impulse);
        }

        public void Die()
        {
            if (dead) return;
            dead = true;
            // Release the walking root, not the skeleton: all limb-to-limb joints stay intact.
            Object.Destroy(joints[0]);
            joints[0] = null;
            broken[0] = true;
            root.isKinematic = true;
            root.GetComponent<Collider>().enabled = false;
        }

        public void Reset()
        {
            dead = false;
            relaxedUntil = 0f;
            root.isKinematic = false;
            root.GetComponent<Collider>().enabled = true;
            CaptureTargets();
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].position = visuals[i].position;
                bones[i].rotation = visuals[i].rotation;
                bones[i].linearVelocity = Vector3.zero;
                bones[i].angularVelocity = Vector3.zero;
            }
            for (int i = 0; i < joints.Length; i++)
            {
                if (joints[i] != null) { broken[i] = false; continue; }
                Rigidbody parent = Parents[i] < 0 ? root : bones[Parents[i]];
                var joint = bones[i].gameObject.AddComponent<ConfigurableJoint>();
                joint.connectedBody = parent;
                joint.autoConfigureConnectedAnchor = false;
                joint.connectedAnchor = parent.transform.InverseTransformPoint(bones[i].position);
                ConfigureJoint(joint, i);
                joints[i] = joint;
                broken[i] = false;
            }
            ResetFilters();
        }

        public bool BreakJoint(int index)
        {
            if (index <= 0 || index >= joints.Length || broken[index] || joints[index] == null)
                return false;
            Object.Destroy(joints[index]);
            joints[index] = null;
            broken[index] = true;
            return true;
        }

        public bool IsKneeBroken => broken[10] || broken[13];
        public bool IsWristBroken => broken[5];

        public void IgnoreOwnWeapon(Collider shape)
        {
            if (shape == null) return;
            foreach (Collider collider in allColliders) Physics.IgnoreCollision(shape, collider, true);
        }

        private bool Detached(int index)
        {
            while (index >= 0)
            {
                if (broken[index]) return true;
                index = Parents[index];
            }
            return false;
        }

        public void Dispose()
        {
            if (container != null) Object.Destroy(container.gameObject);
            if (lowFriction != null) Object.Destroy(lowFriction);
        }
    }

}
