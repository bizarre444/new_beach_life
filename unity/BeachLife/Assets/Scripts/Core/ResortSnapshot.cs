using System;
using System.Collections.Generic;

namespace BeachLife.Core
{
    // Serializable state snapshots for save games. Plain data only: the Unity layer turns them
    // into JSON with JsonUtility, while headless tools can round-trip them without an engine.

    [Serializable]
    public sealed class ResortSnapshot
    {
        public int Version = 3;
        public int Seed;
        public int NextBuildingId = 1;
        public int NextTouristId = 1;
        public int NextStaffId = 1;
        public float Reputation;
        public int TotalVisitors;
        public int TotalWalkouts;
        public int NegativeCashDays;
        public bool Bankrupt;

        public ClockSnapshot Clock;
        // Initialized so CaptureSnapshot can fill them in without null checks; JsonUtility overwrites on load.
        public EconomySnapshot Economy = new EconomySnapshot();
        public MapSnapshot Map;
        public WeatherSnapshot Weather;
        public MarketingSnapshot Marketing;
        public GoalsSnapshot Goals = new GoalsSnapshot();
        public List<BuildingSnapshot> Buildings = new List<BuildingSnapshot>();
        public List<TouristSnapshot> Tourists = new List<TouristSnapshot>();
        public List<StaffSnapshot> Staff = new List<StaffSnapshot>();
        public List<string> EventLog = new List<string>();
        public float[] Litter;
    }

    [Serializable]
    public sealed class ClockSnapshot
    {
        public int Day;
        public float MinuteOfDay;
        public float Speed;
    }

    [Serializable]
    public sealed class EconomySnapshot
    {
        public int Cash;
        public LedgerSnapshot Today;
        public List<LedgerSnapshot> History = new List<LedgerSnapshot>();
    }

    [Serializable]
    public sealed class LedgerSnapshot
    {
        public int Day;
        public int Income;
        public int Upkeep;
        public int Construction;
        public int Marketing;
        public int Wages;
        public int Visitors;
        public int LeftAngry;
    }

    [Serializable]
    public sealed class MapSnapshot
    {
        public int Width;
        public int Height;
        public int[] Tiles;
    }

    [Serializable]
    public sealed class WeatherSnapshot
    {
        public int Current;
        public int Forecast;
    }

    [Serializable]
    public sealed class MarketingSnapshot
    {
        public int Level;
    }

    [Serializable]
    public sealed class GoalsSnapshot
    {
        public List<int> Achieved = new List<int>();
    }

    [Serializable]
    public sealed class BuildingSnapshot
    {
        public int Id;
        public string DefId;
        public int X;
        public int Y;
        public float PriceMultiplier = 1f;
        public int Revenue;
        public float Dirtiness;
        /// <summary>Hotel guests holding a bed but not physically inside right now.</summary>
        public List<int> LongStayers = new List<int>();
    }

    [Serializable]
    public sealed class TouristSnapshot
    {
        public int Id;
        public string Name;
        public int GuestType;
        public float X;
        public float Y;
        public float[] Needs;
        public float Mood;
        public int Money;
        public int Spent;
        public int State;
        public int TargetBuildingId = -1;
        public int PursuedNeed = -1;
        public float ServiceRemaining;
        public float WaitedMinutes;
        public int StayDaysRemaining;
    }

    [Serializable]
    public sealed class StaffSnapshot
    {
        public int Id;
        public int Type;
        public string Name;
        public float X;
        public float Y;
        public int AssignedBuildingId = -1;
    }
}
