using BeachLife.Core;
using UnityEngine;

namespace BeachLife.Unity
{
    /// <summary>Owns the simulation and drives it from Unity's update loop.</summary>
    public sealed class ResortRunner : MonoBehaviour
    {
        public static ResortRunner Instance { get; private set; }

        [Header("Plot")]
        public int MapWidth = 48;
        public int MapHeight = 36;
        public int Seed = 12345;

        [Header("Time")]
        [Tooltip("Real seconds for one in-game minute at speed 1.")]
        public float RealSecondsPerGameMinute = 0.25f;
        public float[] SpeedSteps = { 0f, 1f, 3f, 8f };

        public Resort Resort { get; private set; }
        public int SpeedIndex { get; private set; } = 1;

        private void Awake()
        {
            Instance = this;
            var config = new SimConfig { RealSecondsPerGameMinute = RealSecondsPerGameMinute };
            Resort = new Resort(MapWidth, MapHeight, Seed, config);
            Resort.Clock.Speed = SpeedSteps[SpeedIndex];
        }

        private void Update()
        {
            Resort.Tick(Time.deltaTime);
        }

        public void SetSpeed(int index)
        {
            SpeedIndex = Mathf.Clamp(index, 0, SpeedSteps.Length - 1);
            Resort.Clock.Speed = SpeedSteps[SpeedIndex];
        }

        public void CycleSpeed() => SetSpeed((SpeedIndex + 1) % SpeedSteps.Length);

        public static Vector3 WorldPos(float tileX, float tileY, float height = 0f) =>
            new Vector3(tileX + 0.5f, height, tileY + 0.5f);

        public static Vector3 WorldPos(GridPos pos, float height = 0f) =>
            WorldPos(pos.X, pos.Y, height);
    }
}
