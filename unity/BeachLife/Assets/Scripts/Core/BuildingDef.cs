using System.Collections.Generic;

namespace BeachLife.Core
{
    public enum BuildingCategory
    {
        Beach,
        Food,
        Drink,
        Facility,
        Entertainment,
        Accommodation,
        Infrastructure,
    }

    /// <summary>Static description of a placeable object.</summary>
    public sealed class BuildingDef
    {
        public string Id;
        public string DisplayName;
        public BuildingCategory Category;
        public int Width = 1;
        public int Height = 1;
        public int BuildCost;
        public int DailyUpkeep;
        /// <summary>Guests that can use it at the same time.</summary>
        public int Capacity = 1;
        /// <summary>In-game minutes a guest spends inside.</summary>
        public float ServiceMinutes = 10f;
        /// <summary>What a guest pays per visit.</summary>
        public int TicketPrice;
        /// <summary>Need -> amount restored over a full visit.</summary>
        public Dictionary<NeedType, float> Satisfies = new Dictionary<NeedType, float>();
        /// <summary>Only placeable on these terrain types; empty means anywhere buildable.</summary>
        public HashSet<TileType> AllowedTiles = new HashSet<TileType>();
        /// <summary>Passive mood bonus for guests nearby, and its radius in tiles.</summary>
        public float AmbienceBonus;
        public int AmbienceRadius = 4;

        public bool CanSatisfy(NeedType need) => Satisfies.ContainsKey(need);

        public bool AllowsTile(TileType tile) => AllowedTiles.Count == 0 || AllowedTiles.Contains(tile);
    }
}
