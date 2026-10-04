using UnityEngine;

namespace HaoxiKaiyan
{
    public sealed partial class MarionetteController
    {
        public bool ApplyWeaponHit(float speed, Vector3 point, Vector3 direction, float cooldown,
            PuppetBodyPart part, float sourceMass, float leverage, bool enemySource,
            HitZone zone, float edgeAlignment, WeaponSpec sourceWeapon, float attackerStrength,
            float sourceAttackPower = 1f)
        {
            if (IsDefeated || speed < HitImpact.MinimumSpeed || Time.time < nextDamageTime) return false;
            nextDamageTime = Time.time + cooldown;
            float targetMass = part != null ? part.Body.mass : body.mass;
            HitImpact hit = HitImpact.FromCollision(speed, sourceMass, targetMass, leverage, point, direction);
            if (enemySource)
            {
                hit.damage *= .58f;
                hit.push *= .7f;
                hit.lift *= .65f;
                if (sourceAttackPower < 1f)
                {
                    if (hit.tier == HitTier.Launch) hit.tier = HitTier.Heavy;
                    hit.lift = 0f;
                    hit.stun = Mathf.Min(hit.stun, .36f);
                    hit.damage *= .82f;
                }
                else if (sourceAttackPower > 1.1f && speed >= 3.6f)
                {
                    hit.tier = HitTier.Launch;
                    hit.push = Mathf.Max(3.1f, hit.push * 1.35f);
                    hit.lift = Mathf.Max(3.6f, hit.lift);
                    hit.stun = .80f;
                    hit.stop = .08f;
                    hit.damage *= 1.12f;
                }
            }
            // A quick blade may still hurt without toppling a braced puppet. Head/neck
            // contacts are the exception; body and limbs demand a committed fast swing.
            if (IsEnemy)
            {
                bool vulnerable = zone == HitZone.Head || zone == HitZone.Neck;
                float launchThreshold = vulnerable ? GameplayTuning.Combat.EnemyHeadLaunchSpeed : zone == HitZone.Knee ? GameplayTuning.Combat.EnemyKneeLaunchSpeed : GameplayTuning.Combat.EnemyBodyLaunchSpeed;
                hit.damage *= vulnerable ? .92f : .72f;
                if (hit.tier == HitTier.Launch && speed < launchThreshold)
                {
                    hit.tier = HitTier.Heavy;
                    hit.lift = 0f;
                    hit.push = Mathf.Min(hit.push, 2.4f);
                    hit.stun = .28f;
                    hit.stop = .055f;
                }
            }
            LastHitZone = zone;
            bool broken = IsEnemy && part != null && TryBreakPart(part, zone, sourceWeapon,
                speed, leverage, edgeAlignment, attackerStrength);
            if (broken && zone == HitZone.Neck) hit.damage = Mathf.Max(hit.damage, health);
            ReceiveHit(hit, part);
            if (broken && zone == HitZone.Wrist && part.Index == 5) DropWeapon();
            return true;
        }

        private bool TryBreakPart(PuppetBodyPart part, HitZone zone, WeaponSpec sourceWeapon,
            float speed, float leverage, float edgeAlignment, float attackerStrength)
        {
            if (zone != HitZone.Neck && zone != HitZone.Wrist && zone != HitZone.Knee) return false;
            int index = part.Index;
            float cut = sourceWeapon.sharpness * Mathf.Max(0f, edgeAlignment - .22f) *
                speed * Mathf.Lerp(.7f, 1.18f, leverage);
            float crush = sourceWeapon.breakPower * sourceWeapon.mass /
                (sourceWeapon.mass + .65f) * speed * .47f;
            float stress = sourceWeapon.weaponClass == WeaponClass.Edged ? cut + crush * .20f : crush;
            jointStrain[index] += stress * attackerStrength;
            float limit = zone == HitZone.Neck ? GameplayTuning.Combat.NeckBreakStrain : zone == HitZone.Wrist ? GameplayTuning.Combat.WristBreakStrain : GameplayTuning.Combat.KneeBreakStrain;
            return jointStrain[index] >= limit && dynamics.BreakJoint(index);
        }

        public void ReactWeaponContact(HitImpact impact, Vector3 point, Vector3 sweepDirection)
        {
            if (weaponBody == null) return;
            float obstruction = weaponSpec.mass > 1f ? .31f : .62f;
            weaponBody.linearVelocity *= 1f - obstruction * .20f;
            weaponBody.angularVelocity *= 1f - obstruction;
            weaponBody.AddForceAtPosition(-sweepDirection * Mathf.Min(3.5f,
                impact.speed * weaponSpec.mass * .18f), point, ForceMode.Impulse);
            dynamics?.ReactToWeaponContact(-sweepDirection, point,
                Mathf.Min(1.1f, impact.speed * weaponSpec.mass * .035f));
        }

        public void ReactWeaponBlock(Vector3 point, Vector3 incomingDirection, float speed)
        {
            if (weaponBody == null) return;
            ReactWeaponContact(HitImpact.FromSpeed(Mathf.Min(speed, 6f), point, incomingDirection),
                point, incomingDirection);
            WeaponBlocked?.Invoke(point);
        }

        public void ApplyEnemyHit(float damage, Vector3 shove)
        {
            if (IsDefeated || Time.time < nextDamageTime) return;
            nextDamageTime = Time.time + .35f;
            HitImpact hit = HitImpact.FromSpeed(2f, transform.position + Vector3.up * 1.35f, shove.normalized);
            hit.damage = damage; hit.push = 1.5f; hit.stun = .28f;
            ReceiveHit(hit);
        }

        private void ReceiveHit(HitImpact hit, PuppetBodyPart part = null)
        {
            LastImpact = hit;
            health = Mathf.Max(0f, health - hit.damage);
            stunUntil = Time.time + hit.stun;
            rig.React(hit.tier == HitTier.Light ? .5f : hit.tier == HitTier.Heavy ? 1.4f : 2.1f);
            if (hit.tier == HitTier.Launch && !IsDefeated)
            {
                knockdownPending = true;
                launchWasAirborne = false;
                knockedDown = false;
            }
            if (hit.lift > .01f || IsDefeated)
                body.constraints = RigidbodyConstraints.FreezeRotation;
            Vector3 direction = Vector3.ProjectOnPlane(hit.direction, Vector3.up).normalized;
            if (direction.sqrMagnitude < .1f) direction = Vector3.right;
            // VelocityChange makes the three reactions independent of different puppet masses.
            if (body.isKinematic)
            {
                body.isKinematic = false;
                body.linearVelocity = motorVelocity;
            }
            body.AddForce(direction * hit.push + Vector3.up * hit.lift, ForceMode.VelocityChange);
            dynamics?.Hit(part, hit);
            if (IsDefeated)
            {
                BeginKnockdown();
                dynamics?.Die();
                if (IsEnemy) DropWeapon();
            }
            Damaged?.Invoke(this, hit.damage, hit.point);
        }

        private void BeginKnockdown()
        {
            knockdownPending = false;
            knockedDown = true;
            knockdownUntil = Time.time + .78f;
            if (IsDefeated) deathFinishAt = Time.time + 1.15f;
        }

    }
}
