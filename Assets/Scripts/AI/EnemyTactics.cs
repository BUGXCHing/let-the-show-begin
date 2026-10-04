using System.Collections.Generic;
using UnityEngine;

namespace HaoxiKaiyan
{
    internal enum EnemyPhase { Approach, Windup, Strike, Recovery }
    internal sealed class EnemyBrain
    {
        public MarionetteController actor;
        public EnemyPhase phase;
        public float timer, attackCooldown, dodgeCooldown, dodgeTimer;
        public Vector3 strikeDirection, dodgeDirection;
        public int slot;
        public int attackCount;
        public bool heavyStrike;
    }
    /// <summary>Shared attack slots, individual light/heavy strikes, dodging and separation.</summary>
    internal static class EnemyTactics
    {
        internal static void Tick(MarionetteController player, List<EnemyBrain> enemies, float dt)
        {
            if (player.IsDefeated) return;
            int engaged = 0;
            foreach (EnemyBrain brain in enemies)
                if (brain.actor != null && !brain.actor.IsDefeated &&
                    (brain.phase == EnemyPhase.Windup || brain.phase == EnemyPhase.Strike)) engaged++;
            foreach (EnemyBrain brain in enemies)
            {
                MarionetteController actor = brain.actor;
                if (actor == null || actor.IsDefeated) continue;
                if (actor.IsInterrupted)
                {
                    brain.phase = EnemyPhase.Recovery;
                    brain.timer = GameplayTuning.Enemy.RecoveryDuration;
                    brain.attackCooldown = .42f;
                    actor.SetTelegraph(false);
                    actor.SetEnemyMovement(Vector3.zero, false);
                    continue;
                }
                Vector3 planar = Vector3.ProjectOnPlane(player.transform.position - actor.transform.position, Vector3.up);
                float distance = planar.magnitude;
                Vector3 toward = distance > .001f ? planar / distance : Vector3.forward;
                Vector3 side = new Vector3(-toward.z, 0f, toward.x) * ((brain.slot & 1) == 0 ? 1f : -1f);
                if (brain.phase == EnemyPhase.Approach && distance > .001f) actor.SetEnemyFacing(planar);
                brain.attackCooldown -= dt;
                brain.dodgeCooldown -= dt;
                brain.dodgeTimer -= dt;
                Vector3 velocity = Vector3.zero;
                bool winding = false;
                switch (brain.phase)
                {
                    case EnemyPhase.Approach:
                        // Circle at reach, rush when outside it, and give each puppet a
                        // different lane. This permits alternating and pincer attacks.
                        velocity = distance > 2.05f ? toward * GameplayTuning.Enemy.ChaseSpeed + side * .60f :
                            distance < 1.18f ? -toward * 1.3f + side * .8f : side * .65f;
                        if (distance < 2.65f && player.WeaponTransform != null &&
                            player.WeaponSwingSpeed > 3.8f && brain.dodgeCooldown <= 0f)
                        {
                            Vector3 blade = player.WeaponTransform.position +
                                player.WeaponTransform.forward * player.WeaponLength;
                            if (Vector3.Distance(blade, actor.transform.position + Vector3.up * 1.1f) < 2.1f)
                            {
                                actor.SetEnemyFacing(player.WeaponTransform.position - actor.transform.position);
                                brain.dodgeDirection = side - toward * .35f;
                                brain.dodgeTimer = .28f;
                                brain.dodgeCooldown = 1.15f;
                            }
                        }
                        if (brain.dodgeTimer > 0f) velocity = brain.dodgeDirection.normalized * GameplayTuning.Enemy.ChaseSpeed;
                        bool attackSlot = engaged < Mathf.Min(2, Mathf.Max(1, (enemies.Count + 1) / 3)) ||
                            (enemies.Count >= 3 && (brain.slot % 3) == Mathf.FloorToInt(Time.time * 1.15f) % 3);
                        if (distance < 2.35f && brain.attackCooldown <= 0f && attackSlot && brain.dodgeTimer <= 0f)
                        {
                            brain.phase = EnemyPhase.Windup;
                            brain.attackCount++;
                            brain.heavyStrike = brain.attackCount % 2 == 1 &&
                                (player.MoveSpeed < 2f || player.IsInterrupted || distance < 1.7f);
                            brain.timer = brain.heavyStrike ? GameplayTuning.Enemy.HeavyWindup : GameplayTuning.Enemy.LightWindup;
                            brain.strikeDirection = toward;
                            winding = true;
                            velocity = Vector3.zero;
                            engaged++;
                        }
                        break;
                    case EnemyPhase.Windup:
                        winding = true;
                        brain.timer -= dt;
                        // An approaching puppet commits immediately rather than waiting in
                        // front of the player for a long passive telegraph.
                        if (brain.timer <= 0f)
                        {
                            brain.phase = EnemyPhase.Strike;
                            brain.timer = GameplayTuning.Enemy.StrikeDuration;
                            winding = false;
                        }
                        break;
                    case EnemyPhase.Strike:
                        velocity = brain.strikeDirection * GameplayTuning.Enemy.StrikeSpeed;
                        brain.timer -= dt;
                        if (brain.timer <= 0f)
                        {
                            brain.phase = EnemyPhase.Recovery;
                            brain.timer = GameplayTuning.Enemy.RecoveryDuration;
                            brain.attackCooldown = .90f + brain.slot * .055f;
                        }
                        break;
                    case EnemyPhase.Recovery:
                        brain.timer -= dt;
                        if (brain.timer <= 0f) brain.phase = EnemyPhase.Approach;
                        break;
                }
                // Soft flocking reinforces the root separation and prevents a single
                // visually tangled ball of enemies from pursuing the same point.
                foreach (EnemyBrain other in enemies)
                {
                    if (other == brain || other.actor == null || other.actor.IsDefeated) continue;
                    Vector3 away = Vector3.ProjectOnPlane(actor.transform.position - other.actor.transform.position, Vector3.up);
                    float gap = away.magnitude;
                    if (gap > .01f && gap < 1.55f) velocity += away / gap * (1.55f - gap) * 3.0f;
                }
                actor.SetTelegraph(winding);
                actor.SetEnemyMovement(Vector3.ClampMagnitude(velocity, GameplayTuning.Enemy.StrikeSpeed), winding,
                    brain.phase == EnemyPhase.Strike, brain.heavyStrike ? GameplayTuning.Enemy.HeavyAttackPower : GameplayTuning.Enemy.LightAttackPower);
            }
        }

    }
}
