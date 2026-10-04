using UnityEngine;

namespace HaoxiKaiyan
{
    public sealed partial class MarionetteController
    {
        private void AttachWeaponJoint()
        {
            weaponJoint = weapon.gameObject.AddComponent<ConfigurableJoint>();
            weaponJoint.connectedBody = dynamics.Wrist;
            weaponJoint.autoConfigureConnectedAnchor = false;
            weaponJoint.anchor = Vector3.zero;
            weaponJoint.connectedAnchor = Vector3.zero;
            weaponJoint.xMotion = weaponJoint.yMotion = weaponJoint.zMotion = ConfigurableJointMotion.Limited;
            weaponJoint.linearLimit = new SoftJointLimit { limit = .10f };
            weaponJoint.angularXMotion = weaponJoint.angularYMotion = weaponJoint.angularZMotion = ConfigurableJointMotion.Free;
            weaponJoint.projectionMode = JointProjectionMode.PositionAndRotation;
            weaponJoint.projectionDistance = .08f;
        }

        public WeaponPickup DropWeapon()
        {
            if (weapon == null) return null;
            if (weaponJoint != null) Destroy(weaponJoint);
            if (weaponCollider != null) weaponCollider.isTrigger = false;
            MeleeWeaponHitbox hitbox = weapon.GetComponent<MeleeWeaponHitbox>();
            if (hitbox != null) hitbox.enabled = false;
            if (bladeTrail != null) bladeTrail.emitting = false;
            WeaponPickup pickup = weapon.gameObject.AddComponent<WeaponPickup>();
            pickup.Configure(weaponSpec.kind);
            weapon = null; weaponBody = null; weaponCollider = null; weaponJoint = null; bladeTrail = null;
            WeaponSwingSpeed = 0f;
            return pickup;
        }

        public void HandleWeaponDoubleTap()
        {
            if (IsEnemy || IsDefeated) return;
            WeaponPickup closest = null;
            float nearest = 1.38f;
            foreach (WeaponPickup pickup in FindObjectsByType<WeaponPickup>())
            {
                if (pickup == null || Time.time < pickup.ReadyAt) continue;
                float distance = Vector3.ProjectOnPlane(pickup.transform.position - body.position, Vector3.up).magnitude;
                if (distance >= nearest) continue;
                nearest = distance; closest = pickup;
            }
            if (closest == null) { DropWeapon(); return; }
            WeaponKind next = closest.Kind;
            DropWeapon();
            Destroy(closest.gameObject);
            weaponSpec = WeaponSpec.Get(next);
            BuildWeapon(weaponDark, weaponBrass, weaponBlade);
            dynamics.IgnoreOwnWeapon(weaponCollider);
            AttachWeaponJoint();
            previousTip = weaponBody.position + weaponBody.rotation * Vector3.forward * WeaponLength;
            previousRoot = body.position;
        }

