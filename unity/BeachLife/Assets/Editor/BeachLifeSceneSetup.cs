using BeachLife.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BeachLife.EditorTools
{
    /// <summary>
    /// Editor helper that writes the playable scene so it can be added to the build settings.
    /// The scene contains the whole uGUI hierarchy (HUD + main menu), so buttons, panels and
    /// labels can be restyled directly in the editor; at runtime the components find those
    /// nodes by name and only bind behaviour.
    /// </summary>
    public static class BeachLifeSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Resort.unity";

        [MenuItem("BeachLife/Create Resort Scene")]
        public static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var camera = Camera.main;
            if (camera != null && camera.GetComponent<CameraRig>() == null)
                camera.gameObject.AddComponent<CameraRig>();
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.backgroundColor = new Color(0.47f, 0.72f, 0.9f);
                camera.farClipPlane = 500f;
                var rig = camera.GetComponent<CameraRig>();
                rig.LookAt(new Vector3(20f, 0f, 20f), 30f);
            }

            // Resort root with every gameplay component; Awake/Start run on play and create
            // the simulation, while the UI nodes saved below are reused instead of rebuilt.
            var root = new GameObject("Resort");
            root.AddComponent<ResortRunner>();
            root.AddComponent<WorldView>();
            root.AddComponent<BuildController>();
            root.AddComponent<WeatherFx>();
            var hud = root.AddComponent<HudGui>();
            var menu = root.AddComponent<MainMenu>();

            // Build the named UI trees in edit mode so they get saved with the scene.
            hud.EnsureUI();
            menu.EnsureUI();
            menu.gameObject.SetActive(true);

            // Lights, mirroring the runtime bootstrap.
            var hemi = new GameObject("Hemisphere Light");
            var hemiLight = hemi.AddComponent<Light>();
            hemiLight.type = LightType.Directional;
            hemiLight.color = new Color(0.6f, 0.72f, 0.85f);
            hemiLight.intensity = 0.3f;
            hemiLight.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var sun = new GameObject("Sun");
            var sunLight = sun.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.intensity = 0.9f;
            sunLight.color = new Color(1f, 0.96f, 0.85f);
            sunLight.shadows = LightShadows.Soft;
            sunLight.transform.rotation = Quaternion.Euler(52f, -30f, 0f);

            RenderSettings.ambientLight = new Color(0.4f, 0.42f, 0.46f);

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"BeachLife: created {ScenePath} with the editable UI hierarchy and set it as the first build scene.");
        }
    }
}
