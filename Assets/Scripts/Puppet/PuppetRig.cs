using UnityEngine;

namespace HaoxiKaiyan
{
    // A small articulated rig for the prototype geometry. Each visible segment belongs to a
    // parent joint; the wrist follows the real weapon, then the torso and planted feet catch up.
    public sealed partial class PuppetRig
    {
        private readonly Transform root, pelvis, waist, chest, neck, head;
        private readonly Limb leftArm, rightArm, leftLeg, rightLeg;
        private readonly Transform bodyBar, weaponBar;
        private readonly LineRenderer[] threads;
        private readonly Renderer warning;
        private readonly bool enemy;
        private readonly Foot[] feet = { new Foot(), new Foot() };
        private float chestYaw, pelvisYaw, chestRate, pelvisRate, breath, reaction, nextStep;
        private float spinBlend, spinHold;
        private float runPhase;
        private float locomotionBlend, supportBlend;
        private bool runningLastFrame;
        private int pivotFoot;
        private Vector3 freeHandPosition, freeHandVelocity;
        private bool freeHandReady;
        private int nextFoot;
        private Transform[] physicalNodes;
        private Vector3[] targetLocalPositions;
        private Quaternion[] targetLocalRotations;

        public Transform Chest => chest;
        public Transform Pelvis => pelvis;
        public Transform Wrist => rightArm.Tip;
        public Transform LeftFoot => leftLeg.Tip;
        public Transform RightFoot => rightLeg.Tip;
        public Vector3 LeftFootGoal => feet[0].Position;
        public Vector3 RightFootGoal => feet[1].Position;
        public bool LeftFootStepping => feet[0].Stepping;
        public bool RightFootStepping => feet[1].Stepping;
        public int StepCount { get; private set; }
        public float ChestYaw => chestYaw;
        public float PelvisYaw => pelvisYaw;
        // Parent-first order matches PuppetDynamics. The positions remain procedural muscle
        // targets; the visible joints are overlaid with the simulated pose after each frame.
        public Transform[] PhysicalNodes => physicalNodes ??= new[] { pelvis, chest, head,
            rightArm.Root, rightArm.Mid, rightArm.Tip, leftArm.Root, leftArm.Mid, leftArm.Tip,
            leftLeg.Root, leftLeg.Mid, leftLeg.Tip, rightLeg.Root, rightLeg.Mid, rightLeg.Tip };

