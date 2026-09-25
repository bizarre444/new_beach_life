using System;
using System.Linq;
using BeachLife.Core;

namespace BeachLife.SimRunner
{
    /// <summary>Runs the simulation without Unity so balance changes can be checked from the shell.</summary>
    public static class Program
    {
        public static void Main(string[] args)
        {
            int days = args.Length > 0 && int.TryParse(args[0], out int parsed) ? parsed : 5;
            int seed = args.Length > 1 && int.TryParse(args[1], out int parsedSeed) ? parsedSeed : 2024;

            var resort = new Resort(48, 36, seed);
            PlaceStarterResort(resort);

            Console.WriteLine($"Starting cash: ${resort.Economy.Cash:n0}   reputation: {resort.Reputation:0}");
            int minutesPerDay = resort.Config.DayEndMinute - resort.Config.DayStartMinute;

            for (int day = 0; day < days; day++)
            {
                int peakGuests = 0;
                for (int minute = 0; minute < minutesPerDay; minute++)
                {
                    resort.Tick(1f);
                    peakGuests = Math.Max(peakGuests, resort.Tourists.Count);
                }

                var ledger = resort.Economy.History.Last();
                Console.WriteLine(
                    $"Day {ledger.Day,2}: visitors {ledger.Visitors,3}  peak {peakGuests,3}  " +
                    $"income ${ledger.Income,6:n0}  upkeep ${ledger.Upkeep,5:n0}  profit ${ledger.Profit,7:n0}  " +
                    $"angry {ledger.LeftAngry,3}  reputation {resort.Reputation,5:0.0}  cash ${resort.Economy.Cash,7:n0}");
            }

            Console.WriteLine();
            Console.WriteLine("Venue takings:");
            foreach (var building in resort.Buildings.Values.OrderByDescending(b => b.Revenue))
                Console.WriteLine($"  {building.Def.DisplayName,-16} ${building.Revenue,7:n0}  dirt {building.Dirtiness * 100f,3:0}%");
        }

        private static void PlaceStarterResort(Resort resort)
        {
            Place(resort, "burger_stand", 8, 24);
            Place(resort, "beach_bar", 14, 24);
            Place(resort, "toilets", 20, 24);
            Place(resort, "showers", 24, 24);
            Place(resort, "pool", 28, 22);
            Place(resort, "restaurant", 36, 24);
            for (int i = 0; i < 12; i++) Place(resort, "sun_lounger", 6 + i * 3, 8);
            for (int i = 0; i < 6; i++) Place(resort, "parasol", 7 + i * 6, 10);
            for (int i = 0; i < 4; i++) Place(resort, "litter_bin", 10 + i * 8, 20);
        }

        private static void Place(Resort resort, string id, int x, int y)
        {
            var result = resort.TryBuild(id, x, y);
            if (!result.Success) Console.WriteLine($"  ! could not place {id} at ({x},{y}): {result.Error}");
        }
    }
}
