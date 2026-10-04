using UnityEngine;

namespace HaoxiKaiyan
{
    // A dropped Rigidbody stays in the world; the ring is only an unobtrusive pickup cue.
    public sealed class WeaponPickup : MonoBehaviour
    {
        private LineRenderer ring;
        private Material ringMaterial;
        public WeaponKind Kind { get; private set; }
        public float ReadyAt { get; private set; }

        public void Configure(WeaponKind kind)
        {
            Kind = kind;
            ReadyAt = Time.time + .20f;
            GameObject marker = new GameObject("Quiet pickup circle");
            ring = marker.AddComponent<LineRenderer>();
            ring.positionCount = 33;
            ring.loop = true;
            ring.useWorldSpace = true;
            ring.widthMultiplier = .013f;
            ringMaterial = Resources.Load<Material>("HaoxiRuntimeTrail");
            if (ringMaterial != null) ring.sharedMaterial = ringMaterial;
            ring.startColor = ring.endColor = new Color(.68f, .44f, .14f, .72f);
        }

        private void LateUpdate()
        {
            if (ring == null) return;
            Vector3 center = transform.position;
            float radius = .35f + Mathf.Sin(Time.time * 2f) * .025f;
            for (int i = 0; i < 33; i++)
            {
                float angle = i * Mathf.PI * 2f / 32f;
                ring.SetPosition(i, new Vector3(center.x + Mathf.Cos(angle) * radius,
                    .045f, center.z + Mathf.Sin(angle) * radius));
            }
        }

        private void OnDestroy()
        {
            if (ring != null) Destroy(ring.gameObject);
        }

        public static WeaponPickup Spawn(WeaponKind kind, Vector3 position)
        {
            WeaponSpec spec = WeaponSpec.Get(kind);
            GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shaft.name = "Ground " + kind;
            shaft.transform.position = position + Vector3.up * .16f;
            shaft.transform.rotation = Quaternion.Euler(85f, 24f, 0f);
            shaft.transform.localScale = new Vector3(.11f, .11f, spec.length);
            Material source = Resources.Load<Material>("HaoxiRuntimeLit");
            if (source != null)
            {
                Material wood = new Material(source) { color = new Color(.37f, .23f, .13f) };
                shaft.GetComponent<Renderer>().sharedMaterial = wood;
            }
            Rigidbody body = shaft.AddComponent<Rigidbody>();
            body.mass = spec.mass;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearDamping = spec.damping;
            body.angularDamping = spec.damping;
            if (kind == WeaponKind.WarHammer || kind == WeaponKind.SawAxe)
            {
                GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
                head.name = "Weighted head";
                head.transform.SetParent(shaft.transform, false);
                head.transform.localPosition = Vector3.forward * .42f;
                head.transform.localScale = new Vector3(3.4f, 2.5f, .18f);
                // Unity's default primitive material is not the project's URP
                // runtime material and renders pink in the WebGL player.
                head.GetComponent<Renderer>().sharedMaterial = shaft.GetComponent<Renderer>().sharedMaterial;
                Destroy(head.GetComponent<Collider>());
            }
            WeaponPickup pickup = shaft.AddComponent<WeaponPickup>();
            pickup.Configure(kind);
            return pickup;
        }
    }
}
