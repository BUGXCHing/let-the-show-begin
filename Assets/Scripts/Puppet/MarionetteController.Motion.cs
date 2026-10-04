using UnityEngine;

namespace HaoxiKaiyan
{
    public sealed partial class MarionetteController
    {
        // Fixed-tick ordering is part of the controller: update the root before the weapon.
        private void FixedUpdate()
        {
            if (!built || rig == null || dynamics == null) return;
            float dt = Time.fixedDeltaTime;
            UpdateMovement(dt);
            SimulateWeapon(dt);
        }

        private void UpdateMovement(float dt)
        {
            if (knockdownPending)
            {
                if (body.position.y > .22f) launchWasAirborne = true;
                if (launchWasAirborne && body.position.y < .10f && body.linearVelocity.y < .6f)
                    BeginKnockdown();
            }
            if (!knockdownPending && !knockedDown && !IsDefeated &&
                Time.time >= stunUntil && body.position.y < .12f)
            {
                body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
                if (!body.isKinematic)
                {
                    motorVelocity = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
                    body.isKinematic = true;
                }
            }
            if (!IsDefeated && !IsInterrupted)
            {
                // A grounded motor owns navigation. During hits we release the root back to
                // dynamics for recoil/launch; during walking joints move around this anchor.
                float speed = GameplayTuning.Movement.PlayerSpeed * MovementMultiplier * (1f - weaponSpec.movePenalty);
                Vector3 target = IsEnemy ? enemyMovement * (dynamics.IsKneeBroken ? .28f : 1f) :
                    new Vector3(movement.x, 0f, movement.y) * speed;
                float navigationAcceleration = IsEnemy ? GameplayTuning.Movement.EnemyAcceleration : GameplayTuning.Movement.PlayerAcceleration * MovementMultiplier *
                    (1f - weaponSpec.movePenalty * 1.6f);
                motorVelocity = Vector3.MoveTowards(motorVelocity, target, navigationAcceleration * dt);
                Vector3 next = body.position + motorVelocity * dt;
                next = new Vector3(Mathf.Clamp(next.x, -GameplayTuning.Movement.ArenaHalfExtent, GameplayTuning.Movement.ArenaHalfExtent), 0f,
                    Mathf.Clamp(next.z, -GameplayTuning.Movement.ArenaHalfExtent, GameplayTuning.Movement.ArenaHalfExtent));
                // The two walking roots deliberately ignore physical collision so hits
                // cannot launch a kinematic motor. Keep their capsules apart here;
                // otherwise a held blade can be driven into the opponent's joints.
                foreach (MarionetteController neighbor in combatants)
                {
                    if (neighbor == null || neighbor == this || neighbor.IsDefeated || neighbor.Body == null) continue;
                    float separation = neighbor.IsEnemy == IsEnemy ? GameplayTuning.Movement.SameTeamSpacing : MinimumActorSeparation;
                    Vector3 away = Vector3.ProjectOnPlane(next - neighbor.Body.position, Vector3.up);
                    if (away.sqrMagnitude < separation * separation)
                    {
                        if (away.sqrMagnitude < .0001f)
                            away = Vector3.ProjectOnPlane(body.position - neighbor.Body.position, Vector3.up);
                        if (away.sqrMagnitude < .0001f) away = IsEnemy ? Vector3.right : Vector3.left;
                        next = neighbor.Body.position + away.normalized * separation;
                        next = new Vector3(Mathf.Clamp(next.x, -GameplayTuning.Movement.ArenaHalfExtent, GameplayTuning.Movement.ArenaHalfExtent), 0f,
                            Mathf.Clamp(next.z, -GameplayTuning.Movement.ArenaHalfExtent, GameplayTuning.Movement.ArenaHalfExtent));
                    }
                }
                motorVelocity = Vector3.ProjectOnPlane((next - body.position) / dt, Vector3.up);
                body.MovePosition(next);
            }
            else motorVelocity = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);

        }
    }
}
