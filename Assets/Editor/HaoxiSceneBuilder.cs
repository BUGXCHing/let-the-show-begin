using System.Collections.Generic;
using System.IO;
using System.Linq;
using HaoxiKaiyan;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HaoxiKaiyan.Editor
{
    public static class HaoxiSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/HaoxiArena.unity";

        [MenuItem("好戏开演/生成可玩场景")]
        public static void CreatePlayableScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                bool replace = EditorUtility.DisplayDialog("场景已存在", "重新生成会替换当前原型场景文件。继续吗？", "重新生成", "取消");
                if (!replace) return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("好戏开演 - Runtime-built prototype");
            root.AddComponent<HaoxiGameDirector>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            EditorSceneManager.SetActiveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("好戏开演：已生成可玩场景 Assets/Scenes/HaoxiArena.unity。点击 Play 进入原型。");
        }

        [MenuItem("好戏开演/构建 WebGL试玩包")]
        public static void BuildWebGl() => BuildWebGlInternal(true);

        // Batch mode is used after closing the interactive editor; there is no dialog UI.
        public static void BuildWebGlBatch() => BuildWebGlInternal(false);

        // Opt-in recovery for stale native/Burst artifacts; normal builds stay incremental.
        public static void BuildWebGlCleanBatch() => BuildWebGlInternal(false, true);

        private static void BuildWebGlInternal(bool askBeforeReplace, bool cleanCache = false)
        {
            const string output = "Build/WebGL";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new System.InvalidOperationException("找不到可玩场景：" + ScenePath);

            if (askBeforeReplace && System.IO.Directory.Exists(output) &&
                !EditorUtility.DisplayDialog("WebGL 输出已存在", "构建将更新 Build/WebGL 中的原型文件。", "继续构建", "取消"))
                return;

            EnsureRuntimeLitMaterial();
            EnsureTrailMaterial();

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = cleanCache ? BuildOptions.CleanBuildCache : BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                MakeWebPreviewResponsive(output);
                Debug.Log($"WebGL试玩包构建完成：{output}（{report.summary.totalSize / (1024 * 1024)} MB）");
            }
            else
                throw new System.InvalidOperationException($"WebGL构建未完成：{report.summary.result}，详情见 Console / 批处理日志。");
        }

        private static void EnsureRuntimeLitMaterial()
        {
            const string assetPath = "Assets/Resources/HaoxiRuntimeLit.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(assetPath) != null)
                return;

            System.IO.Directory.CreateDirectory("Assets/Resources");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new System.InvalidOperationException("项目中找不到 Universal Render Pipeline/Lit Shader。");

            var material = new Material(shader) { name = "Haoxi Runtime Lit" };
            AssetDatabase.CreateAsset(material, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void EnsureTrailMaterial()
        {
            const string path = "Assets/Resources/HaoxiRuntimeTrail.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) throw new System.InvalidOperationException("找不到 URP 粒子材质 Shader，刀轨无法构建。");
            Material material = new Material(shader) { name = "Haoxi soft blade arc" };
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();
        }

        private static void MakeWebPreviewResponsive(string output)
        {
            string index = Path.Combine(output, "index.html");
            string html = File.ReadAllText(index);
            // Unity's stock desktop template still creates a 960x600 canvas; override it in
            // the repeatable build step so narrow desktop and tablet viewports show the HUD.
            const string style = "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1,maximum-scale=1,user-scalable=no\">" +
                "<style>html,body{margin:0;width:100%;height:100%;overflow:hidden;background:#e9e6dc;}" +
                "#unity-container,#unity-fullscreen-container,#unity-canvas{position:fixed!important;inset:0!important;" +
                "width:100vw!important;height:100dvh!important;max-width:100vw!important;max-height:100dvh!important;" +
                "transform:none!important;}#unity-footer{display:none!important;}</style>";
            if (!html.Contains("#unity-footer{display:none!important;}"))
                File.WriteAllText(index, html.Replace("<head>", "<head>" + style));
        }
    }
}
