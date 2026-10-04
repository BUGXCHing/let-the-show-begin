namespace HaoxiKaiyan
{
    /// <summary>
    /// The demo's existing defaults, grouped by the system that consumes them.
    /// Units: metres, seconds, degrees; Rigidbody angular speed uses radians/second.
    /// These are tuning choices, not universal physical constants. See docs/TUNING.md.
    /// </summary>
    public static class GameplayTuning
    {
        public static class Movement
        {
            public const float PlayerSpeed = 4.35f;
            public const float PlayerAcceleration = 29f;
            public const float EnemyAcceleration = 23f;
            public const float ArenaHalfExtent = 9.1f;
            public const float OpposingSpacing = .69f;
            public const float SameTeamSpacing = 1.05f;
        }

        public static class Weapon
        {
            public const float GripHeight = 1.38f;
            public const float IntentThreshold = .20f;
            public const float InertiaDamageWindow = .52f;
            public const float PullResponse = 10f;
            public const float IdleOrbitStiffness = 42f;
            public const float ActiveOrbitStiffness = 155f;
            public const float OrbitDriveReference = 185f;
            public const float LightOrbitDamping = 12f;
            public const float HeavyOrbitDamping = 18f;
            public const float MaxOrbitSpeed = 880f;
            public const float OrbitSpeedRatio = .72f;
            public const float StrokeDirectionThreshold = 42f;
            public const float IdleTetherStiffness = 82f;
            public const float IdleTetherDamping = 14f;
            public const float ActiveTetherDamping = 28f;
            public const float MaxTetherAcceleration = 170f;
            public const float MaxTorqueAcceleration = 205f;
            public const float ArmReach = .85f;
        }

        public static class Combat
        {
            public const float PlayerHealth = 180f;
            public const float EnemyHealth = 200f;
            public const float MinimumSwingSpeed = .65f;
            public const float HeavyForceThreshold = 3.1f;
            public const float LaunchForceThreshold = 6.4f;
            public const float ImpactSpeedCap = 9.5f;
            public const float HitCooldown = .25f;
            public const float ContactSeparationTime = .095f;
            public const float EnemyHeadLaunchSpeed = 6.5f;
            public const float EnemyKneeLaunchSpeed = 9f;
            public const float EnemyBodyLaunchSpeed = 9.3f;
            public const float NeckBreakStrain = 7.8f;
            public const float WristBreakStrain = 6.2f;
            public const float KneeBreakStrain = 8.3f;
        }

        public static class Enemy
        {
            public const int MaxLivingCount = 12;
            public const float ChaseSpeed = 3.55f;
            public const float StrikeSpeed = 4.55f;
            public const float LightAttackPower = .85f;
            public const float HeavyAttackPower = 1.4f;
            public const float LightWindup = .22f;
            public const float HeavyWindup = .43f;
            public const float StrikeDuration = .28f;
            public const float RecoveryDuration = .38f;
        }

        public static class Pose
        {
            public const float MaxDeltaTime = .04f;
            public const float ChestStiffness = 73f;
            public const float ChestDamping = 14f;
            public const float ChestMaxSpeed = 540f;
            public const float PelvisStiffness = 58f;
            public const float PelvisDamping = 13f;
            public const float PelvisMaxSpeed = 520f;
            public const float RunEnterSpeed = 2.8f;
            public const float RunExitSpeed = 2.2f;
        }
    }
}
