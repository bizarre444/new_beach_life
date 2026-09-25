namespace BeachLife.Core
{
    /// <summary>Tunable constants for the resort simulation. All rates are per in-game minute.</summary>
    public sealed class SimConfig
    {
        public static readonly SimConfig Default = new SimConfig();

        public float RealSecondsPerGameMinute = 1f;
        public int DayStartMinute = 6 * 60;
        public int DayEndMinute = 26 * 60; // 02:00 next morning

        public int StartingCash = 50000;
        public int StartingReputation = 30;

        public float TouristWalkSpeed = 0.9f;       // tiles per game minute
        public float MaxTouristsPerReputation = 1.2f;
        public int MinTouristCapacity = 12;
        public float ArrivalsPerMinuteBase = 0.35f;

        /// <summary>Need decay per game minute, indexed by <see cref="NeedType"/>.</summary>
        public float[] NeedDecay =
        {
            0.0035f, // Hunger
            0.0050f, // Thirst
            0.0042f, // Bladder
            0.0022f, // Hygiene
            0.0030f, // Fun
            0.0018f, // Energy
            0.0026f, // Tan
        };

        public float UrgentNeedThreshold = 0.45f;
        public float CriticalNeedThreshold = 0.15f;
        public float MoodGainPerSatisfiedMinute = 0.004f;
        public float MoodLossPerCriticalNeedMinute = 0.012f;
        public float MoodLossPerQueueMinute = 0.002f;
        public float LitterChancePerMinute = 0.004f;
        public float ReputationSmoothing = 0.02f;

        public int TouristStartMoneyMin = 60;
        public int TouristStartMoneyMax = 260;
        public float MoodLeaveThreshold = 0.12f;
    }
}