        // Damped orbit and rigidbody force/torque; never a canned attack animation.
        private void SimulateWeapon(float dt)
        {
            if (IsDefeated) { WeaponSwingSpeed = 0f; return; }
            if (weapon == null)
            {
                dynamics.Simulate(dt);
                WeaponSwingSpeed = 0f;
                return;
            }
            // One consistent target height. Stick magnitude gates active pull;
            // above the threshold pull ramps to 1, not an analogue height/force selection.
            pull = Mathf.MoveTowards(pull, IsEnemy ? .80f : weaponInput.magnitude > GameplayTuning.Weapon.IntentThreshold ? 1f : 0f, dt * GameplayTuning.Weapon.PullResponse);
            float requestedYaw = yawTarget + enemyWindup * 48f - enemyStrike * 48f;
            float stiffness = Mathf.Lerp(GameplayTuning.Weapon.IdleOrbitStiffness, GameplayTuning.Weapon.ActiveOrbitStiffness * weaponSpec.angularDrive / GameplayTuning.Weapon.OrbitDriveReference * StrengthMultiplier, pull);
            float angularDamping = Mathf.Lerp(GameplayTuning.Weapon.LightOrbitDamping, GameplayTuning.Weapon.HeavyOrbitDamping, Mathf.InverseLerp(.3f, 1.7f, weaponSpec.mass));
            float maxOrbit = Mathf.Min(GameplayTuning.Weapon.MaxOrbitSpeed, weaponSpec.maxAngularSpeed * Mathf.Rad2Deg * GameplayTuning.Weapon.OrbitSpeedRatio);
            orbitRate = Mathf.Clamp(orbitRate + (Mathf.DeltaAngle(orbitYaw, requestedYaw) * stiffness - orbitRate * angularDamping) * dt, -maxOrbit, maxOrbit);
            orbitYaw = Mathf.Repeat(orbitYaw + orbitRate * dt + 180f, 360f) - 180f;
            int sign = Mathf.Abs(orbitRate) > GameplayTuning.Weapon.StrokeDirectionThreshold ? (int)Mathf.Sign(orbitRate) : 0;
            if (sign != 0 && sign != swingSign) { swingSign = sign; SwingSequence++; }

            Vector3 desiredGrip = ReachableWeaponGoal();
            Vector3 relativeVelocity = weaponBody.linearVelocity -
                (body.isKinematic ? motorVelocity : body.linearVelocity);
            // A released prop is loosely tethered, so walking pulls it along with a visible lag.
            // Deliberate stick input tightens the string and transfers force to the wrist/torso.
            float tether = Mathf.Lerp(GameplayTuning.Weapon.IdleTetherStiffness, weaponSpec.drive * StrengthMultiplier, IsEnemy ? .72f : pull);
            float damping = Mathf.Lerp(GameplayTuning.Weapon.IdleTetherDamping, GameplayTuning.Weapon.ActiveTetherDamping, IsEnemy ? .80f : pull);
            Vector3 acceleration = (desiredGrip - weaponBody.position) * tether - relativeVelocity * damping;
            weaponBody.AddForce(Vector3.ClampMagnitude(acceleration, GameplayTuning.Weapon.MaxTetherAcceleration * StrengthMultiplier), ForceMode.Acceleration);
            Quaternion desiredRotation = DesiredWeaponRotation();
            Quaternion delta = desiredRotation * Quaternion.Inverse(weaponBody.rotation);
            delta.ToAngleAxis(out float degrees, out Vector3 axis);
            if (degrees > 180f) degrees -= 360f;
            if (axis.sqrMagnitude > .001f && !float.IsNaN(axis.x))
                weaponBody.AddTorque(Vector3.ClampMagnitude(axis * (degrees * Mathf.Deg2Rad * weaponSpec.angularDrive * StrengthMultiplier) -
                    weaponBody.angularVelocity * (18f + weaponSpec.damping * 25f), GameplayTuning.Weapon.MaxTorqueAcceleration * StrengthMultiplier), ForceMode.Acceleration);

            dynamics?.Simulate(dt);

            Vector3 tip = weaponBody.position + weaponBody.rotation * Vector3.forward * WeaponLength;
            WeaponSwingSpeed = ((tip - previousTip) - (body.position - previousRoot)).magnitude / dt;
            previousTip = tip; previousRoot = body.position;
        }

        private Vector3 DesiredWeaponPosition()
        {
            Vector3 direction = Quaternion.Euler(0, orbitYaw, 0) * Vector3.forward;
            return body.position + direction * Mathf.Lerp(.54f, .72f, pull) + Vector3.up * DefaultGripHeight;
        }

        private Vector3 ReachableWeaponGoal()
        {
            Vector3 desired = DesiredWeaponPosition();
            Vector3 shoulder = rig.Chest.TransformPoint(new Vector3(.30f, .12f, 0));
            return shoulder + Vector3.ClampMagnitude(desired - shoulder, GameplayTuning.Weapon.ArmReach);
        }

        private Quaternion DesiredWeaponRotation()
        {
            float pitch = IsEnemy ? 18f - enemyWindup * 24f :
                12f;
            return Quaternion.Euler(pitch, orbitYaw, 0);
        }

