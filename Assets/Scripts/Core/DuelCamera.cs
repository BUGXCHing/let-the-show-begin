using UnityEngine;
using System.Collections.Generic;

namespace HaoxiKaiyan
{
    // Keep the joint dance close while both combatants and their weapons remain readable.
    [RequireComponent(typeof(Camera))]
    public sealed class DuelCamera : MonoBehaviour
    {
        private MarionetteController player, opponent;
        private IReadOnlyList<MarionetteController> opponents;
        private Camera view;
        private Vector3 focus, focusVelocity;
        private float zoomVelocity, impact;
        private readonly Vector3 viewDirection = new Vector3(0f, -.67f, .74f).normalized;

        public void Configure(MarionetteController actor, MarionetteController enemy)
        {
            player = actor; opponent = enemy;
            view = GetComponent<Camera>();
            view.orthographic = true;
            view.orthographicSize = 3.15f;
            transform.rotation = Quaternion.LookRotation(viewDirection, Vector3.up);
            focus = actor.transform.position * .56f + enemy.transform.position * .44f + Vector3.up * 1.18f;
            transform.position = focus - viewDirection * 10f;
        }

        public void SetOpponents(IReadOnlyList<MarionetteController> actors) => opponents = actors;

        public void Kick(HitTier tier)
        {
            impact = Mathf.Max(impact, tier == HitTier.Light ? .025f : tier == HitTier.Heavy ? .075f : .13f);
        }

        private void LateUpdate()
        {
            if (player == null || view == null) return;
            float dt = Time.unscaledDeltaTime;
            if (opponents != null)
            {
                float nearest = float.MaxValue;
                opponent = null;
                foreach (MarionetteController actor in opponents)
                {
                    if (actor == null || !actor.IsEnemy || actor.IsDefeated) continue;
                    float gap = (actor.transform.position - player.transform.position).sqrMagnitude;
                    if (gap < nearest) { nearest = gap; opponent = actor; }
                }
            }
            Vector3 a = player.transform.position, b = opponent != null ? opponent.transform.position : a;
            Vector3 aim = a * .56f + b * .44f + Vector3.up * 1.18f;
            aim += Vector3.ClampMagnitude(player.Body.linearVelocity, .9f) * .1f;
            // A weapon may be dropped or destroyed during a duel. Keep the camera
            // following the dancer, not a stale prop reference.
            if (player.WeaponTransform != null)
                aim += (player.WeaponTransform.position - a) * .055f;
            focus = Vector3.SmoothDamp(focus, aim, ref focusVelocity, .22f, 16f, dt);
            float width = Mathf.Max(1f, view.aspect);
            float horizontal = Mathf.Abs(a.x - b.x) / (2f * width) + 1.8f;
            float depth = Mathf.Abs(a.z - b.z) * .38f + 2.85f;
            float targetSize = Mathf.Clamp(Mathf.Max(3.05f, horizontal, depth), 3.05f, 6.2f);
            view.orthographicSize = Mathf.SmoothDamp(view.orthographicSize, targetSize + impact * .42f, ref zoomVelocity, .30f, 9f, dt);
            impact = Mathf.MoveTowards(impact, 0f, dt * .50f);
            Vector3 tinyKick = transform.right * Mathf.Sin(Time.unscaledTime * 73f) * impact * .16f;
            transform.position = focus - viewDirection * 10f + tinyKick;
        }
    }
}
