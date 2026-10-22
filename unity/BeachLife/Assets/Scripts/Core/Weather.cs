namespace BeachLife.Core
{
    public enum WeatherType
    {
        Sunny = 0,
        Hot = 1,
        Cloudy = 2,
        Rainy = 3,
    }

    /// <summary>Static multipliers for one weather type; 1 is neutral.</summary>
    public readonly struct WeatherInfo
    {
        public readonly WeatherType Type;
        public readonly string DisplayName;
        public readonly string Description;
        /// <summary>Multiplier on the guest arrival rate.</summary>
        public readonly float ArrivalMultiplier;
        /// <summary>Multiplier on mood gain/loss for guests outside.</summary>
        public readonly float MoodMultiplier;
        /// <summary>Multiplier on thirst decay.</summary>
        public readonly float ThirstMultiplier;
        /// <summary>Multiplier on the tan need decay (0 means no tanning).</summary>
        public readonly float TanMultiplier;
        /// <summary>Light tint the renderer applies while this weather lasts.</summary>
        public readonly float SunIntensity;

        public WeatherInfo(WeatherType type, string displayName, string description,
            float arrivalMultiplier, float moodMultiplier, float thirstMultiplier,
            float tanMultiplier, float sunIntensity)
        {
            Type = type;
            DisplayName = displayName;
            Description = description;
            ArrivalMultiplier = arrivalMultiplier;
            MoodMultiplier = moodMultiplier;
            ThirstMultiplier = thirstMultiplier;
            TanMultiplier = tanMultiplier;
            SunIntensity = sunIntensity;
        }
    }

    public static class WeatherTable
    {
        public static readonly WeatherInfo[] All =
        {
            new WeatherInfo(WeatherType.Sunny, "Sunny", "Perfect beach weather.",
                1f, 1f, 1f, 1f, 0.85f),
            new WeatherInfo(WeatherType.Hot, "Heatwave", "Scorching heat - guests drink a lot.",
                1.1f, 1f, 1.7f, 1.3f, 0.92f),
            new WeatherInfo(WeatherType.Cloudy, "Cloudy", "Grey but dry.",
                0.85f, 0.9f, 1f, 0.3f, 0.62f),
            new WeatherInfo(WeatherType.Rainy, "Rain", "Rain drives guests away.",
                0.45f, 0.6f, 0.9f, 0f, 0.42f),
        };

        public static WeatherInfo Get(WeatherType type) => All[(int)type];
    }
}
