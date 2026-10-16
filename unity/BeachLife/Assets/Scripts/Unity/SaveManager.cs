using System;
using System.IO;
using BeachLife.Core;
using UnityEngine;

namespace BeachLife.Unity
{
    /// <summary>Save slots in persistent storage; the snapshot DTOs come from the core sim.</summary>
    public static class SaveManager
    {
        public const int SlotCount = 3;

        public static string SlotPath(int slot) =>
            Path.Combine(Application.persistentDataPath, $"beachlife_slot{slot}.json");

        public static bool Save(Resort resort, int slot)
        {
            try
            {
                var json = JsonUtility.ToJson(resort.CaptureSnapshot(), prettyPrint: false);
                File.WriteAllText(SlotPath(slot), json);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"BeachLife: saving slot {slot} failed - {e.Message}");
                return false;
            }
        }

        public static ResortSnapshot Load(int slot)
        {
            try
            {
                var path = SlotPath(slot);
                if (!File.Exists(path)) return null;
                return JsonUtility.FromJson<ResortSnapshot>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogError($"BeachLife: loading slot {slot} failed - {e.Message}");
                return null;
            }
        }

        public static bool HasSave(int slot) => File.Exists(SlotPath(slot));

        /// <summary>"Day 4 13:20 - $41,250" style label for the slot buttons; empty when the slot is free.</summary>
        public static string SlotLabel(int slot)
        {
            var snapshot = Load(slot);
            if (snapshot?.Clock == null) return "(empty)";
            return $"Day {snapshot.Clock.Day}  ${snapshot.Economy.Cash:n0}";
        }
    }
}
