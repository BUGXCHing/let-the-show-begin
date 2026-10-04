using HaoxiKaiyan;
using UnityEditor;
using UnityEngine;

public static class HaoxiGameplayProbeMenu
{
    [InitializeOnLoadMethod]
    private static void RegisterMotionProbe()
    {
        EditorApplication.playModeStateChanged -= OnMotionPlay;
        EditorApplication.playModeStateChanged += OnMotionPlay;
    }

    private static void OnMotionPlay(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("HaoxiGameplayProbe", false))
        {
            SessionState.SetBool("HaoxiGameplayProbe", false);
            new GameObject("Gameplay smoke probe").AddComponent<HaoxiGameplayProbe>();
        }
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("HaoxiMotionProbe", false)) return;
        SessionState.SetBool("HaoxiMotionProbe", false);
        new GameObject("Motion stability probe").AddComponent<HaoxiMotionProbe>();
    }

    [MenuItem("好戏开演/动作系统自检")]
    public static void RunMotion()
    {
        if (EditorApplication.isPlaying)
            new GameObject("Motion stability probe").AddComponent<HaoxiMotionProbe>();
        else
        {
            SessionState.SetBool("HaoxiMotionProbe", true);
            EditorApplication.isPlaying = true;
        }
    }

    [MenuItem("好戏开演/运行玩法自检 _F6")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying)
        {
            SessionState.SetBool("HaoxiGameplayProbe", true);
            EditorApplication.isPlaying = true;
            return;
        }
        new GameObject("Gameplay smoke probe").AddComponent<HaoxiGameplayProbe>();
    }

    [MenuItem("好戏开演/预览走位与脚步")]
    public static void PreviewGait()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("先进入 Play 模式，再预览脚步。");
            return;
        }
        new GameObject("Gait preview probe").AddComponent<HaoxiGaitPreview>();
    }
}
