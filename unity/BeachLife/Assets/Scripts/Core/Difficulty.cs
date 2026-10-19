namespace BeachLife.Core
{
    public enum Difficulty
    {
        Relaxed = 0,
        Classic = 1,
        Challenge = 2,
    }

    /// <summary>Starting conditions for one difficulty level.</summary>
    public readonly struct DifficultyInfo
    {
        public readonly Difficulty Level;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly int StartingCash;
        public readonly int StartingReputation;
        public readonly float ArrivalsPerMinuteBase;
        public readonly int BankruptcyGraceDays;

        public DifficultyInfo(Difficulty level, string displayName, string description,
            int startingCash, int startingReputation, float arrivalsPerMinuteBase, int bankruptcyGraceDays)
        {
            Level = level;
            DisplayName = displayName;
            Description = description;
            StartingCash = startingCash;
            StartingReputation = startingReputation;
            ArrivalsPerMinuteBase = arrivalsPerMinuteBase;
            BankruptcyGraceDays = bankruptcyGraceDays;
        }
    }

    public static class DifficultyTable
    {
        public static readonly DifficultyInfo[] All =
        {
            new DifficultyInfo(Difficulty.Relaxed, "Easy breezes",
                "A generous grant, warm crowds and a patient bank. For building the resort of your dreams.",
                90000, 45, 0.45f, 5),
            new DifficultyInfo(Difficulty.Classic, "Classic resort",
                "The intended experience: a starter loan, modest crowds and three days of bank patience.",
                50000, 30, 0.35f, 3),
            new DifficultyInfo(Difficulty.Challenge, "Seasoned manager",
                "A tight budget, thin early crowds and an impatient bank. Every guest counts.",
                30000, 15, 0.30f, 2),
        };

        public static DifficultyInfo Get(Difficulty level) => All[(int)level];
    }
}
