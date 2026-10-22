using BeachLife.Core;
using UnityEngine;

namespace BeachLife.Unity
{
    /// <summary>Owns the simulation and drives it from Unity's update loop.</summary>
    public sealed class ResortRunner : MonoBehaviour
    {
        public static ResortRunner Instance { get; private set; }

        [Header("Plot")]
        // 40x40 grid matches the original Beach Life (iMAP_GRIDSIZE_XY 40).
        public int MapWidth = 40;
        public int MapHeight = 40;
        public int Seed = 12345;

        [Header("Game")]
        public Difficulty Difficulty = Difficulty.Classic;
        /// <summary>True while a playable (non-bankrupt) session exists, so the menu can offer Continue.</summary>
        public bool HasSession { get; private set; }

        [Header("Time")]
        [Tooltip("Real seconds for one in-game minute at speed 1. 1.0 matches the original's 1200-second day.")]
        public float RealSecondsPerGameMinute = 1f;
        public float[] SpeedSteps = { 0f, 1f, 3f, 8f };

        public Resort Resort { get; private set; }
        public int SpeedIndex { get; private set; } = 1;

        private void Awake()
        {
            Instance = this;
            Resort = CreateResort(Seed);
            var rig = FindObjectOfType<CameraRig>();
            if (rig != null) rig.SetMapBounds(MapWidth, MapHeight);
        }

        private Resort CreateResort(int seed)
        {
            var preset = DifficultyTable.Get(Difficulty);
            var config = new SimConfig
            {
                RealSecondsPerGameMinute = RealSecondsPerGameMinute,
                StartingCash = preset.StartingCash,
                StartingReputation = preset.StartingReputation,
                ArrivalsPerMinuteBase = preset.ArrivalsPerMinuteBase,
                BankruptcyGraceDays = preset.BankruptcyGraceDays,
            };
            var resort = new Resort(MapWidth, MapHeight, seed, config);
            resort.Clock.Speed = SpeedSteps[SpeedIndex];
            return resort;
        }

        private void Update()
        {
            Resort.Tick(Time.deltaTime);
            if (Resort.Bankrupt && SpeedIndex != 0) SetSpeed(0); // game over freezes the clock
        }

        public void SetSpeed(int index)
        {
            SpeedIndex = Mathf.Clamp(index, 0, SpeedSteps.Length - 1);
            Resort.Clock.Speed = SpeedSteps[SpeedIndex];
        }

        public void CycleSpeed() => SetSpeed((SpeedIndex + 1) % SpeedSteps.Length);

        /// <summary>Swaps the simulation for a fresh one; views re-sync themselves.</summary>
        public void NewGame()
        {
            Seed = Random.Range(1, int.MaxValue);
            Resort = CreateResort(Seed);
            SpeedIndex = 1;
            Resort.Clock.Speed = SpeedSteps[SpeedIndex];
            HasSession = true;
        }

        /// <summary>Marks the session started after loading a save.</summary>
        public void MarkSessionStarted() => HasSession = true;

        /// <summary>Clears the session (bankruptcy); the menu then hides Continue.</summary>
        public void ClearSession() => HasSession = false;

        public static Vector3 WorldPos(float tileX, float tileY, float height = 0f) =>
            new Vector3(tileX + 0.5f, height, tileY + 0.5f);

        public static Vector3 WorldPos(GridPos pos, float height = 0f) =>
            WorldPos(pos.X, pos.Y, height);
    }
}