        private void BuildWeapon(Material dark, Material brass, Material blade)
        {
            weapon = new GameObject(weaponSpec.kind.ToString() + (IsEnemy ? " opponent prop" : " pulled prop")).transform;
            weapon.SetPositionAndRotation(DesiredWeaponPosition(), DesiredWeaponRotation());
            weaponBody = weapon.gameObject.AddComponent<Rigidbody>();
            weaponBody.mass = weaponSpec.mass; weaponBody.useGravity = true;
            weaponBody.linearDamping = weaponSpec.damping; weaponBody.angularDamping = weaponSpec.damping;
            weaponBody.maxAngularVelocity = weaponSpec.maxAngularSpeed;
            weaponBody.interpolation = RigidbodyInterpolation.Interpolate;
            weaponBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            BoxCollider shape = weapon.gameObject.AddComponent<BoxCollider>();
            shape.center = new Vector3(0, 0, (BladeStart + WeaponLength) * .5f);
            shape.size = new Vector3(WeaponContactRadius * 1.2f, WeaponContactRadius * 1.2f, WeaponLength - BladeStart);
            // Swept overlap in MeleeWeaponHitbox supplies the actual hit and impulse.
            // A solid held blade wedged against the opponent's many joint colliders
            // and then snapped back under the wrist joint on mobile frame spikes.
            shape.isTrigger = true; weaponCollider = shape;
            Physics.IgnoreCollision(shape, bodyCollider, true);
            Part("Grip", PrimitiveType.Cylinder, weapon, new Vector3(0, 0, .03f), new Vector3(.075f, .19f, .075f), dark).localRotation = Quaternion.Euler(90, 0, 0);
            Part("Guard", PrimitiveType.Cube, weapon, new Vector3(0, 0, .22f), new Vector3(.34f, .07f, .07f), brass);
            float shaftLength = WeaponLength - .27f;
            Part(weaponSpec.weaponClass == WeaponClass.Blunt ? "Wooden shaft" : "Wooden blade", PrimitiveType.Cube,
                weapon, new Vector3(0, 0, .24f + shaftLength * .5f),
                new Vector3(weaponSpec.weaponClass == WeaponClass.Blunt ? .105f : .09f, .12f, shaftLength),
                weaponSpec.weaponClass == WeaponClass.Blunt ? dark : blade);
            if (weaponSpec.weaponClass == WeaponClass.Edged)
                Part("Blade edge", PrimitiveType.Cube, weapon, new Vector3(.05f, .02f, .24f + shaftLength * .5f),
                    new Vector3(.025f, .07f, shaftLength), brass);
            if (weaponSpec.kind == WeaponKind.WarHammer || weaponSpec.kind == WeaponKind.SawAxe)
                Part("Weighted head", PrimitiveType.Cube, weapon, new Vector3(0, 0, WeaponLength - .18f),
                    new Vector3(weaponSpec.kind == WeaponKind.WarHammer ? .47f : .36f, .29f, .27f), brass);
            else if (weaponSpec.kind == WeaponKind.BaseballBat || weaponSpec.kind == WeaponKind.Wrench)
                Part("Blunt end", PrimitiveType.Cylinder, weapon, new Vector3(0, 0, WeaponLength - .18f),
                    new Vector3(.13f, .22f, .13f), brass).localRotation = Quaternion.Euler(90, 0, 0);
            weapon.gameObject.AddComponent<MeleeWeaponHitbox>().Configure(this);
            if (!IsEnemy)
            {
                Transform tip = new GameObject("Blade arc").transform;
                tip.SetParent(weapon, false); tip.localPosition = Vector3.forward * WeaponLength;
                bladeTrail = tip.gameObject.AddComponent<TrailRenderer>();
                bladeTrail.sharedMaterial = Resources.Load<Material>("HaoxiRuntimeTrail") ?? blade;
                bladeTrail.time = .15f; bladeTrail.minVertexDistance = .025f; bladeTrail.numCapVertices = 2;
                bladeTrail.startColor = new Color(.7f, .26f, .1f, .58f);
                bladeTrail.endColor = new Color(.7f, .26f, .1f, 0f);
                bladeTrail.startWidth = .11f; bladeTrail.endWidth = 0f; bladeTrail.emitting = false;
            }
        }

        private static Transform Part(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name; part.transform.SetParent(parent, false);
            part.transform.localPosition = position; part.transform.localScale = scale;
            foreach (Collider c in part.GetComponents<Collider>()) { c.enabled = false; Destroy(c); }
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }

        private static Material Mat(Color color, float metallic = 0f)
        {
            Material source = Resources.Load<Material>("HaoxiRuntimeLit");
            Material material = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color; material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", .3f);
            return material;
        }

    }
}
