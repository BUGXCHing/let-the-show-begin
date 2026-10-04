using System.Collections.Generic;
using UnityEngine;

namespace HaoxiKaiyan
{
    // Samples the actual swept blade between physics ticks, including intermediate rotations.
    [DefaultExecutionOrder(20)]
    public sealed class MeleeWeaponHitbox : MonoBehaviour
    {
        private MarionetteController owner;
        private Vector3 previousPosition, previousRoot;
        private Quaternion previousRotation;
        private bool initialized;
        private readonly Collider[] overlapBuffer = new Collider[64];
        private readonly Dictionary<MarionetteController, Contact> contacts = new Dictionary<MarionetteController, Contact>();
        private readonly Dictionary<MarionetteController, Candidate> candidates = new Dictionary<MarionetteController, Candidate>();
        public int SweepQueries { get; private set; }
        public int RawOverlaps { get; private set; }
        public int RawEnemyOverlaps { get; private set; }
        public int FullBuffers { get; private set; }
        public int TargetCandidates { get; private set; }
        public int SuppressedContacts { get; private set; }
        public int SlowContacts { get; private set; }
        public int AppliedHits { get; private set; }
        public int ParriedHits { get; private set; }
        public float NearestGuardGap { get; private set; } = float.MaxValue;

        private struct Candidate
        {
            public PuppetBodyPart part;
            public HitZone zone;
            public Vector3 point;
            public float along, score;
        }

        private sealed class Contact
        {
            public float lastSeen = -10f, lastHit = -10f;
            public int stroke = -1, blockedStroke = -1;
        }

        public void Configure(MarionetteController actor)
        {
            owner = actor;
            ResetHistory();
        }

        public void ResetHistory()
        {
            if (owner == null) return;
            previousPosition = owner.WeaponBody.position;
            previousRotation = owner.WeaponBody.rotation;
            previousRoot = owner.Body.position;
            initialized = true;
            contacts.Clear();
            NearestGuardGap = float.MaxValue;
            SweepQueries = RawOverlaps = RawEnemyOverlaps = FullBuffers = TargetCandidates = SuppressedContacts = SlowContacts = AppliedHits = ParriedHits = 0;
        }

