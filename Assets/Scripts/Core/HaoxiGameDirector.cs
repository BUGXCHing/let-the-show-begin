using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static HaoxiKaiyan.StagePalette;

namespace HaoxiKaiyan
{
    /// <summary>Creates the compact arena and wires gameplay/UI without external art dependencies.</summary>
    public sealed partial class HaoxiGameDirector : MonoBehaviour
    {
        private MarionetteController player;
        private MarionetteController enemy;
        private readonly List<EnemyBrain> enemies = new List<EnemyBrain>();
        private static readonly WeaponKind[] EnemyWeapons =
            { WeaponKind.BaseballBat, WeaponKind.LongSword, WeaponKind.Wrench, WeaponKind.SawAxe, WeaponKind.ShortKnife, WeaponKind.WarHammer };
        private DualStickInput controls;
        private VirtualStick weaponStick;
        private Image playerHealthFill;
        private Image enemyHealthFill;
        private Text statusText;
        private GameObject resultPanel;
        private bool paused;
        private bool ended;
        private Sprite circleSprite;
        private Sprite healthSprite;
        private Camera stageCamera;
        private DuelCamera duelCamera;
        private ImpactAudio impactAudio;
        private Text playerHealthText;
        private Text enemyHealthText;
        private float statusRestoreAt;
        private float hitStopUntil;
        private int spawnCount;
        private int totalParries;

        private void Start()
        {
            Application.targetFrameRate = 60;
            BuildStage();
            BuildActors();
            BuildInterface();
        }

        private void Update()
        {
            if (controls == null || player == null || enemy == null)
                return;

            if (hitStopUntil > 0f && Time.unscaledTime >= hitStopUntil)
            {
                hitStopUntil = 0f;
                if (!paused) Time.timeScale = 1f;
            }

            if (controls.RestartPressed)
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }
            if (controls.PausePressed && !ended)
                TogglePause();
            if (paused || ended)
                return;

            if (statusText != null && statusRestoreAt > 0f && Time.unscaledTime >= statusRestoreAt)
            {
                statusText.text = "好 戏 开 演    ·    牵 线 为 刃";
                statusRestoreAt = 0f;
            }

            player.SetControls(controls.Move, controls.Weapon);
            weaponStick?.SetPhysicalFeedback(player.WeaponControlIndicator);
            if (controls.WeaponDoubleTapped) player.HandleWeaponDoubleTap();
            EnemyTactics.Tick(player, enemies, Time.deltaTime);
            UpdateHud();
            if (enemies.Count > 0 && enemies.TrueForAll(e => e.actor == null || e.actor.DeathPoseReady))
                Finish(true);
            else if (player.DeathPoseReady)
                Finish(false);
        }

        private void BuildStage()
        {
            stageCamera = StageBuilder.Build(transform);
            impactAudio = stageCamera.GetComponent<ImpactAudio>();
            duelCamera = stageCamera.GetComponent<DuelCamera>();
        }

        private void BuildActors()
        {
            player = new GameObject("Player marionette").AddComponent<MarionetteController>();
            player.Build(false, new Vector3(-1.45f, 0f, -0.25f), Quaternion.Euler(0f, 70f, 0f));
            player.Damaged += OnActorDamaged;
            player.WeaponBlocked += OnWeaponBlocked;
            AddEnemy(new Vector3(1.45f, 0f, .25f));
            WeaponPickup.Spawn(WeaponKind.WarHammer, new Vector3(-2.65f, 0f, 1.65f));
            duelCamera.Configure(player, enemy);
        }

        private void AddEnemy(Vector3 position)
        {
            int index = spawnCount++;
            MarionetteController actor = new GameObject("Opponent marionette " + (index + 1))
                .AddComponent<MarionetteController>();
            Vector3 face = player.transform.position - position;
            actor.Build(true, position, Quaternion.Euler(0f, Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg, 0f),
                EnemyWeapons[index % EnemyWeapons.Length]);
            actor.Damaged += OnActorDamaged;
            actor.WeaponBlocked += OnWeaponBlocked;
            foreach (MarionetteController other in MarionetteController.Combatants)
            {
                if (other == actor || other == null) continue;
                Physics.IgnoreCollision(actor.BodyCollider, other.BodyCollider, true);
                if (actor.WeaponCollider != null && other.WeaponCollider != null)
                    Physics.IgnoreCollision(actor.WeaponCollider, other.WeaponCollider, true);
            }
            enemies.Add(new EnemyBrain
            {
                actor = actor,
                slot = index,
                attackCooldown = index == 0 ? .25f : .48f + index * .18f
            });
            if (enemy == null) enemy = actor;
        }

