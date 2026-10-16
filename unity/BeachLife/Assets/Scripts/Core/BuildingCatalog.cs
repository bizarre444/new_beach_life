using System.Collections.Generic;
using System.Linq;

namespace BeachLife.Core
{
    /// <summary>The starting set of buildable objects, in the spirit of the original beach resort sim.</summary>
    public static class BuildingCatalog
    {
        public static readonly IReadOnlyList<BuildingDef> All = Build();

        private static readonly Dictionary<string, BuildingDef> ById =
            All.ToDictionary(def => def.Id);

        public static BuildingDef Get(string id) => ById[id];

        public static bool TryGet(string id, out BuildingDef def) => ById.TryGetValue(id, out def);

        private static List<BuildingDef> Build()
        {
            return new List<BuildingDef>
            {
                new BuildingDef
                {
                    Id = "sun_lounger", DisplayName = "Sun lounger", Category = BuildingCategory.Beach,
                    Width = 1, Height = 1, BuildCost = 120, DailyUpkeep = 2, Capacity = 1,
                    ServiceMinutes = 45f, TicketPrice = 6,
                    AllowedTiles = { TileType.Sand },
                    Satisfies = { { NeedType.Tan, 0.9f }, { NeedType.Energy, 0.5f } },
                },
                new BuildingDef
                {
                    Id = "parasol", DisplayName = "Parasol", Category = BuildingCategory.Beach,
                    Width = 1, Height = 1, BuildCost = 90, DailyUpkeep = 1, Capacity = 2,
                    ServiceMinutes = 30f, TicketPrice = 3,
                    AllowedTiles = { TileType.Sand },
                    Satisfies = { { NeedType.Energy, 0.4f } },
                    AmbienceBonus = 0.02f, AmbienceRadius = 3,
                },
                new BuildingDef
                {
                    Id = "beach_bar", DisplayName = "Beach bar", Category = BuildingCategory.Drink,
                    Width = 3, Height = 2, BuildCost = 3200, DailyUpkeep = 120, Capacity = 6,
                    ServiceMinutes = 20f, TicketPrice = 14,
                    Satisfies = { { NeedType.Thirst, 0.95f }, { NeedType.Fun, 0.25f } },
                },
                new BuildingDef
                {
                    Id = "burger_stand", DisplayName = "Burger stand", Category = BuildingCategory.Food,
                    Width = 2, Height = 2, BuildCost = 2100, DailyUpkeep = 80, Capacity = 4,
                    ServiceMinutes = 18f, TicketPrice = 12,
                    Satisfies = { { NeedType.Hunger, 0.85f } },
                },
                new BuildingDef
                {
                    Id = "restaurant", DisplayName = "Restaurant", Category = BuildingCategory.Food,
                    Width = 4, Height = 3, BuildCost = 7800, DailyUpkeep = 260, Capacity = 12,
                    ServiceMinutes = 50f, TicketPrice = 34,
                    Satisfies = { { NeedType.Hunger, 1f }, { NeedType.Thirst, 0.6f }, { NeedType.Fun, 0.2f } },
                },
                new BuildingDef
                {
                    Id = "toilets", DisplayName = "Toilets", Category = BuildingCategory.Facility,
                    Width = 2, Height = 2, BuildCost = 1400, DailyUpkeep = 60, Capacity = 4,
                    ServiceMinutes = 8f, TicketPrice = 1,
                    Satisfies = { { NeedType.Bladder, 1f }, { NeedType.Hygiene, 0.2f } },
                },
                new BuildingDef
                {
                    Id = "showers", DisplayName = "Showers", Category = BuildingCategory.Facility,
                    Width = 2, Height = 1, BuildCost = 900, DailyUpkeep = 40, Capacity = 2,
                    ServiceMinutes = 10f, TicketPrice = 2,
                    Satisfies = { { NeedType.Hygiene, 1f } },
                },
                new BuildingDef
                {
                    Id = "pool", DisplayName = "Swimming pool", Category = BuildingCategory.Entertainment,
                    Width = 5, Height = 4, BuildCost = 9500, DailyUpkeep = 300, Capacity = 20,
                    ServiceMinutes = 40f, TicketPrice = 10,
                    Satisfies = { { NeedType.Fun, 0.7f }, { NeedType.Hygiene, 0.3f }, { NeedType.Tan, 0.4f } },
                    AmbienceBonus = 0.03f, AmbienceRadius = 6,
                },
                new BuildingDef
                {
                    Id = "nightclub", DisplayName = "Nightclub", Category = BuildingCategory.Entertainment,
                    Width = 4, Height = 4, BuildCost = 14000, DailyUpkeep = 520, Capacity = 30,
                    ServiceMinutes = 70f, TicketPrice = 28,
                    Satisfies = { { NeedType.Fun, 1f }, { NeedType.Thirst, 0.4f }, { NeedType.Energy, -0.3f } },
                },
                new BuildingDef
                {
                    Id = "hotel_block", DisplayName = "Hotel block", Category = BuildingCategory.Accommodation,
                    Width = 5, Height = 3, BuildCost = 18000, DailyUpkeep = 600, Capacity = 24,
                    ServiceMinutes = 240f, TicketPrice = 55,
                    Satisfies = { { NeedType.Energy, 1f }, { NeedType.Hygiene, 0.7f }, { NeedType.Bladder, 1f } },
                },
                new BuildingDef
                {
                    Id = "ice_cream_stand", DisplayName = "Ice cream stand", Category = BuildingCategory.Drink,
                    Width = 2, Height = 1, BuildCost = 1600, DailyUpkeep = 55, Capacity = 3,
                    ServiceMinutes = 8f, TicketPrice = 8,
                    Satisfies = { { NeedType.Thirst, 0.5f }, { NeedType.Hunger, 0.3f }, { NeedType.Fun, 0.15f } },
                },
                new BuildingDef
                {
                    Id = "volleyball_court", DisplayName = "Volleyball court", Category = BuildingCategory.Entertainment,
                    Width = 4, Height = 3, BuildCost = 2400, DailyUpkeep = 70, Capacity = 8,
                    ServiceMinutes = 30f, TicketPrice = 5,
                    AllowedTiles = { TileType.Sand },
                    Satisfies = { { NeedType.Fun, 0.8f }, { NeedType.Energy, -0.2f } },
                },
                new BuildingDef
                {
                    Id = "gift_shop", DisplayName = "Gift shop", Category = BuildingCategory.Entertainment,
                    Width = 3, Height = 2, BuildCost = 5200, DailyUpkeep = 180, Capacity = 6,
                    ServiceMinutes = 15f, TicketPrice = 22,
                    Satisfies = { { NeedType.Fun, 0.45f } },
                },
                new BuildingDef
                {
                    Id = "lifeguard_tower", DisplayName = "Lifeguard tower", Category = BuildingCategory.Infrastructure,
                    Width = 1, Height = 1, BuildCost = 700, DailyUpkeep = 25, Capacity = 0,
                    ServiceMinutes = 0f, TicketPrice = 0,
                    AllowedTiles = { TileType.Sand },
                    ProvidesSafety = true,
                    AmbienceBonus = 0.01f, AmbienceRadius = 5,
                },
                new BuildingDef
                {
                    Id = "fountain", DisplayName = "Fountain", Category = BuildingCategory.Infrastructure,
                    Width = 2, Height = 2, BuildCost = 1400, DailyUpkeep = 15, Capacity = 0,
                    ServiceMinutes = 0f, TicketPrice = 0,
                    AmbienceBonus = 0.03f, AmbienceRadius = 6,
                },
                new BuildingDef
                {
                    Id = "litter_bin", DisplayName = "Litter bin", Category = BuildingCategory.Infrastructure,
                    Width = 1, Height = 1, BuildCost = 150, DailyUpkeep = 8, Capacity = 0,
                    ServiceMinutes = 0f, TicketPrice = 0,
                    AmbienceBonus = 0.015f, AmbienceRadius = 5,
                },
                new BuildingDef
                {
                    Id = "palm_tree", DisplayName = "Palm tree", Category = BuildingCategory.Infrastructure,
                    Width = 1, Height = 1, BuildCost = 200, DailyUpkeep = 4, Capacity = 0,
                    ServiceMinutes = 0f, TicketPrice = 0,
                    AmbienceBonus = 0.025f, AmbienceRadius = 4,
                },
            };
        }
    }
}
