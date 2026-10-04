using System;
using System.Collections.Generic;
using UnityEngine;

namespace HaoxiKaiyan
{
    // The floor body translates. A separate simulated blade pulls the visible articulated rig.
    [DefaultExecutionOrder(-30)]
    public sealed partial class MarionetteController : MonoBehaviour
    {
        public const int ActorLayer = 8;
        public const float BladeStart = .24f;
        public enum BuffKind { Movement, Strength, MaxHealth }
        private Rigidbody body, weaponBody;
        private Collider bodyCollider, weaponCollider;
        private Transform weapon;
        private TrailRenderer bladeTrail;
        private PuppetRig rig;
        private PuppetDynamics dynamics;
        private ConfigurableJoint weaponJoint;
        private static readonly List<MarionetteController> combatants = new List<MarionetteController>();
        public static IReadOnlyList<MarionetteController> Combatants => combatants;
        private Vector2 movement, weaponInput;
        private Vector3 enemyMovement;
        private Vector3 motorVelocity;
        private float health = GameplayTuning.Combat.PlayerHealth;
        private float maxHealth = GameplayTuning.Combat.PlayerHealth;
        private float baseHealth = GameplayTuning.Combat.PlayerHealth;
        private const float DefaultGripHeight = GameplayTuning.Weapon.GripHeight;
        private const float MinimumActorSeparation = GameplayTuning.Movement.OpposingSpacing;
        private float yawTarget, orbitYaw, orbitRate, pull, lastPullTime = -10f;
        private int movementBuffs, strengthBuffs, healthBuffs;
        private WeaponSpec weaponSpec;
        private Material weaponDark, weaponBrass, weaponBlade;
        private readonly float[] jointStrain = new float[15];
        private float nextDamageTime, stunUntil, enemyWindup, enemyStrike;
        private float enemyAttackPower = 1f;
        private float collapse, knockdownUntil, deathFinishAt;
        private bool knockdownPending, launchWasAirborne, knockedDown;
        private int swingSign;
        private Vector3 previousTip, previousRoot;
        private bool built;

        public bool IsEnemy { get; private set; }
        public bool IsDefeated => health <= 0f;
        public bool IsInterrupted => Time.time < stunUntil || knockdownPending || knockedDown || collapse > .12f ||
            (body != null && body.position.y > .18f);
        public bool IsKnockedDown => knockedDown;
        public float VisualCollapse => collapse;
        public bool DeathPoseReady => IsDefeated && knockedDown && Time.time >= deathFinishAt;
        public float Health01 => Mathf.Clamp01(health / maxHealth);
        public float MaxHealth => maxHealth;
        public float Health => health;
        public float MoveSpeed => body == null ? 0f :
            Vector3.ProjectOnPlane(body.isKinematic ? motorVelocity : body.linearVelocity, Vector3.up).magnitude;
        public bool WeaponInputActive => weaponInput.sqrMagnitude > .01f;
        public bool CanDamage => weapon != null && !IsDefeated && !IsInterrupted &&
            (IsEnemy ? enemyStrike > .5f : Time.time < lastPullTime + GameplayTuning.Weapon.InertiaDamageWindow);
        public float WeaponLength => weaponSpec.length;
        public float WeaponContactRadius => weaponSpec.kind == WeaponKind.WarHammer ? .19f :
            weaponSpec.weaponClass == WeaponClass.Blunt ? .13f : .105f;
        public WeaponKind EquippedWeapon => weaponSpec.kind;
        public WeaponSpec CurrentWeaponSpec => weaponSpec;
        public float StrikeStrength => StrengthMultiplier;
        public float EnemyAttackPower => enemyAttackPower;
        public HitZone LastHitZone { get; private set; }
        public float WeaponGoalGap => weapon == null ? 0f : Vector3.Distance(weaponBody.position, ReachableWeaponGoal());
        public float WeaponWristGap => weapon == null ? 0f : Vector3.Distance(weaponBody.position, dynamics.Wrist.position);
        public Vector2 WeaponControlIndicator => weapon == null ? Vector2.zero :
            new Vector2(Mathf.Sin(weaponBody.rotation.eulerAngles.y * Mathf.Deg2Rad),
                Mathf.Cos(weaponBody.rotation.eulerAngles.y * Mathf.Deg2Rad)) *
            weaponInput.magnitude;
        public float WeaponSwingSpeed { get; private set; }
        public int SwingSequence { get; private set; }
        public int FootSteps => rig?.StepCount ?? 0;
        public bool LeftFootStepping => rig?.LeftFootStepping ?? false;
        public bool RightFootStepping => rig?.RightFootStepping ?? false;
        public Vector3 LeftFootGoal => rig?.LeftFootGoal ?? Vector3.zero;
        public Vector3 RightFootGoal => rig?.RightFootGoal ?? Vector3.zero;
        public float ChestYaw => rig?.ChestYaw ?? 0f;
        public float PelvisYaw => rig?.PelvisYaw ?? 0f;
        public float MaxJointError => dynamics?.MaxJointError ?? float.PositiveInfinity;
        public Transform[] PoseJoints => rig?.PhysicalNodes;
        public bool KneeBroken => dynamics != null && dynamics.IsKneeBroken;
        public Transform WeaponHand => rig?.Wrist;
        public Transform LeftFoot => rig?.LeftFoot;
        public Transform RightFoot => rig?.RightFoot;
        public Rigidbody Body => body;
        public Collider BodyCollider => bodyCollider;
        public Rigidbody WeaponBody => weaponBody;
        public Transform WeaponTransform => weapon;
        public Collider WeaponCollider => weaponCollider;
        public HitImpact LastImpact { get; private set; }
        public event Action<MarionetteController, float, Vector3> Damaged;
        public event Action<Vector3> WeaponBlocked;

