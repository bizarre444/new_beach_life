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

        /// <summary>Tears the current game down and starts a fresh resort with a new seed.</summary>
        public static void Restart()
        {
            var existing = FindObjectOfType<ResortRunner>();
            if (existing != null) Destroy(existing.transform.root.gameObject);
            Create(48, 36, Random.Range(1, int.MaxValue));
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

            var camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.78f, 0.92f);
            camera.farClipPlane = 500f;
            var rig = camera.gameObject.GetComponent<CameraRig>() ?? camera.gameObject.AddComponent<CameraRig>();
            rig.LookAt(new Vector3(width / 2f, 0f, height / 2f), 34f);

            if (FindObjectOfType<Light>() == null)
            {
                var sun = new GameObject("Sun");
                var light = sun.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.05f;
                light.color = new Color(1f, 0.97f, 0.88f);
                sun.transform.rotation = Quaternion.Euler(52f, -30f, 0f);
            }

            RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.6f);
            return runner;
        }
    }
}
