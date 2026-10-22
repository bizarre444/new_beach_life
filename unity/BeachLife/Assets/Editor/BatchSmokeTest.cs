#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using BeachLife.Core;
using BeachLife.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BeachLife.EditorTools
{
    /// <summary>
    /// Headless end-to-end check for CI / batch runs: enters play mode (no domain reload),
    /// lets the game bootstrap itself, builds a resort, simulates three days, round-trips a
    /// save through the real JSON serializer, renders a screenshot and writes a JSON report.
    /// Run with: Unity.exe -batchmode -projectPath ... -executeMethod BeachLife.EditorTools.BatchSmokeTest.Run
    /// </summary>
    public static class BatchSmokeTest
    {
        private const int WarmupUpdates = 30;

        private static readonly List<string> Errors = new List<string>();
        private static SmokeReport _report;
        private static int _updatesSeen;

        public static void Run()
        {
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;

            // Prefer the saved scene so the editable-UI path (Resort.unity) is what gets tested;
            // fall back to an empty scene where the bootstrap creates everything at runtime.
            const string scenePath = "Assets/Scenes/Resort.unity";
            if (System.IO.File.Exists(scenePath))
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            EditorApplication.update += WaitForPlayMode;
            EditorApplication.isPlaying = true;
        }

        [Serializable]
        private class SmokeReport
        {
            public int BuildingsPlaced;
            public List<string> BuildFailures = new List<string>();
            public int VisitorsAfter3Days;
            public int CashAfter3Days;
            public float ReputationAfter3Days;
            public bool SnapshotRoundTripOk;
            public string Screenshot = "";
            public List<string> Errors = new List<string>();
            public List<string> UiDiag = new List<string>();
        }

        private static void CollectUiDiagnostics()
        {
            var hud = UnityEngine.Object.FindObjectOfType<HudGui>();
            var menu = UnityEngine.Object.FindObjectOfType<MainMenu>();
            var cam = Camera.main;
            _report.UiDiag.Add($"camera={(cam != null ? cam.name : "NULL")} pos={(cam != null ? cam.transform.position.ToString("F1") : "-")}");
            if (menu != null) _report.UiDiag.Add($"menu.Visible={MainMenu.Visible}");
            if (hud == null) { _report.UiDiag.Add("HUD=NULL"); return; }

            var canvas = hud.transform.Find("UICanvas")?.GetComponent<Canvas>();
            if (canvas == null) { _report.UiDiag.Add("UICanvas=MISSING"); return; }
            _report.UiDiag.Add($"UICanvas activeInHierarchy={canvas.gameObject.activeInHierarchy} " +
                               $"enabled={canvas.enabled} renderMode={canvas.renderMode} " +
                               $"cam={(canvas.worldCamera != null ? canvas.worldCamera.name : "NULL")} " +
                               $"sort={canvas.sortingOrder} planeDist={canvas.planeDistance}");

            foreach (var panelName in new[] { "TopBar", "PalettePanel", "InspectorPanel", "LogPanel" })
            {
                var panel = canvas.transform.Find(panelName) as RectTransform;
                if (panel == null) { _report.UiDiag.Add($"{panelName}=MISSING"); continue; }
                var rect = panel.rect;
                _report.UiDiag.Add($"{panelName}: active={panel.gameObject.activeInHierarchy} children={panel.childCount} " +
                                   $"rect(w={rect.width:F0} h={rect.height:F0} x={rect.x:F0} y={rect.y:F0})");
            }

            var cashText = canvas.transform.Find("TopBar/CashText")?.GetComponent<UnityEngine.UI.Text>();
            _report.UiDiag.Add($"CashText={(cashText != null ? $"'{cashText.text}'" : "MISSING")}");

            var paletteRoot = canvas.transform.Find("PalettePanel/BuildingScroll") as RectTransform;
            var paletteContent = canvas.transform.Find("PalettePanel/BuildingScroll/Viewport/Content") as RectTransform;
            _report.UiDiag.Add($"PaletteScroll: rootChildren={(paletteRoot != null ? paletteRoot.childCount.ToString() : "MISS")} " +
                               $"contentChildren={(paletteContent != null ? paletteContent.childCount.ToString() : "MISS")}");

            var menuBtn = canvas.transform.Find("TopBar/MenuBtn") as RectTransform;
            _report.UiDiag.Add($"MenuBtn={(menuBtn != null ? $"pos=({menuBtn.position.x:F0},{menuBtn.position.y:F0}) size=({menuBtn.rect.width:F0}x{menuBtn.rect.height:F0})" : "MISSING")}");
        }

        private static void WaitForPlayMode()
        {
            if (!EditorApplication.isPlaying) return;
            if (ResortRunner.Instance == null || ResortRunner.Instance.Resort == null) return;

            EditorApplication.update -= WaitForPlayMode;
            _report = new SmokeReport();
            EditorApplication.update += CaptureMenuThenSimulate;
        }

        /// <summary>Waits a few frames so the world and menu are rendered before the first shot.</summary>
        private static void CaptureMenuThenSimulate()
        {
            _updatesSeen++;
            if (_updatesSeen < WarmupUpdates) return;
            EditorApplication.update -= CaptureMenuThenSimulate;
            CaptureScreenshot("../../build/beachlife_menu.png"); // menu is up at this point
            Simulate();
            EditorApplication.update += CollectAndFinish;
        }

        private static void Simulate()
        {
            // Start through the main menu exactly like a player picking a level.
            MainMenu.StartNewGame(Difficulty.Relaxed);
            var resort = ResortRunner.Instance.Resort;
            if (MainMenu.Visible) Errors.Add("Menu still visible after starting a game");

            PlaceAll(resort);

            var cleaner = resort.Hire(StaffType.Cleaner);
            var attendant = resort.Hire(StaffType.Attendant);
            if (cleaner == null || attendant == null) Errors.Add("Hire returned null");
            if (attendant != null && !resort.AssignAttendant(attendant.Id, FindId(resort, "beach_bar")))
                Errors.Add("Attendant assignment failed");
            resort.SetCampaign(CampaignLevel.Regional);

            // Three in-game days, in steps that keep pathing cheap.
            int guard = 0;
            while (resort.Clock.Day < 4 && guard++ < 5000) resort.Tick(10f);

            _report.VisitorsAfter3Days = resort.TotalVisitors;
            _report.CashAfter3Days = resort.Economy.Cash;
            _report.ReputationAfter3Days = resort.Reputation;

            if (resort.TotalVisitors < 20) Errors.Add($"Too few visitors: {resort.TotalVisitors}");

            var json = JsonUtility.ToJson(resort.CaptureSnapshot());
            var restored = JsonUtility.FromJson<ResortSnapshot>(json);
            var copy = new Resort(restored.Map.Width, restored.Map.Height, 1);
            copy.ApplySnapshot(restored);
            _report.SnapshotRoundTripOk =
                copy.Economy.Cash == resort.Economy.Cash
                && copy.Buildings.Count == resort.Buildings.Count
                && copy.Tourists.Count == resort.Tourists.Count
                && Math.Abs(copy.Reputation - resort.Reputation) < 0.001f
                && copy.Clock.Day == resort.Clock.Day;
            if (!_report.SnapshotRoundTripOk)
                Errors.Add($"Snapshot mismatch: cash {copy.Economy.Cash}/{resort.Economy.Cash}, "
                           + $"buildings {copy.Buildings.Count}/{resort.Buildings.Count}, "
                           + $"tourists {copy.Tourists.Count}/{resort.Tourists.Count}");
        }

        private static void CollectAndFinish()
        {
            _updatesSeen++;
            if (_updatesSeen < WarmupUpdates) return; // let WorldView sync the new objects

            EditorApplication.update -= CollectAndFinish;
            try
            {
                CollectUiDiagnostics();
                _report.Screenshot = CaptureScreenshot("../../build/beachlife_smoke.png");
            }
            catch (Exception e)
            {
                Errors.Add("Screenshot failed: " + e.Message);
            }

            _report.Errors.AddRange(Errors);
            foreach (var error in Errors) Debug.LogError("BeachLifeSmoke: " + error);

            var reportPath = Environment.GetEnvironmentVariable("BEACHLIFE_REPORT");
            if (string.IsNullOrEmpty(reportPath)) reportPath = Path.GetFullPath("../../build/beachlife_report.json");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(_report, true));
            Debug.Log($"BeachLife smoke test report: {reportPath}");

            EditorApplication.isPlaying = false;
            EditorApplication.Exit(Errors.Count > 0 ? 1 : 0);
        }

        private static void PlaceAll(Resort resort)
        {
            // Layout tuned for the 40x40 plot (original Beach Life grid size).
            Place(resort, "beach_bar", 20, 20);
            Place(resort, "toilets", 26, 20);
            Place(resort, "restaurant", 12, 20);
            Place(resort, "pool", 30, 21);
            Place(resort, "hotel_block", 6, 20);
            Place(resort, "gift_shop", 5, 24);
            Place(resort, "fountain", 34, 19);
            Place(resort, "ice_cream_stand", 28, 19);
            for (int i = 0; i < 6; i++) Place(resort, "sun_lounger", 14 + i * 2, 6);
            Place(resort, "parasol", 15, 8);
            Place(resort, "parasol", 21, 8);
            Place(resort, "volleyball_court", 32, 6);
            Place(resort, "lifeguard_tower", 24, 5);
            Place(resort, "palm_tree", 10, 10);
            Place(resort, "palm_tree", 27, 10);
            Place(resort, "litter_bin", 18, 17);
            Place(resort, "litter_bin", 24, 17);
            Place(resort, "litter_bin", 30, 17);
        }

        private static void Place(Resort resort, string defId, int x, int y)
        {
            var result = resort.TryBuild(defId, x, y);
            if (result.Success) _report.BuildingsPlaced++;
            else _report.BuildFailures.Add($"{defId}@{x},{y}: {result.Error}");
        }

        private static int FindId(Resort resort, string defId)
        {
            foreach (var building in resort.Buildings.Values)
                if (building.Def.Id == defId) return building.Id;
            return -1;
        }

        private static string CaptureScreenshot(string relativePath)
        {
            var camera = Camera.main;
            if (camera == null) return "";

            const int width = 1280, height = 720;
            var rt = new RenderTexture(width, height, 24);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;

            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            camera.targetTexture = null;
            RenderTexture.active = null;
            rt.Release();

            var path = Path.GetFullPath(relativePath);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            return path;
        }
    }
}
#endif
