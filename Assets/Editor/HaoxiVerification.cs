using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HaoxiKaiyan.Editor
{
    /// <summary>Runs the existing multi-frame probes in a closed-editor batch session.</summary>
    [InitializeOnLoad]
    public static class HaoxiVerification
    {
        private const string ActiveKey = "Haoxi.BatchVerification.Active";
        private const string MarkerKey = "Haoxi.BatchVerification.Marker";
        private const string StartedKey = "Haoxi.BatchVerification.Started";
        private const string ResultKey = "Haoxi.BatchVerification.Result";

        static HaoxiVerification()
        {
            Application.logMessageReceived += Observe;
            EditorApplication.update += CheckTimeout;
        }

        public static void RunGameplayBatch() => Begin("HAOXI_SMOKE_", HaoxiGameplayProbeMenu.Run);
        public static void RunMotionBatch() => Begin("HAOXI_MOTION_", HaoxiGameplayProbeMenu.RunMotion);

        private static void Begin(string marker, Action start)
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Use the editor menus for interactive verification.");
            EditorSceneManager.OpenScene("Assets/Scenes/HaoxiArena.unity");
            SessionState.SetString(MarkerKey, marker);
            SessionState.SetFloat(StartedKey, (float)EditorApplication.timeSinceStartup);
            SessionState.SetInt(ResultKey, -1);
            SessionState.SetBool(ActiveKey, true);
            start();
        }

        private static void Observe(string message, string stack, LogType type)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (type == LogType.Exception || type == LogType.Error)
                SessionState.SetInt(ResultKey, 1);
            string marker = SessionState.GetString(MarkerKey, "");
            if (marker.Length == 0 || !message.StartsWith(marker, StringComparison.Ordinal)) return;
            int code = message.StartsWith(marker + "PASS", StringComparison.Ordinal) &&
                SessionState.GetInt(ResultKey, -1) != 1 ? 0 : 1;
            SessionState.SetInt(ResultKey, code);
            // Wait until the probe has printed its detail line and restored scene state.
            EditorApplication.delayCall += Complete;
        }

        private static void CheckTimeout()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(StartedKey, 0f) < 180f) return;
            SessionState.SetInt(ResultKey, 1);
            Debug.LogError("HAOXI_VERIFICATION_TIMEOUT: probe did not finish within 180 seconds.");
            Complete();
        }

        private static void Complete()
        {
            int result = SessionState.GetInt(ResultKey, 1);
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.Exit(result);
        }
    }
}
