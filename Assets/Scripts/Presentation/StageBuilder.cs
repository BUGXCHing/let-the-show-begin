using UnityEngine;
using static HaoxiKaiyan.StagePalette;

namespace HaoxiKaiyan
{
    internal static class StagePalette
    {
        internal static readonly Color Ink = new Color(0.11f, 0.13f, 0.13f);
        internal static readonly Color Paper = new Color(0.88f, 0.86f, 0.79f);
        internal static readonly Color Vermilion = new Color(0.62f, 0.18f, 0.12f);

    }

    /// <summary>Procedural stage, lighting and camera. Contains no combat decisions.</summary>
    internal static class StageBuilder
    {
        internal static Camera Build(Transform parent)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.61f, 0.57f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.79f, 0.78f, 0.73f);
            RenderSettings.fogDensity = 0.018f;

            Material paper = CreateMaterial(Paper, 0f, 0.1f);
            Material ink = CreateMaterial(new Color(0.22f, 0.25f, 0.24f), 0f, 0.12f);
            Material stone = CreateMaterial(new Color(0.56f, 0.56f, 0.52f), 0f, 0.15f);
            Material red = CreateMaterial(Vermilion, 0f, 0.18f);

            MakePrimitive("Round ink-stage plinth", PrimitiveType.Cylinder, parent,
                new Vector3(0, -0.27f, 0), new Vector3(20.2f, 0.26f, 20.2f), ink);
            MakePrimitive("Warm paper arena", PrimitiveType.Cube, parent,
                new Vector3(0, -0.08f, 0), new Vector3(19.45f, 0.16f, 19.45f), paper);
            MakePrimitive("Stage edge - north", PrimitiveType.Cube, parent,
                new Vector3(0, 0.02f, 9.55f), new Vector3(19.2f, 0.08f, 0.055f), red);
            MakePrimitive("Stage edge - south", PrimitiveType.Cube, parent,
                new Vector3(0, 0.02f, -9.55f), new Vector3(19.2f, 0.08f, 0.055f), red);
            MakePrimitive("Stage edge - east", PrimitiveType.Cube, parent,
                new Vector3(9.55f, 0.02f, 0), new Vector3(0.055f, 0.08f, 19.2f), red);
            MakePrimitive("Stage edge - west", PrimitiveType.Cube, parent,
                new Vector3(-9.55f, 0.02f, 0), new Vector3(0.055f, 0.08f, 19.2f), red);
            AddArenaBoundary(parent, "Hidden arena boundary east", new Vector3(9.58f, 0.9f, 0), new Vector3(0.16f, 1.8f, 19.1f));
            AddArenaBoundary(parent, "Hidden arena boundary west", new Vector3(-9.58f, 0.9f, 0), new Vector3(0.16f, 1.8f, 19.1f));
            AddArenaBoundary(parent, "Hidden arena boundary north", new Vector3(0, 0.9f, 9.58f), new Vector3(19.1f, 1.8f, 0.16f));
            AddArenaBoundary(parent, "Hidden arena boundary south", new Vector3(0, 0.9f, -9.58f), new Vector3(19.1f, 1.8f, 0.16f));

            // Ink-wash silhouettes and lantern-like stage markers; decorative shapes stay outside the fight.
            for (int i = 0; i < 7; i++)
            {
                float x = -8.8f + i * 2.92f;
                float height = 0.32f + (i % 3) * 0.12f;
                MakePrimitive("Distant ink ridge", PrimitiveType.Sphere, parent,
                    new Vector3(x, 0.12f, 8.92f + (i % 2) * 0.10f), new Vector3(2.5f, height, 0.23f), stone);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    MakePrimitive("Stage lantern stand", PrimitiveType.Cylinder, parent,
                        new Vector3(side * 8.72f, 0.34f, z * 8.72f), new Vector3(0.12f, 0.34f, 0.12f), ink);
                    MakePrimitive("Vermilion lantern", PrimitiveType.Sphere, parent,
                        new Vector3(side * 8.72f, 0.82f, z * 8.72f), new Vector3(0.25f, 0.34f, 0.25f), red);
                }
            }

            var key = new GameObject("Soft stage key light").AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.15f;
            key.color = new Color(1f, 0.91f, 0.77f);
            key.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            key.shadows = LightShadows.Soft;
            var fill = new GameObject("Cool stage fill").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.48f;
            fill.color = new Color(0.73f, 0.81f, 0.83f);
            fill.transform.rotation = Quaternion.Euler(25f, 145f, 0f);

            Camera stageCamera = new GameObject("Following stage camera").AddComponent<Camera>();
            stageCamera.tag = "MainCamera";
            stageCamera.clearFlags = CameraClearFlags.SolidColor;
            stageCamera.backgroundColor = new Color(0.76f, 0.76f, 0.72f);
            stageCamera.nearClipPlane = 0.1f;
            stageCamera.farClipPlane = 80f;
            stageCamera.gameObject.AddComponent<AudioListener>();
            stageCamera.gameObject.AddComponent<ImpactAudio>();
            stageCamera.gameObject.AddComponent<DuelCamera>();
            return stageCamera;
        }

        private static GameObject MakePrimitive(string name, PrimitiveType type, Transform parent,
            Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            // Only the arena cubes should participate in collisions; decoration must never
            // become an invisible second body when characters move or turn.
            if (type != PrimitiveType.Cube)
                RemoveDecorationColliders(go);
            Renderer renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            return go;
        }

        private static void RemoveDecorationColliders(GameObject go)
        {
            SphereCollider sphere = go.GetComponent<SphereCollider>();
            if (sphere != null) { sphere.enabled = false; Object.Destroy(sphere); }
            CapsuleCollider capsule = go.GetComponent<CapsuleCollider>();
            if (capsule != null) { capsule.enabled = false; Object.Destroy(capsule); }
            BoxCollider box = go.GetComponent<BoxCollider>();
            if (box != null) { box.enabled = false; Object.Destroy(box); }
            MeshCollider mesh = go.GetComponent<MeshCollider>();
            if (mesh != null) { mesh.enabled = false; Object.Destroy(mesh); }
        }

        private static void AddArenaBoundary(Transform parent, string name, Vector3 position, Vector3 size)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = position;
            wall.transform.localScale = size;
            Renderer renderer = wall.GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = false;
        }

        internal static Material CreateMaterial(Color color, float metallic, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material mat = new Material(shader) { color = color };
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }
    }
}
