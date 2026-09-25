using System.Collections.Generic;

namespace BeachLife.Core
{
    public sealed class DayLedger
    {
        public int Day;
        public int Income;
        public int Upkeep;
        public int Construction;
        public int Visitors;
        public int LeftAngry;

        public int Profit => Income - Upkeep - Construction;
    }

    public sealed class Economy
    {
        public int Cash { get; private set; }
        public DayLedger Today { get; private set; } = new DayLedger { Day = 1 };
        public readonly List<DayLedger> History = new List<DayLedger>();

        public Economy(int startingCash)
        {
            Cash = startingCash;
        }

        public bool CanAfford(int amount) => Cash >= amount;

        public bool TrySpendConstruction(int amount)
        {
            if (!CanAfford(amount)) return false;
            Cash -= amount;
            Today.Construction += amount;
            return true;
        }

        public void Refund(int amount)
        {
            Cash += amount;
            Today.Construction -= amount;
        }

        public void Earn(int amount)
        {
            Cash += amount;
            Today.Income += amount;
        }

        public void PayUpkeep(int amount)
        {
            Cash -= amount;
            Today.Upkeep += amount;
        }

        public DayLedger CloseDay()
        {
            var closed = Today;
            History.Add(closed);
            Today = new DayLedger { Day = closed.Day + 1 };
            return closed;
        }
    }
}