        private void FixedUpdate()
        {
            if (owner == null || !initialized || owner.WeaponBody == null || owner.WeaponTransform != transform) return;
            Vector3 position = owner.WeaponBody.position;
            Quaternion rotation = owner.WeaponBody.rotation;
            Vector3 rootPosition = owner.Body.position;
            if (owner.CanDamage)
            {
                float angularTravel = Quaternion.Angle(previousRotation, rotation) * Mathf.Deg2Rad * owner.WeaponLength;
                int slices = Mathf.Clamp(Mathf.CeilToInt((Vector3.Distance(previousPosition, position) + angularTravel) / .085f), 1, 36);
                for (int step = 1; step <= slices; step++)
                {
                    float t = (float)step / slices;
                    Vector3 p = Vector3.Lerp(previousPosition, position, t);
                    Quaternion q = Quaternion.Slerp(previousRotation, rotation, t);
                    Vector3 start = p + q * Vector3.forward * MarionetteController.BladeStart;
                    Vector3 end = p + q * Vector3.forward * owner.WeaponLength;
                    // Held weapons are triggers to prevent joint snagging. Solve their
                    // swept shaft intersection explicitly, before any body hit at this slice.
                    foreach (MarionetteController defender in MarionetteController.Combatants)
                    {
                        if (defender == null || defender == owner || defender.IsEnemy == owner.IsEnemy ||
                            defender.IsDefeated || defender.WeaponBody == null) continue;
                        Vector3 otherStart = defender.WeaponBody.position +
                            defender.WeaponBody.rotation * Vector3.forward * MarionetteController.BladeStart;
                        Vector3 otherEnd = defender.WeaponBody.position +
                            defender.WeaponBody.rotation * Vector3.forward * defender.WeaponLength;
                        float range = owner.WeaponContactRadius + defender.WeaponContactRadius + .045f;
                        float gap = SegmentDistanceSquared(start, end, otherStart, otherEnd);
                        NearestGuardGap = Mathf.Min(NearestGuardGap, Mathf.Sqrt(gap));
                        if (gap > range * range) continue;
                        if (!contacts.TryGetValue(defender, out Contact guard))
                        {
                            guard = new Contact(); contacts.Add(defender, guard);
                        }
                        if (guard.blockedStroke == owner.SwingSequence) continue;
                        Vector3 sweep = (position + rotation * Vector3.forward * owner.WeaponLength) -
                            (previousPosition + previousRotation * Vector3.forward * owner.WeaponLength) -
                            (rootPosition - previousRoot);
                        float guardSpeed = sweep.magnitude / Time.fixedDeltaTime;
                        if (guardSpeed < HitImpact.MinimumSpeed) continue;
                        Vector3 point = (end + otherEnd) * .5f;
                        guard.blockedStroke = owner.SwingSequence;
                        guard.lastSeen = Time.time;
                        guard.lastHit = Time.time;
                        ParriedHits++;
                        owner.ReactWeaponBlock(point, sweep.normalized, guardSpeed);
                        defender.ReactWeaponBlock(point, -sweep.normalized, guardSpeed);
                    }
                    int count = Physics.OverlapCapsuleNonAlloc(start, end, owner.WeaponContactRadius, overlapBuffer,
                        (1 << MarionetteController.ActorLayer) | 1, QueryTriggerInteraction.Ignore);
                    SweepQueries++;
                    RawOverlaps += count;
                    if (count == overlapBuffer.Length) FullBuffers++;
                    candidates.Clear();
                    for (int i = 0; i < count; i++)
                    {
                        Collider collider = overlapBuffer[i];
                        PuppetBodyPart part = collider.GetComponent<PuppetBodyPart>();
                        MarionetteController target = part != null ? part.Owner :
                            collider.GetComponentInParent<MarionetteController>();
                        if (target != null && target.IsEnemy != owner.IsEnemy) RawEnemyOverlaps++;
                        if (target == null || target == owner || target.IsEnemy == owner.IsEnemy || target.IsDefeated) continue;
                        Vector3 axis = end - start;
                        float fraction = Mathf.Clamp01(Vector3.Dot(collider.bounds.center - start, axis) / axis.sqrMagnitude);
                        Vector3 bladePoint = Vector3.Lerp(start, end, fraction);
                        Vector3 nearest = collider.ClosestPoint(bladePoint);
                        HitZone zone = part != null ? part.ZoneFor(collider) : HitZone.Body;
                        float score = Vector3.Distance(bladePoint, nearest) + (part == null ? .06f : 0f) -
                            (zone == HitZone.Neck || zone == HitZone.Wrist || zone == HitZone.Knee ? .025f : 0f);
                        if (candidates.TryGetValue(target, out Candidate prior) && prior.score <= score) continue;
                        candidates[target] = new Candidate
                        {
                            part = part,
                            zone = zone,
                            point = nearest,
                            along = Mathf.Lerp(MarionetteController.BladeStart, owner.WeaponLength, fraction),
                            score = score
                        };
                    }
                    foreach (var pair in candidates)
                    {
                        TargetCandidates++;
                        MarionetteController target = pair.Key;
                        Candidate candidate = pair.Value;
                        if (!contacts.TryGetValue(target, out Contact contact))
                        {
                            contact = new Contact(); contacts.Add(target, contact);
                        }
                        if (contact.blockedStroke == owner.SwingSequence)
                        { SuppressedContacts++; continue; }
                        bool separated = Time.time - contact.lastSeen > GameplayTuning.Combat.ContactSeparationTime;
                        bool reversed = contact.stroke != owner.SwingSequence;
                        contact.lastSeen = Time.time;
                        if ((!separated && !reversed) || Time.time - contact.lastHit < GameplayTuning.Combat.HitCooldown)
                        { SuppressedContacts++; continue; }

                        Vector3 nearest = candidate.point;
                        Vector3 bladePoint = Vector3.forward * candidate.along;
                        Vector3 motion = (position + rotation * bladePoint) - (previousPosition + previousRotation * bladePoint)
                            - (rootPosition - previousRoot);
                        Vector3 swingVelocity = motion / Time.fixedDeltaTime;
                        float speed = swingVelocity.magnitude;
                        // Body travel or an opponent walking into a held sword cannot count as a swing.
                        if (speed < HitImpact.MinimumSpeed) { SlowContacts++; continue; }
                        Vector3 targetVelocity = candidate.part != null ? candidate.part.Body.GetPointVelocity(nearest) : target.Body.GetPointVelocity(nearest);
                        // Solver projection may jump the jointed prop a long way in a
                        // single fixed tick. Preserve the real direction and slow/fast
                        // distinction, but do not turn that correction into a one-hit kill.
                        float impactSpeed = Mathf.Min(GameplayTuning.Combat.ImpactSpeedCap, Mathf.Clamp(
                            Vector3.Dot(swingVelocity - targetVelocity, swingVelocity.normalized), 0f, speed * 1.5f));
                        if (impactSpeed < HitImpact.MinimumSpeed) { SlowContacts++; continue; }
                        Vector3 radial = Vector3.ProjectOnPlane(target.Body.position - rootPosition, Vector3.up).normalized;
                        Vector3 push = (radial * .72f + swingVelocity.normalized * .28f).normalized;
                        float edgeAlignment = Mathf.Max(0f, Vector3.Dot(swingVelocity.normalized, q * Vector3.right));
                        if (owner.EquippedWeapon == WeaponKind.LongSword) edgeAlignment =
                            Mathf.Abs(Vector3.Dot(swingVelocity.normalized, q * Vector3.right));
                        if (target.ApplyWeaponHit(impactSpeed, nearest, push, GameplayTuning.Combat.HitCooldown, candidate.part,
                            owner.WeaponBody.mass * Mathf.Lerp(1f, 1.23f, owner.StrikeStrength - 1f),
                            candidate.along / owner.WeaponLength, owner.IsEnemy, candidate.zone,
                            edgeAlignment, owner.CurrentWeaponSpec, owner.StrikeStrength, owner.EnemyAttackPower))
                        {
                            AppliedHits++;
                            contact.lastHit = Time.time;
                            contact.stroke = owner.SwingSequence;
                            owner.ReactWeaponContact(target.LastImpact, nearest, swingVelocity.normalized);
                        }
                    }
                }
            }
            previousPosition = position;
            previousRotation = rotation;
            previousRoot = rootPosition;
        }

        private static float SegmentDistanceSquared(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            float s, t;
            if (a <= 1e-6f && e <= 1e-6f) return r.sqrMagnitude;
            if (a <= 1e-6f) { s = 0f; t = Mathf.Clamp01(f / e); }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= 1e-6f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                else
                {
                    float b = Vector3.Dot(d1, d2), denom = a * e - b * b;
                    s = denom > 1e-6f ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                    else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
                }
            }
            return (p1 + d1 * s - p2 - d2 * t).sqrMagnitude;
        }
    }
}
