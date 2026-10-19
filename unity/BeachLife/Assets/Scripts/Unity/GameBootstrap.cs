using UnityEngine;

namespace BeachLife.Unity
{
    /// <summary>
    /// Creates the whole game object graph at runtime, so pressing Play in an empty scene is enough.
    /// Drop this component into a scene to control the plot size from the inspector instead.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        public int MapWidth = 48;
        public int MapHeight = 36;
        public int Seed = 12345;

        private void Awake() => Create(MapWidth, MapHeight, Seed);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindObjectOfType<GameBootstrap>() != null) return;
            if (FindObjectOfType<ResortRunner>() != null) return;
            Create(48, 36, 12345);
        }

        public static ResortRunner Create(int width, int height, int seed)
        {
            var existing = FindObjectOfType<ResortRunner>();
            if (existing != null) return existing;

            var root = new GameObject("Resort");
            var runner = root.AddComponent<ResortRunner>();
            runner.MapWidth = width;
            runner.MapHeight = height;
            runner.Seed = seed;
            root.AddComponent<WorldView>();
            root.AddComponent<BuildController>();
            root.AddComponent<HudGui>();
            root.AddComponent<WeatherFx>();
            root.AddComponent<MainMenu>();

            var camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color(0.55f, 0.78f, 0.92f);
            camera.farClipPlane = 500f;
            var rig = camera.gameObject.GetComponent<CameraRig>() ?? camera.gameObject.AddComponent<CameraRig>();
            rig.LookAt(new Vector3(width / 2f, 0f, height / 2f), 34f);

            // Gradient skybox quad behind the map.
            CreateSkybox(width, height);

            // Hemisphere light for soft sky/ground fill.
            var hemi = new GameObject("Hemisphere Light");
            var hemiLight = hemi.AddComponent<Light>();
            hemiLight.type = LightType.Directional;
            hemiLight.color = new Color(0.6f, 0.72f, 0.85f); // sky tint
            hemiLight.intensity = 0.4f;
            hemiLight.enabled = true;
            hemiLight.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // point down for ambient fill

            // Shadow-casting sun.
            var sun = new GameObject("Sun");
            var sunLight = sun.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.intensity = 1.15f;
            sunLight.color = new Color(1f, 0.96f, 0.85f);
            sunLight.shadows = LightShadows.Soft;
            sunLight.transform.rotation = Quaternion.Euler(52f, -30f, 0f);
            sunLight.cullingMask = -1; // all layers

            RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.6f);
            return runner;
        }

        private static void CreateSkybox(int width, int height)
        {
            var sky = new GameObject("Skybox");
            var skyMesh = sky.AddComponent<MeshFilter>();
            var skyRenderer = sky.AddComponent<MeshRenderer>();

            // Large quad behind the map.
            var skyWidth = Mathf.Max(width, height) * 4f;
            var skyHeight = 60f;
            var skyPos = new Vector3(width / 2f, skyHeight / 2f - 5f, height / 2f + 80f);
            sky.transform.position = skyPos;
            sky.transform.rotation = Quaternion.identity;

            var quad = new Mesh { name = "SkyQuad" };
            quad.SetVertices(new[]
            {
                new Vector3(-skyWidth / 2f, 0f, 0f),
                new Vector3(skyWidth / 2f, 0f, 0f),
                new Vector3(skyWidth / 2f, skyHeight, 0f),
                new Vector3(-skyWidth / 2f, skyHeight, 0f),
            });
            quad.SetUVs(0, new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
            });
            quad.SetIndices(new[] { 0, 1, 2, 0, 2, 3 }, MeshTopology.Triangles, 0);
            quad.UploadMeshData(false);

            skyMesh.mesh = quad;

            // Gradient sky material using a built-in shader.
            var skyShader = Shader.Find("Sprites/Default") ?? Shader.Find("Legacy Shaders/Diffuse");
            var skyMat = new Material(skyShader ?? Shader.Find("Legacy Shaders/Transparent/Diffuse"));

            // Procedural gradient texture (2x64: top = deep blue, bottom = light blue).
            var tex = new Texture2D(2, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
            {
                float t = y / 63f;
                Color c = Color.Lerp(new Color(0.45f, 0.65f, 0.88f), new Color(0.72f, 0.85f, 0.95f), t);
                tex.SetPixel(0, y, c);
                tex.SetPixel(1, y, c);
            }
            tex.Apply();
            skyMat.mainTexture = tex;
            skyMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Background;
            skyRenderer.sharedMaterial = skyMat;
        }

    }
}