        public void Build(bool enemy, Vector3 position, Quaternion facing, WeaponKind? startingWeapon = null)
        {
            IsEnemy = enemy;
            baseHealth = enemy ? GameplayTuning.Combat.EnemyHealth : GameplayTuning.Combat.PlayerHealth;
            maxHealth = health = baseHealth;
            weaponSpec = WeaponSpec.Get(startingWeapon ?? (enemy ? WeaponKind.BaseballBat : WeaponKind.WoodenSaber));
            combatants.Add(this);
            // Only the articulated hit zones are scanned; the locomotion capsule is invisible
            // to the blade query but still supports feet and normal physics collisions.
            gameObject.layer = 0;
            transform.SetPositionAndRotation(position, Quaternion.identity);
            yawTarget = orbitYaw = facing.eulerAngles.y;
            body = gameObject.AddComponent<Rigidbody>();
            // Navigation has its own grounded motor. The much lighter joint bodies must
            // articulate around it rather than lifting or flinging the whole actor.
            body.mass = enemy ? 7f : 8f;
            body.linearDamping = .45f;
            body.angularDamping = 6f;
            body.useGravity = true;
            body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0, .95f, 0);
            capsule.height = 1.88f;
            capsule.radius = .27f;
            bodyCollider = capsule;

            Material wood = Mat(enemy ? new Color(.29f, .35f, .31f) : new Color(.55f, .28f, .12f));
            Material dark = Mat(enemy ? new Color(.12f, .19f, .17f) : new Color(.21f, .10f, .055f));
            Material brass = Mat(new Color(.72f, .47f, .17f), .38f);
            Material cloth = Mat(enemy ? new Color(.40f, .46f, .40f) : new Color(.83f, .75f, .59f));
            Material thread = Mat(new Color(.16f, .15f, .14f));
            weaponDark = dark; weaponBrass = brass; weaponBlade = cloth;
            rig = new PuppetRig(transform, enemy, facing, wood, dark, brass, cloth, thread);
            BuildWeapon(dark, brass, cloth);
            rig.Pose(weapon.position, weapon.rotation, Vector3.zero, false, 0f, .02f);
            dynamics = new PuppetDynamics(this, rig, body, bodyCollider);
            AttachWeaponJoint();
            previousTip = weaponBody.position + weaponBody.rotation * Vector3.forward * WeaponLength;
            previousRoot = body.position;
            body.isKinematic = true;
            built = true;
        }

        public void SetControls(Vector2 move, Vector2 pullDirection)
        {
            if (IsDefeated) return;
            movement = Vector2.ClampMagnitude(move, 1f);
            weaponInput = Vector2.ClampMagnitude(pullDirection, 1f);
            if (weaponInput.magnitude > GameplayTuning.Weapon.IntentThreshold)
            {
                yawTarget = Mathf.Atan2(weaponInput.x, weaponInput.y) * Mathf.Rad2Deg;
                lastPullTime = Time.time;
            }
        }


        public void GrantBuff(BuffKind kind)
        {
            if (IsEnemy) return;
            switch (kind)
            {
                case BuffKind.Movement: movementBuffs = Mathf.Min(7, movementBuffs + 1); break;
                case BuffKind.Strength: strengthBuffs = Mathf.Min(7, strengthBuffs + 1); break;
                case BuffKind.MaxHealth:
                    healthBuffs = Mathf.Min(8, healthBuffs + 1);
                    maxHealth = baseHealth + 20f * healthBuffs;
                    health = Mathf.Min(maxHealth, health + 20f);
                    break;
            }
        }

        public void ClearBuffs()
        {
            movementBuffs = strengthBuffs = healthBuffs = 0;
            maxHealth = baseHealth;
            health = Mathf.Min(health, maxHealth);
        }

