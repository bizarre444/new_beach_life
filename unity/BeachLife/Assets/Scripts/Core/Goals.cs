using System.Collections.Generic;

namespace BeachLife.Core
{
    public enum GoalId
    {
        Guests50 = 0,
        Reputation60 = 1,
        Profit5000 = 2,
        OwnHotel = 3,
        Guests150 = 4,
        Reputation85 = 5,
        Cash200k = 6,
    }

    /// <summary>Static description of a milestone the player can reach.</summary>
    public readonly struct GoalDef
    {
        public readonly GoalId Id;
        public readonly string DisplayName;
        public readonly int RewardCash;

        public GoalDef(GoalId id, string displayName, int rewardCash)
        {
            Id = id;
            DisplayName = displayName;
            RewardCash = rewardCash;
        }
    }

    public static class GoalTable
    {
        public static readonly GoalDef[] All =
        {
            new GoalDef(GoalId.Guests50, "Welcome 50 guests in a single day", 1500),
            new GoalDef(GoalId.Reputation60, "Reach a reputation of 60", 3000),
            new GoalDef(GoalId.Profit5000, "Make $5,000 profit in one day", 2500),
            new GoalDef(GoalId.OwnHotel, "Keep hotel guests overnight", 4000),
            new GoalDef(GoalId.Guests150, "Welcome 150 guests in a single day", 5000),
            new GoalDef(GoalId.Reputation85, "Reach a reputation of 85", 10000),
            new GoalDef(GoalId.Cash200k, "Hold $200,000 in cash", 10000),
        };

        public static GoalDef Get(GoalId id) => All[(int)id];
    }

    /// <summary>Tracks which milestones are done and pays their rewards.</summary>
    public sealed class Goals
    {
        public readonly HashSet<GoalId> Achieved = new HashSet<GoalId>();

        public bool IsAchieved(GoalId id) => Achieved.Contains(id);

        /// <summary>Checks every outstanding goal; pays cash rewards through the economy.</summary>
        /// <returns>Display names of goals completed by this call.</returns>
        public List<string> Evaluate(Resort resort)
        {
            var completed = new List<string>();
            TryComplete(resort, GoalId.Guests50, resort.Economy.Today.Visitors >= 50, completed);
            TryComplete(resort, GoalId.Guests150, resort.Economy.Today.Visitors >= 150, completed);
            TryComplete(resort, GoalId.Reputation60, resort.Reputation >= 60f, completed);
            TryComplete(resort, GoalId.Reputation85, resort.Reputation >= 85f, completed);
            TryComplete(resort, GoalId.Profit5000, resort.Economy.Today.Profit >= 5000, completed);
            TryComplete(resort, GoalId.Cash200k, resort.Economy.Cash >= 200000, completed);
            TryComplete(resort, GoalId.OwnHotel, resort.HasOvernightGuests, completed);
            return completed;
        }

        private static void TryComplete(Resort resort, GoalId id, bool condition, List<string> completed)
        {
            if (!condition || resort.Goals.IsAchieved(id)) return;
            resort.Goals.Achieved.Add(id);
            var def = GoalTable.Get(id);
            resort.Economy.Earn(def.RewardCash);
            resort.EventLog.Add($"[{resort.Clock.TimeLabel}] Goal reached: {def.DisplayName} (+${def.RewardCash:n0})");
            completed.Add(def.DisplayName);
        }
    }
}
