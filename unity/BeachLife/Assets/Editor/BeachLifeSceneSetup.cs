using BeachLife.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BeachLife.EditorTools
{
    /// <summary>Editor helper that writes the playable scene so it can be added to the build settings.</summary>
    public static class BeachLifeSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Resort.unity";

        [MenuItem("BeachLife/Create Resort Scene")]
        public static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var bootstrap = new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            bootstrap.MapWidth = 48;
            bootstrap.MapHeight = 36;

            var camera = Camera.main;
            if (camera != null && camera.GetComponent<CameraRig>() == null)
                camera.gameObject.AddComponent<CameraRig>();

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"BeachLife: created {ScenePath} and set it as the first build scene.");
        }
    }
}
