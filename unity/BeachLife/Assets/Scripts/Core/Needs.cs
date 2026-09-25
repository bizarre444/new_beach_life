using System;

namespace BeachLife.Core
{
    public enum NeedType
    {
        Hunger = 0,
        Thirst = 1,
        Bladder = 2,
        Hygiene = 3,
        Fun = 4,
        Energy = 5,
        Tan = 6,
    }

    /// <summary>Need levels in [0,1] where 1 is fully satisfied.</summary>
    public struct Needs
    {
        public const int Count = 7;

        public float[] Values;

        public static Needs CreateFresh(Random rng)
        {
            var needs = new Needs { Values = new float[Count] };
            for (int i = 0; i < Count; i++)
                needs.Values[i] = 0.7f + (float)rng.NextDouble() * 0.3f;
            return needs;
        }

        public float Get(NeedType type) => Values[(int)type];

        public void Set(NeedType type, float value) => Values[(int)type] = Clamp01(value);

        public void Add(NeedType type, float delta) => Set(type, Values[(int)type] + delta);

        public void Decay(SimConfig config, float minutes)
        {
            for (int i = 0; i < Count; i++)
                Values[i] = Clamp01(Values[i] - config.NeedDecay[i] * minutes);
        }

        /// <summary>Lowest need, or null when nothing is below <paramref name="threshold"/>.</summary>
        public NeedType? MostUrgent(float threshold)
        {
            int worst = -1;
            float worstValue = threshold;
            for (int i = 0; i < Count; i++)
            {
                if (Values[i] < worstValue)
                {
                    worstValue = Values[i];
                    worst = i;
                }
            }
            return worst < 0 ? (NeedType?)null : (NeedType)worst;
        }

        public int CountBelow(float threshold)
        {
            int n = 0;
            for (int i = 0; i < Count; i++)
                if (Values[i] < threshold) n++;
            return n;
        }

        public float Average()
        {
            float sum = 0f;
            for (int i = 0; i < Count; i++) sum += Values[i];
            return sum / Count;
        }

        internal static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