        private float StrengthMultiplier => Mathf.Min(1.9f, 1f + strengthBuffs * .11f);
        private float MovementMultiplier => Mathf.Min(1.75f, Mathf.Pow(1.09f, movementBuffs));

        public void SetEnemyFacing(Vector3 direction)
        {
            if (direction.sqrMagnitude > .001f)
                yawTarget = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        }

        public void SetEnemyMovement(Vector3 velocity, bool windingUp, bool striking = false)
            => SetEnemyMovement(velocity, windingUp, striking, 1f);

        public void SetEnemyMovement(Vector3 velocity, bool windingUp, bool striking, float attackPower)
        {
            enemyMovement = velocity;
            enemyAttackPower = attackPower;
            enemyWindup = windingUp ? (attackPower > 1.1f ? 1.20f : 1f) : 0f;
            enemyStrike = striking ? (attackPower > 1.1f ? 1.35f : 1f) : 0f;
        }

        public void SetTelegraph(bool active) => rig?.SetWarning(active && !IsInterrupted);

        public void ResetActor(Vector3 position, Quaternion facing)
        {
            if (!built || rig == null || dynamics == null) return;
            if (weapon == null)
            {
                weaponSpec = WeaponSpec.Get(IsEnemy ? WeaponKind.BaseballBat : WeaponKind.WoodenSaber);
                BuildWeapon(weaponDark, weaponBrass, weaponBlade);
                dynamics.IgnoreOwnWeapon(weaponCollider);
                AttachWeaponJoint();
            }
            if (body.isKinematic) body.isKinematic = false;
            body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            health = maxHealth; nextDamageTime = stunUntil = 0f; lastPullTime = -10f;
            for (int i = 0; i < jointStrain.Length; i++) jointStrain[i] = 0f;
            LastHitZone = HitZone.Body;
            collapse = knockdownUntil = deathFinishAt = 0f;
            knockdownPending = launchWasAirborne = knockedDown = false;
            movement = weaponInput = Vector2.zero;
            pull = 0f;
            body.position = position; transform.position = position;
            body.linearVelocity = body.angularVelocity = Vector3.zero;
            motorVelocity = Vector3.zero;
            yawTarget = orbitYaw = facing.eulerAngles.y; orbitRate = 0f;
            weaponBody.position = DesiredWeaponPosition(); weaponBody.rotation = DesiredWeaponRotation();
            weaponBody.linearVelocity = weaponBody.angularVelocity = Vector3.zero;
            rig.ResetPose(position, facing, weaponBody.position);
            rig.Pose(weaponBody.position, weaponBody.rotation, Vector3.zero, false, 0f, .02f);
            dynamics?.Reset();
            body.isKinematic = true;
            previousRoot = position;
            previousTip = weaponBody.position + weaponBody.rotation * Vector3.forward * WeaponLength;
            weapon.GetComponent<MeleeWeaponHitbox>().ResetHistory();
        }

        private void LateUpdate()
        {
            if (!built || rig == null || dynamics == null || Time.deltaTime <= 0f) return;
            if (knockedDown && !IsDefeated && Time.time >= knockdownUntil) knockedDown = false;
            collapse = Mathf.MoveTowards(collapse, knockedDown ? 1f : 0f, Time.deltaTime * (knockedDown ? 6f : 2.7f));
            Vector3 grip = weapon != null ? weapon.position : rig.Wrist.position;
            Quaternion bladeRotation = weapon != null ? weapon.rotation : rig.Wrist.rotation;
            // Let the muscle aim a short distance ahead of the actual blade while
            // keeping the rendered hand on the actual grip after physics blending.
            Vector3 poseGrip = weapon != null && WeaponInputActive ?
                Vector3.MoveTowards(grip, ReachableWeaponGoal(), .32f) : grip;
            rig.Pose(poseGrip, bladeRotation, body.isKinematic ? motorVelocity : body.linearVelocity,
                enemyWindup > .5f, collapse, Time.deltaTime, WeaponInputActive || IsEnemy &&
                (enemyWindup > .5f || enemyStrike > .5f));
            dynamics?.CaptureTargets();
            dynamics?.ApplyVisualPose(grip, bladeRotation);
            if (bladeTrail != null)
            {
                bladeTrail.emitting = CanDamage && WeaponSwingSpeed > 1.25f;
                bladeTrail.widthMultiplier = Mathf.Lerp(.055f, .15f, Mathf.InverseLerp(1.2f, 8f, WeaponSwingSpeed));
            }
        }

        private void OnDestroy()
        {
            combatants.Remove(this);
            dynamics?.Dispose();
            if (weapon != null) Destroy(weapon.gameObject);
        }
    }
}