        public PuppetRig(Transform parent, bool isEnemy, Quaternion facing, Material wood,
            Material dark, Material brass, Material cloth, Material lineMaterial)
        {
            root = parent;
            enemy = isEnemy;
            chestYaw = pelvisYaw = facing.eulerAngles.y;
            pelvis = Joint("Pelvis pivot", parent, new Vector3(0, 1.10f, 0));
            Part("Carved hips", PrimitiveType.Sphere, pelvis, Vector3.zero, new Vector3(.40f, .22f, .31f), dark);
            waist = Joint("Flexible waist", pelvis, new Vector3(0, .19f, 0));
            Part("Waist pin", PrimitiveType.Sphere, waist, Vector3.zero, Vector3.one * .18f, brass);
            Part("Lower torso", PrimitiveType.Capsule, waist, new Vector3(0, .12f, 0), new Vector3(.29f, .20f, .23f), wood);
            chest = Joint("Chest pivot", waist, new Vector3(0, .34f, 0));
            Part("Ribcage", PrimitiveType.Capsule, chest, new Vector3(0, -.04f, 0), new Vector3(.37f, .23f, .25f), cloth);
            Part("Breastplate", PrimitiveType.Sphere, chest, new Vector3(0, -.025f, .19f), new Vector3(.23f, .19f, .055f), brass);
            neck = Joint("Neck pivot", chest, new Vector3(0, .23f, 0));
            Part("Neck joint", PrimitiveType.Cylinder, neck, Vector3.zero, new Vector3(.09f, .09f, .09f), dark);
            head = Joint("Head pivot", neck, new Vector3(0, .20f, 0));
            Part("Wooden head", PrimitiveType.Sphere, head, Vector3.zero, new Vector3(.26f, .33f, .24f), wood);
            Part("Pale mask", PrimitiveType.Sphere, head, new Vector3(0, 0, .19f), new Vector3(.20f, .25f, .07f), cloth);
            Part("Left eye", PrimitiveType.Sphere, head, new Vector3(-.068f, .025f, .25f), new Vector3(.026f, .032f, .018f), dark);
            Part("Right eye", PrimitiveType.Sphere, head, new Vector3(.068f, .025f, .25f), new Vector3(.026f, .032f, .018f), dark);
            Part("Head crest", PrimitiveType.Sphere, head, new Vector3(0, .18f, 0), new Vector3(.22f, .07f, .22f), dark);

            leftArm = new Limb("Free arm", chest, new Vector3(-.30f, .12f, 0), .48f, .44f, .10f, wood, dark, brass, false);
            rightArm = new Limb("Sword arm", chest, new Vector3(.30f, .12f, 0), .48f, .44f, .10f, wood, dark, brass, false);
            // Extra leg reach leaves room for visible knee bend and a planted foot during a fast step.
            leftLeg = new Limb("Left leg", pelvis, new Vector3(-.16f, -.035f, 0), .52f, .50f, .105f, wood, dark, brass, true);
            rightLeg = new Limb("Right leg", pelvis, new Vector3(.16f, -.035f, 0), .52f, .50f, .105f, wood, dark, brass, true);
            for (int i = 0; i < 2; i++) { feet[i].Position = RestFoot(i); feet[i].Yaw = pelvisYaw; }

            if (enemy)
            {
                Material red = NewMaterial(new Color(.83f, .09f, .035f));
                warning = Part("Enemy windup lantern", PrimitiveType.Sphere, head, new Vector3(0, .35f, 0), Vector3.one * .13f, red).GetComponent<Renderer>();
                warning.enabled = false;
                bodyBar = weaponBar = null;
                threads = null;
            }
            else
            {
                warning = null;
                bodyBar = Joint("Body control bar", root, new Vector3(0, 2.86f, 0));
                weaponBar = Joint("Weapon control bar", root, new Vector3(.65f, 2.76f, 0));
                Part("Body crossbar", PrimitiveType.Cube, bodyBar, Vector3.zero, new Vector3(.91f, .055f, .065f), wood);
                Part("Body crosspiece", PrimitiveType.Cube, bodyBar, Vector3.zero, new Vector3(.055f, .055f, .31f), dark);
                Part("Weapon crossbar", PrimitiveType.Cube, weaponBar, Vector3.zero, new Vector3(.55f, .05f, .06f), wood);
                threads = new LineRenderer[6];
                for (int i = 0; i < threads.Length; i++)
                {
                    Transform t = Joint("Visible thread " + i, root, Vector3.zero);
                    LineRenderer line = t.gameObject.AddComponent<LineRenderer>();
                    line.positionCount = 2; line.useWorldSpace = true; line.sharedMaterial = lineMaterial;
                    line.startWidth = line.endWidth = .006f; threads[i] = line;
                }
            }
        }

        public void SetWarning(bool on) { if (warning != null) warning.enabled = on; }
        public void React(float intensity) { reaction = Mathf.Max(reaction, intensity); }

        public void ResetPose(Vector3 position, Quaternion facing, Vector3 grip)
        {
            RestoreTargets();
            chestYaw = pelvisYaw = facing.eulerAngles.y;
            chestRate = pelvisRate = breath = reaction = 0f;
            spinBlend = spinHold = runPhase = locomotionBlend = supportBlend = 0f; runningLastFrame = false; pivotFoot = 0;
            freeHandVelocity = Vector3.zero; freeHandReady = false;
            nextFoot = 0; nextStep = Time.time + .04f; StepCount = 0;
            Quaternion direction = Quaternion.Euler(0, pelvisYaw, 0);
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                feet[i].Position = position + direction * new Vector3(side * .18f, .09f, i == 0 ? -.045f : .20f);
                feet[i].Yaw = pelvisYaw;
                feet[i].Stepping = false;
            }
            if (bodyBar != null) bodyBar.position = position + Vector3.up * 2.86f;
            if (weaponBar != null) weaponBar.position = grip + Vector3.up * 1.38f;
        }

