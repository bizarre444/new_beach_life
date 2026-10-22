#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BeachLife.EditorTools
{
    /// <summary>
    /// Batch-mode Windows player build: recreates the resort scene, builds the exe into its own
    /// numbered folder (build/Windows_v001, _v002, ... so previous builds are kept) and exits
    /// with a non-zero code on failure. The build number lives in build/version.txt and is
    /// incremented on every successful build.
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

                string buildRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../../build"));
                string versionFile = Path.Combine(buildRoot, "version.txt");
                int version = 1;
                if (File.Exists(versionFile) && int.TryParse(File.ReadAllText(versionFile).Trim(), out int stored))
                    version = stored + 1;
                File.WriteAllText(versionFile, version.ToString());

                string exe = Environment.GetEnvironmentVariable("BEACHLIFE_BUILD_TARGET");
                if (string.IsNullOrEmpty(exe))
                    exe = Path.Combine(buildRoot, $"Windows_v{version:000}", "BeachLife.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(exe));

                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Scenes/Resort.unity" },
                    locationPathName = exe,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                });

                Debug.Log($"BeachLife build v{version}: {report.summary.result}, size {report.summary.totalSize:n0} bytes, " +
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
