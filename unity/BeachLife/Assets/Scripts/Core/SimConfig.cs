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
        public float StaffWalkSpeed = 1.3f;         // tiles per game minute
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
        public float ReputationSmoothing = 0.02f;

        public int TouristStartMoneyMin = 60;
        public int TouristStartMoneyMax = 260;
        public float MoodLeaveThreshold = 0.12f;

        // ---------------------------------------------------------------- litter

        /// <summary>Chance per minute that a wandering guest drops litter.</summary>
        public float LitterChancePerMinute = 0.004f;
        /// <summary>Drop chance multiplier when a litter bin is within <see cref="BinCoverageRadius"/> tiles.</summary>
        public float BinLitterReduction = 0.15f;
        public int BinCoverageRadius = 4;
        /// <summary>Max litter that can pile up on one tile.</summary>
        public float LitterMaxPerTile = 3f;
        /// <summary>How strongly nearby litter drags the local mood down, per unit of litter.</summary>
        public float MoodLossPerLitter = 0.02f;
        /// <summary>Venue attractiveness penalty at maximum nearby litter.</summary>
        public float LitterVenuePenalty = 0.4f;
        /// <summary>Passive litter decay per minute (wind, tide).</summary>
        public float LitterNaturalDecayPerMinute = 0.0008f;

        // ---------------------------------------------------------------- staff

        /// <summary>Minutes a cleaner needs to fully clean one venue.</summary>
        public float CleanerVenueMinutes = 45f;
        /// <summary>Minutes a cleaner needs to clear one litter tile.</summary>
        public float CleanerTileMinutes = 6f;
        /// <summary>Extra venue capacity each attendant adds.</summary>
        public int AttendantCapacityBonus = 3;
        /// <summary>Service speed multiplier when an attendant works the venue.</summary>
        public float AttendantServiceMultiplier = 1.25f;

        // ---------------------------------------------------------------- guests & hotels

        /// <summary>Chance a spawned guest books a hotel stay (only when a hotel has free beds).</summary>
        public float HotelGuestChance = 0.35f;
        public int MaxStayDays = 3;
        /// <summary>Minute after day start when hotel guests wake up.</summary>
        public int WakeUpMinute = 7 * 60 + 30;
        /// <summary>Energy level below which overnight guests head to bed at night.</summary>
        public float SleepEnergyThreshold = 0.6f;
        /// <summary>Score penalty multiplier for venues priced above the guest's tolerance.</summary>
        public float OverpricedScorePenalty = 0.35f;

        // ---------------------------------------------------------------- safety & failure

        /// <summary>Per guest on open sand, per minute, chance of an accident when no lifeguard tower covers them.</summary>
        public float BeachIncidentChancePerGuestMinute = 0.000012f;
        public int LifeguardCoverageRadius = 10;
        public float IncidentMoodPenalty = 0.35f;
        public float IncidentReputationLoss = 2f;
        /// <summary>Consecutive days the resort may run in the red before it goes bankrupt.</summary>
        public int BankruptcyGraceDays = 3;
    }
}
