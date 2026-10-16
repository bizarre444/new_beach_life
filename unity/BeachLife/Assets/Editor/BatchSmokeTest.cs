#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using BeachLife.Core;
using BeachLife.Unity;
using UnityEditor;
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
        }

        private static void WaitForPlayMode()
        {
            if (!EditorApplication.isPlaying) return;
            if (ResortRunner.Instance == null || ResortRunner.Instance.Resort == null) return;

            EditorApplication.update -= WaitForPlayMode;
            _report = new SmokeReport();
            Simulate();
            EditorApplication.update += CollectAndFinish;
        }

        private static void Simulate()
        {
            var resort = ResortRunner.Instance.Resort;
            resort.Clock.Speed = 0f; // drive time manually so 3 days run in one call

            PlaceAll(resort);

            var cleaner = resort.Hire(StaffType.Cleaner);
            var attendant = resort.Hire(StaffType.Attendant);
            if (cleaner == null || attendant == null) Errors.Add("Hire returned null");
            if (attendant != null && !resort.AssignAttendant(attendant.Id, FindId(resort, "beach_bar")))
                Errors.Add("Attendant assignment failed");
            resort.SetCampaign(CampaignLevel.Regional);

            for (int i = 0; i < 3 * 40; i++) resort.Tick(30f); // three in-game days

            _report.VisitorsAfter3Days = resort.TotalVisitors;
            _report.CashAfter3Days = resort.Economy.Cash;
            _report.ReputationAfter3Days = resort.Reputation;

            if (resort.TotalVisitors < 20) Errors.Add($"Too few visitors: {resort.TotalVisitors}");

            var json = JsonUtility.ToJson(resort.CaptureSnapshot());
            var restored = JsonUtility.FromJson<ResortSnapshot>(json);
            var copy = new Resort(48, 36, 1);
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
                _report.Screenshot = CaptureScreenshot();
            }
            catch (Exception e)
            {
                Errors.Add("Screenshot failed: " + e.Message);
            }

            _report.Errors.AddRange(Errors);
            foreach (var error in Errors) Debug.LogError("BeachLifeSmoke: " + error);

            var reportPath = Environment.GetEnvironmentVariable("BEACHLIFE_REPORT");
            if (string.IsNullOrEmpty(reportPath)) reportPath = "Temp/beachlife_report.json";
            File.WriteAllText(reportPath, JsonUtility.ToJson(_report, true));
            Debug.Log($"BeachLife smoke test report: {Path.GetFullPath(reportPath)}");

            EditorApplication.isPlaying = false;
            EditorApplication.Exit(Errors.Count > 0 ? 1 : 0);
        }

        private static void PlaceAll(Resort resort)
        {
            Place(resort, "beach_bar", 20, 20);
            Place(resort, "toilets", 26, 20);
            Place(resort, "restaurant", 12, 20);
            Place(resort, "pool", 30, 21);
            Place(resort, "hotel_block", 38, 20);
            Place(resort, "gift_shop", 8, 21);
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

        private static string CaptureScreenshot()
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

            var path = Path.GetFullPath("Temp/beachlife_smoke.png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            return path;
        }
    }
}
#endif