        private void AddEnemyFromButton()
        {
            if (ended || paused || player.IsDefeated) return;
            int living = 0;
            foreach (EnemyBrain brain in enemies)
                if (brain.actor != null && !brain.actor.IsDefeated) living++;
            if (living >= GameplayTuning.Enemy.MaxLivingCount)
            {
                if (statusText != null) { statusText.text = $"同场最多 {GameplayTuning.Enemy.MaxLivingCount} 名对手"; statusRestoreAt = Time.unscaledTime + 1.3f; }
                return;
            }
            int index = spawnCount;
            float angle = (index * 137.5f + 25f) * Mathf.Deg2Rad;
            Vector3 center = player.transform.position;
            Vector3 position = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 3.65f;
            position.x = Mathf.Clamp(position.x, -8.3f, 8.3f);
            position.z = Mathf.Clamp(position.z, -8.3f, 8.3f);
            for (int attempt = 0; attempt < 10; attempt++)
            {
                bool free = true;
                foreach (EnemyBrain brain in enemies)
                    if (brain.actor != null && !brain.actor.IsDefeated &&
                        Vector3.Distance(position, brain.actor.transform.position) < 1.35f) { free = false; break; }
                if (free) break;
                angle += .65f;
                position = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 3.65f;
                position.x = Mathf.Clamp(position.x, -8.3f, 8.3f);
                position.z = Mathf.Clamp(position.z, -8.3f, 8.3f);
            }
            AddEnemy(position);
            if (statusText != null) { statusText.text = "对手增援 · " + (living + 1); statusRestoreAt = Time.unscaledTime + 1.2f; }
            duelCamera?.SetOpponents(MarionetteController.Combatants);
        }

        private void OnWeaponBlocked(Vector3 point)
        {
            if (!isActiveAndEnabled || ended) return;
            totalParries++;
            if (totalParries % 2 != 1) return; // both weapons report the same interception
            SpawnImpact(point, new Color(.72f, .49f, .19f));
            impactAudio?.Play(HitTier.Light);
            if (statusText != null) { statusText.text = "格挡 · 兵刃相交"; statusRestoreAt = Time.unscaledTime + .52f; }
        }

        private void OnActorDamaged(MarionetteController actor, float damage, Vector3 point)
        {
            // The editor probe disables the director so hitstop must not stall its physics run.
            if (!isActiveAndEnabled) return;
            UpdateHud();
            SpawnImpact(point, actor.IsEnemy ? Vermilion : new Color(0.25f, 0.25f, 0.23f));
            HitImpact impact = actor.LastImpact;
            duelCamera?.Kick(impact.tier);
            impactAudio?.Play(impact.tier);
            hitStopUntil = Mathf.Max(hitStopUntil, Time.unscaledTime + impact.stop);
            if (!paused && !ended) Time.timeScale = .08f;
            if (statusText != null)
            {
                statusText.text = actor.IsEnemy
                    ? (impact.tier == HitTier.Launch ? "击飞 · " : impact.tier == HitTier.Heavy ? "重击 · " : "轻击 · ") + Mathf.CeilToInt(damage)
                    : "受击 · " + Mathf.CeilToInt(damage);
                statusRestoreAt = Time.unscaledTime + .70f;
            }
        }

        private void SpawnImpact(Vector3 position, Color color)
        {
            GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "Brief ink impact mark";
            flash.transform.position = position;
            flash.transform.localScale = Vector3.one * 0.16f;
            Collider c = flash.GetComponent<Collider>();
            if (c != null) Destroy(c);
            Renderer r = flash.GetComponent<Renderer>();
            r.sharedMaterial = StageBuilder.CreateMaterial(color, 0f, 0.05f);
            StartCoroutine(FadeImpact(flash, r));
        }

        private IEnumerator FadeImpact(GameObject impact, Renderer renderer)
        {
            float timer = 0.16f;
            Vector3 start = impact.transform.localScale;
            while (timer > 0f && impact != null)
            {
                float t = 1f - timer / 0.16f;
                impact.transform.localScale = Vector3.Lerp(start, start * 2.7f, t);
                timer -= Time.deltaTime;
                yield return null;
            }
            if (impact != null)
            {
                Destroy(renderer.sharedMaterial);
                Destroy(impact);
            }
        }

        private void Finish(bool won)
        {
            if (ended) return;
            ended = true;
            foreach (EnemyBrain brain in enemies)
                if (brain.actor != null) brain.actor.SetEnemyMovement(Vector3.zero, false);
            player.SetControls(Vector2.zero, Vector2.zero);
            resultPanel.SetActive(true);
            resultPanel.transform.Find("Result").GetComponent<Text>().text = won ? "好戏落幕" : "木偶倒地";
            resultPanel.transform.Find("Caption").GetComponent<Text>().text = won
                ? "这一击，是你亲手牵出来的。"
                : "再试一次：走位拉开距离，提速后再挥砍。";
        }

        private void TogglePause()
        {
            if (ended) return;
            paused = !paused;
            Time.timeScale = paused ? 0f : hitStopUntil > Time.unscaledTime ? .08f : 1f;
            if (statusText != null)
                statusText.text = paused ? "暂 停" : "好 戏 开 演    ·    牵 线 为 刃";
        }

        private void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

    }
}
