#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BeachLife.EditorTools
{
    /// <summary>
    /// Batch-mode Windows player build: recreates the resort scene, builds the exe and
    /// exits with a non-zero code on failure.
    /// Run with: Unity.exe -batchmode -projectPath ... -executeMethod BeachLife.EditorTools.BuildWindowsPlayer.Run
    /// Set BEACHLIFE_BUILD_TARGET to override the output path.
    /// </summary>
    public static class BuildWindowsPlayer
    {
        public static void Run()
        {
            string failure = null;
            try
            {
                BeachLifeSceneSetup.CreateScene();

                string exe = Environment.GetEnvironmentVariable("BEACHLIFE_BUILD_TARGET");
                if (string.IsNullOrEmpty(exe))
                    exe = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../../build/Windows/BeachLife.exe"));
                Directory.CreateDirectory(Path.GetDirectoryName(exe));

                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Scenes/Resort.unity" },
                    locationPathName = exe,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                });

                Debug.Log($"BeachLife build: {report.summary.result}, size {report.summary.totalSize:n0} bytes, " +
                          $"errors {report.summary.totalErrors}, output {exe}");
                if (report.summary.result != BuildResult.Succeeded || report.summary.totalErrors > 0)
                    failure = $"build result {report.summary.result} with {report.summary.totalErrors} errors";
            }
            catch (Exception e)
            {
                failure = e.ToString();
            }

            if (failure != null)
            {
                Debug.LogError("BeachLife player build FAILED: " + failure);
                EditorApplication.Exit(1);
                return;
            }
            EditorApplication.Exit(0);
        }
    }
}
#endif
