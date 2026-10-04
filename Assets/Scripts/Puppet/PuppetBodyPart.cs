using UnityEngine;

namespace HaoxiKaiyan
{
    public enum HitZone { Body, Head, Neck, Wrist, Knee, Limb }

    public sealed class PuppetBodyPart : MonoBehaviour
    {
        public MarionetteController Owner { get; private set; }
        public Rigidbody Body { get; private set; }
        public int Index { get; private set; }
        private Collider jointCollider, shaftCollider, neckCollider;

        public void Configure(MarionetteController owner, Rigidbody body, int index,
            Collider joint, Collider shaft, Collider neck)
        {
            Owner = owner;
            Body = body;
            Index = index;
            jointCollider = joint;
            shaftCollider = shaft;
            neckCollider = neck;
        }

        public HitZone ZoneFor(Collider collider)
        {
            if (collider == neckCollider) return HitZone.Neck;
            if (collider == jointCollider)
            {
                if (Index == 5 || Index == 8) return HitZone.Wrist;
                if (Index == 10 || Index == 13) return HitZone.Knee;
                if (Index == 2) return HitZone.Head;
            }
            return shaftCollider == collider ? HitZone.Limb : HitZone.Body;
        }
    }
}