        public void Pose(Vector3 grip, Quaternion bladeRotation, Vector3 velocity, bool windingUp,
            float collapse, float dt, bool weaponDriven = false)
        {
            RestoreTargets();
            dt = Mathf.Min(dt, GameplayTuning.Pose.MaxDeltaTime);
            Vector3 fromRoot = grip - root.position;
            float gripYaw = Mathf.Atan2(fromRoot.x, fromRoot.z) * Mathf.Rad2Deg;
            // Chest and pelvis have different inertia. The weapon leads both of them.
            Spring(ref chestYaw, ref chestRate, gripYaw - 13f, GameplayTuning.Pose.ChestStiffness, GameplayTuning.Pose.ChestDamping, GameplayTuning.Pose.ChestMaxSpeed, dt);
            Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
            float travelYaw = Mathf.Atan2(planarVelocity.x, planarVelocity.z) * Mathf.Rad2Deg;
            // In ordinary travel the lower body faces its path; during a weapon-led
            // turn it yields to the blade so the two controls remain independent.
            float travelWeight = Mathf.Clamp01(planarVelocity.magnitude / 3.2f) *
                (weaponDriven ? .24f : .68f);
            Spring(ref pelvisYaw, ref pelvisRate,
                Mathf.LerpAngle(chestYaw, travelYaw, travelWeight), GameplayTuning.Pose.PelvisStiffness, GameplayTuning.Pose.PelvisDamping, GameplayTuning.Pose.PelvisMaxSpeed, dt);
            // A navigation turn should be walked through. Reserve the planted
            // pirouette for a deliberate weapon pull, not every change of travel.
            if (weaponDriven && (Mathf.Abs(chestRate) > 105f || Mathf.Abs(pelvisRate) > 95f))
                spinHold = Time.time + .20f;
            float oldSpin = spinBlend;
            spinBlend = Mathf.MoveTowards(spinBlend, Time.time < spinHold ? 1f : 0f, dt * 5f);
            // The first free foot must be the foot scheduled to step. Making it the
            // pivot deadlocked the old scheduler until the legs stretched far apart.
            if (oldSpin < .25f && spinBlend >= .25f) pivotFoot = 1 - nextFoot;
            float twist = Mathf.Clamp(Mathf.DeltaAngle(pelvisYaw, chestYaw), -55f, 55f);
            float turn = Mathf.Clamp(chestRate / 280f, -1f, 1f);
            float moving = Mathf.Clamp01(new Vector2(velocity.x, velocity.z).magnitude / 3.4f);
            float runBlend = Mathf.Clamp01((planarVelocity.magnitude - 1.3f) / 2f);
            locomotionBlend = Mathf.MoveTowards(locomotionBlend, runBlend, dt * 4f);
            float supportSide = !feet[0].Stepping && feet[1].Stepping ? -1f :
                feet[0].Stepping && !feet[1].Stepping ? 1f : 0f;
            supportBlend = Mathf.Lerp(supportBlend, supportSide, 1f - Mathf.Exp(-8f * dt));
            Vector3 travelLocal = Quaternion.Inverse(Quaternion.Euler(0, pelvisYaw, 0)) * planarVelocity;
            float travelLean = Mathf.Clamp(travelLocal.z * .85f, -3.5f, 4.5f) * runBlend;
            float balanceRoll = (-Mathf.Clamp(travelLocal.x * .75f, -3.5f, 3.5f) + supportBlend * 2f) * locomotionBlend;
            breath += dt * (2.2f + moving * 4f + Mathf.Abs(turn) * 1.5f);
            reaction = Mathf.MoveTowards(reaction, 0, dt * 4f);
            float sway = Mathf.Sin(breath * .47f);
            float runFlight = (1f - Mathf.Cos(runPhase * Mathf.PI * 4f)) * .018f * locomotionBlend;
            // A long support leg at rest, a small plié during travel, and a raised
            // center during a turn. No binary running crouch or symmetric squat.
            pelvis.localPosition = new Vector3(-.038f * (1f - locomotionBlend) - turn * .028f + sway * .012f +
                supportBlend * .025f * locomotionBlend, 1.10f - locomotionBlend * .072f + spinBlend * .022f +
                Mathf.Sin(breath) * .009f + runFlight - reaction * .025f - collapse * .73f, 0);
            pelvis.localRotation = Quaternion.Euler(collapse * 78f + travelLean * .35f, pelvisYaw,
                -turn * 5f + collapse * 9f + sway * 1.5f + balanceRoll * .55f);
            waist.localRotation = Quaternion.Euler(-moving * 5f + reaction * 9f + travelLean * .55f,
                twist * .35f, -turn * 6f - sway * 2f - balanceRoll * .35f);
            chest.localRotation = Quaternion.Euler((windingUp ? -14f : 0) - turn * 5f + travelLean * .28f,
                twist * .65f, -turn * 10f + sway * 2.5f - balanceRoll * .65f);
            neck.localRotation = Quaternion.Euler(4f + Mathf.Sin(breath) * 2f, Mathf.DeltaAngle(chestYaw, gripYaw) * .23f, turn * 8f);
            head.localRotation = Quaternion.Euler((enemy ? 8f : 0) + collapse * 22f, 0, -turn * 4f);

            // The shoulder shifts toward a stretched wrist before the ribcage completes the turn.
            Vector3 shoulderToGrip = grip - rightArm.Root.position;
            rightArm.Root.localPosition = new Vector3(.30f, .12f, 0) + chest.InverseTransformDirection(shoulderToGrip.normalized) *
                Mathf.Clamp(shoulderToGrip.magnitude - .73f, 0, .07f);
            rightArm.Solve(grip, chest.right - chest.forward * .4f - Vector3.up * .5f, bladeRotation);
            // The unoccupied arm hangs from its shoulder and follows the body's momentum late.
            Vector3 freeTarget = leftArm.Root.position - chest.right * (.16f + spinBlend * .30f + reaction * .12f) - Vector3.up * (.76f - spinBlend * .24f - reaction * .18f) +
                chest.forward * (.06f - turn * .24f) + chest.right * (sway * .035f - turn * .09f) -
                Vector3.ClampMagnitude(Vector3.ProjectOnPlane(velocity, Vector3.up) * .045f, .18f);
            if (!freeHandReady) { freeHandPosition = freeTarget; freeHandReady = true; }
            freeHandPosition = Vector3.SmoothDamp(freeHandPosition, freeTarget, ref freeHandVelocity, .13f, 4f, dt);
            leftArm.Solve(freeHandPosition, -chest.right - Vector3.up * .35f, chest.rotation * Quaternion.Euler(0, -30f, -20f));
            PoseFeet(velocity, collapse, dt);
            UpdateThreads(grip, bladeRotation, dt);
            SaveTargets();
        }

