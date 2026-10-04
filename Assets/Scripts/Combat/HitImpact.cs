using UnityEngine;

namespace HaoxiKaiyan
{
    public enum HitTier { Light, Heavy, Launch }

    // One measured blade impact drives health, momentum, stun, camera and audio together.
    public struct HitImpact
    {
        public const float MinimumSpeed = GameplayTuning.Combat.MinimumSwingSpeed;
        public HitTier tier;
        public float speed, damage, push, lift, stun, stop;
        public Vector3 point, direction;

        public static HitImpact FromSpeed(float speed, Vector3 point, Vector3 direction)
        {
            return FromCollision(speed, .48f, .35f, .75f, point, direction);
        }

        public static HitImpact FromCollision(float speed, float weaponMass, float targetMass,
            float bladeLeverage, Vector3 point, Vector3 direction)
        {
            float reducedMass = weaponMass * targetMass / Mathf.Max(.01f, weaponMass + targetMass);
            float momentum = reducedMass * speed * Mathf.Lerp(.72f, 1.12f, Mathf.Clamp01(bladeLeverage));
            float energy = .5f * reducedMass * speed * speed;
            float force = speed * Mathf.Lerp(.88f, 1.1f, Mathf.Clamp01(bladeLeverage));
            HitTier tier = force < GameplayTuning.Combat.HeavyForceThreshold ? HitTier.Light : force < GameplayTuning.Combat.LaunchForceThreshold ? HitTier.Heavy : HitTier.Launch;
            return new HitImpact
            {
                tier = tier,
                speed = speed,
                point = point,
                direction = direction,
                damage = Mathf.Clamp(5f + speed * 2.3f + energy * 1.25f, 6f, 48f),
                push = tier == HitTier.Light ? momentum * .7f : Mathf.Clamp(momentum * 2.4f, 2.3f, 6f),
                lift = tier == HitTier.Launch ? Mathf.Clamp(3f + energy * .42f, 3.5f, 6f) : 0f,
                stun = tier == HitTier.Light ? .16f : tier == HitTier.Heavy ? .45f : .88f,
                stop = tier == HitTier.Light ? .035f : tier == HitTier.Heavy ? .065f : .09f
            };
        }
    }
}
