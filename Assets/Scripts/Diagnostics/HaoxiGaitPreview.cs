#if UNITY_EDITOR
using System.Collections;
using HaoxiKaiyan;
using UnityEngine;

// Editor-only choreography loop for visual inspection, not a gameplay animation.
public sealed class HaoxiGaitPreview : MonoBehaviour
{
    private IEnumerator Start()
    {
        yield return null;
        MarionetteController player = null, enemy = null;
        foreach (var actor in FindObjectsByType<MarionetteController>())
        {
            if (actor.IsEnemy) enemy = actor;
            else player = actor;
        }
        var director = FindAnyObjectByType<HaoxiGameDirector>();
        if (player == null || enemy == null || director == null)
        {
            Debug.LogError("HAOXI_GAIT_PREVIEW_FAIL: 缺少场景角色。");
            Destroy(gameObject);
            yield break;
        }
        director.enabled = false;
        Time.timeScale = 1f;
        player.ResetActor(new Vector3(-1.8f, 0f, -.8f), Quaternion.Euler(0f, 90f, 0f));
        enemy.ResetActor(new Vector3(1.8f, 0f, 1.8f), Quaternion.Euler(0f, -90f, 0f));
        enemy.SetEnemyMovement(Vector3.zero, false);
        // Repeated reversals show whether the body leads the feet or the support
        // foot catches the weight. The right stick stays neutral for this pass.
        for (int i = 0; i < 480; i++)
        {
            int phase = i % 80;
            player.SetControls(phase < 40 ? Vector2.right : Vector2.left, Vector2.zero);
            yield return new WaitForFixedUpdate();
        }
        player.SetControls(Vector2.zero, Vector2.zero);
        director.enabled = true;
        Debug.Log("HAOXI_GAIT_PREVIEW_DONE: 八秒往返走位结束，可重启继续手动试玩。");
        Destroy(gameObject);
    }
}
#endif