        public void RefreshThreads(Vector3 grip, Quaternion bladeRotation, float dt)
        {
            if (threads != null) UpdateThreads(grip, bladeRotation, dt);
        }

        // Apply bounded physics to the trunk, then close the limb chains again.
        // Independent world-position overrides of every bone used to stretch the
        // visible skeleton even when the physical joint itself remained connected.
        public void ResolvePhysicalContacts(Vector3 grip, Quaternion bladeRotation,
            bool leftArmIntact, bool rightArmIntact, bool leftLegIntact, bool rightLegIntact)
        {
            if (rightArmIntact) rightArm.Solve(grip, chest.right - chest.forward * .4f - Vector3.up * .5f, bladeRotation);
            if (leftArmIntact) leftArm.Solve(freeHandPosition, -chest.right - Vector3.up * .35f,
                chest.rotation * Quaternion.Euler(0, -30f, -20f));
            Quaternion stance = Quaternion.Euler(0, pelvisYaw, 0);
            if (leftLegIntact) leftLeg.Solve(feet[0].Position, stance * Vector3.forward,
                Quaternion.Euler(feet[0].Stepping ? -Mathf.Sin(feet[0].Progress * Mathf.PI * 2f) * 12f : 0f, feet[0].Yaw, 0));
            if (rightLegIntact) rightLeg.Solve(feet[1].Position, stance * Vector3.forward,
                Quaternion.Euler(feet[1].Stepping ? -Mathf.Sin(feet[1].Progress * Mathf.PI * 2f) * 12f : 0f, feet[1].Yaw, 0));
        }

        private void SaveTargets()
        {
            Transform[] nodes = PhysicalNodes;
            if (targetLocalPositions == null)
            {
                targetLocalPositions = new Vector3[nodes.Length];
                targetLocalRotations = new Quaternion[nodes.Length];
            }
            for (int i = 0; i < nodes.Length; i++)
            {
                targetLocalPositions[i] = nodes[i].localPosition;
                targetLocalRotations[i] = nodes[i].localRotation;
            }
        }

        private void RestoreTargets()
        {
            if (targetLocalPositions == null) return;
            Transform[] nodes = PhysicalNodes;
            for (int i = 0; i < nodes.Length; i++)
            {
                nodes[i].localPosition = targetLocalPositions[i];
                nodes[i].localRotation = targetLocalRotations[i];
            }
        }

        private void UpdateThreads(Vector3 grip, Quaternion bladeRotation, float dt)
        {
            if (threads == null) return;
            bodyBar.position = Vector3.Lerp(bodyBar.position, root.position + Vector3.up * 2.86f, 1 - Mathf.Exp(-9f * dt));
            bodyBar.rotation = Quaternion.Euler(0, pelvisYaw * .4f, -pelvisRate * .025f);
            weaponBar.position = Vector3.Lerp(weaponBar.position, grip + Vector3.up * 1.38f, 1 - Mathf.Exp(-13f * dt));
            weaponBar.rotation = Quaternion.Euler(0, chestYaw, -chestRate * .018f);
            Thread(0, bodyBar.TransformPoint(Vector3.left * .43f), leftArm.Root.position);
            Thread(1, bodyBar.TransformPoint(Vector3.right * .43f), rightArm.Root.position);
            Thread(2, bodyBar.position, neck.position);
            Thread(3, bodyBar.TransformPoint(Vector3.back * .15f), pelvis.position);
            Thread(4, weaponBar.TransformPoint(Vector3.left * .26f), rightArm.Tip.position);
            Thread(5, weaponBar.TransformPoint(Vector3.right * .26f), grip + bladeRotation * Vector3.forward * .75f);
        }

