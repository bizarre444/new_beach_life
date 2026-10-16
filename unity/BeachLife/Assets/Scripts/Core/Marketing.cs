namespace BeachLife.Core
{
    public enum CampaignLevel
    {
        None = 0,
        Starter = 1,
        Regional = 2,
        National = 3,
    }

    /// <summary>Static description of one marketing campaign level.</summary>
    public readonly struct CampaignInfo
    {
        public readonly CampaignLevel Level;
        public readonly string DisplayName;
        /// <summary>Daily cost, charged at the end of every day.</summary>
        public readonly int CostPerDay;
        /// <summary>Multiplier on the guest arrival rate.</summary>
        public readonly float ArrivalMultiplier;

        public CampaignInfo(CampaignLevel level, string displayName, int costPerDay, float arrivalMultiplier)
        {
            Level = level;
            DisplayName = displayName;
            CostPerDay = costPerDay;
            ArrivalMultiplier = arrivalMultiplier;
        }
    }

    public static class CampaignTable
    {
        public static readonly CampaignInfo[] All =
        {
            new CampaignInfo(CampaignLevel.None, "No campaign", 0, 1f),
            new CampaignInfo(CampaignLevel.Starter, "Local flyers", 120, 1.15f),
            new CampaignInfo(CampaignLevel.Regional, "Regional ads", 380, 1.35f),
            new CampaignInfo(CampaignLevel.National, "National TV", 950, 1.7f),
        };

        public static CampaignInfo Get(CampaignLevel level) => All[(int)level];
    }
}