        private void Thread(int i, Vector3 a, Vector3 b) { threads[i].SetPosition(0, a); threads[i].SetPosition(1, b); }

        private static void Spring(ref float angle, ref float rate, float target, float stiffness, float damping, float maxSpeed, float dt)
        {
            rate = Mathf.Clamp(rate + (Mathf.DeltaAngle(angle, target) * stiffness - rate * damping) * dt, -maxSpeed, maxSpeed);
            angle = Mathf.Repeat(angle + rate * dt + 180f, 360f) - 180f;
        }

        private static Transform Joint(string name, Transform parent, Vector3 position)
        {
            Transform t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = position; return t;
        }

        private static Transform Part(string name, PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localScale = scale;
            foreach (Collider c in go.GetComponents<Collider>()) { c.enabled = false; Object.Destroy(c); }
            go.GetComponent<Renderer>().sharedMaterial = material; return go.transform;
        }

        private static Material NewMaterial(Color color)
        {
            Material retained = Resources.Load<Material>("HaoxiRuntimeLit");
            Material result = retained != null ? new Material(retained) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            result.color = color; return result;
        }

        private sealed class Limb
        {
            public Transform Root { get; }
            public Transform Mid { get; }
            public Transform Tip { get; }
            private readonly float upper, lower;
            public float Length => upper + lower;

            public Limb(string name, Transform parent, Vector3 anchor, float a, float b, float width,
                Material wood, Material dark, Material brass, bool foot)
            {
                upper = a; lower = b;
                Root = Joint(name + " shoulder or hip", parent, anchor);
                Mid = Joint(name + " elbow or knee", Root, Vector3.up * upper);
                Tip = Joint(name + " wrist or ankle", Mid, Vector3.up * lower);
                Segment(Root, a, width, wood, dark, brass);
                Segment(Mid, b, width * .85f, wood, dark, brass);
                Part(name + (foot ? " carved shoe" : " hand"), foot ? PrimitiveType.Capsule : PrimitiveType.Sphere, Tip,
                    foot ? new Vector3(0, 0, .07f) : Vector3.zero,
                    foot ? new Vector3(.17f, .10f, .30f) : new Vector3(.15f, .15f, .16f), dark);
            }

            private static void Segment(Transform joint, float len, float radius, Material wood, Material dark, Material brass)
            {
                Part("Ball joint", PrimitiveType.Sphere, joint, Vector3.zero, Vector3.one * radius * 1.5f, dark);
                Part("Wooden bone", PrimitiveType.Capsule, joint, Vector3.up * len * .5f, new Vector3(radius, len * .48f, radius), wood);
                Part("Metal pin", PrimitiveType.Sphere, joint, Vector3.zero, Vector3.one * radius * .62f, brass);
            }

            public void Solve(Vector3 requested, Vector3 pole, Quaternion endRotation)
            {
                Vector3 origin = Root.position;
                Vector3 displacement = requested - origin;
                float distance = Mathf.Clamp(displacement.magnitude, Mathf.Abs(upper - lower) + .02f, upper + lower - .01f);
                Vector3 forward = displacement.sqrMagnitude > .0001f ? displacement.normalized : Vector3.down;
                Vector3 bend = Vector3.ProjectOnPlane(pole, forward);
                if (bend.sqrMagnitude < .001f) bend = Vector3.ProjectOnPlane(Vector3.forward, forward);
                if (bend.sqrMagnitude < .001f) bend = Vector3.right;
                bend.Normalize();
                float along = (upper * upper - lower * lower + distance * distance) / (2f * distance);
                Vector3 elbow = origin + forward * along + bend * Mathf.Sqrt(Mathf.Max(0, upper * upper - along * along));
                Vector3 tip = origin + forward * distance;
                Root.rotation = Quaternion.FromToRotation(Vector3.up, (elbow - origin).normalized);
                Mid.rotation = Quaternion.FromToRotation(Vector3.up, (tip - elbow).normalized);
                Tip.rotation = endRotation;
            }
        }
    }
}
