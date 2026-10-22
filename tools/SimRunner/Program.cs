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

            var resort = new Resort(40, 40, seed, new SimConfig { StartingCash = 150000 });
            PlaceStarterResort(resort);

            // Exercise the new systems the same way a player would.
            resort.Hire(StaffType.Cleaner);
            resort.Hire(StaffType.Cleaner);
            var attendant = resort.Hire(StaffType.Attendant);
            var bar = resort.Buildings.Values.FirstOrDefault(b => b.Def.Id == "beach_bar");
            if (attendant != null && bar != null) resort.AssignAttendant(attendant.Id, bar.Id);
            resort.SetCampaign(CampaignLevel.Regional);

            Console.WriteLine($"Starting cash: ${resort.Economy.Cash:n0}   reputation: {resort.Reputation:0}");
            Console.WriteLine($"Forecast: {WeatherTable.Get(resort.CurrentWeather).DisplayName}, then {WeatherTable.Get(resort.ForecastWeather).DisplayName}");
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
                float litter = resort.Litter.Sum();
                int overnight = resort.Tourists.Count(t => t.StayDaysRemaining > 0);
                Console.WriteLine(
                    $"Day {ledger.Day,2}: {WeatherTable.Get(resort.CurrentWeather).DisplayName,-7}  visitors {ledger.Visitors,3}  peak {peakGuests,3}  " +
                    $"income ${ledger.Income,6:n0}  upkeep ${ledger.Upkeep,5:n0}  wages ${ledger.Wages,4:n0}  ads ${ledger.Marketing,4:n0}  " +
                    $"profit ${ledger.Profit,7:n0}  angry {ledger.LeftAngry,3}  litter {litter,5:0.0}  overnight {overnight,2}  " +
                    $"rep {resort.Reputation,5:0.0}  cash ${resort.Economy.Cash,7:n0}");
            }

            Console.WriteLine();
            Console.WriteLine("Venue takings:");
            foreach (var building in resort.Buildings.Values.OrderByDescending(b => b.Revenue))
                Console.WriteLine($"  {building.Def.DisplayName,-16} ${building.Revenue,7:n0}  dirt {building.Dirtiness * 100f,3:0}%");

            Console.WriteLine();
            Console.WriteLine("Goals reached:");
            foreach (var id in resort.Goals.Achieved)
                Console.WriteLine($"  {GoalTable.Get(id).DisplayName}");
            Console.WriteLine(resort.Bankrupt ? "RESORT WENT BANKRUPT" : $"Resort healthy after {days} days");
        }

        private static void PlaceStarterResort(Resort resort)
        {
            Place(resort, "burger_stand", 8, 24);
            Place(resort, "beach_bar", 14, 24);
            Place(resort, "toilets", 20, 24);
            Place(resort, "showers", 24, 24);
            Place(resort, "pool", 28, 22);
            Place(resort, "restaurant", 36, 24);
            Place(resort, "hotel_block", 33, 20);
            Place(resort, "ice_cream_stand", 26, 26);
            Place(resort, "volleyball_court", 10, 8);
            Place(resort, "gift_shop", 5, 26);
            for (int i = 0; i < 12; i++) Place(resort, "sun_lounger", 6 + i * 3, 6);
            for (int i = 0; i < 6; i++) Place(resort, "parasol", 7 + i * 6, 12);
            for (int i = 0; i < 4; i++) Place(resort, "litter_bin", 10 + i * 8, 18);
            Place(resort, "lifeguard_tower", 20, 6);
            Place(resort, "fountain", 18, 25);
            Place(resort, "palm_tree", 4, 14);
            Place(resort, "palm_tree", 38, 14);
        }

        private static void Place(Resort resort, string id, int x, int y)
        {
            var result = resort.TryBuild(id, x, y);
            if (!result.Success) Console.WriteLine($"  ! could not place {id} at ({x},{y}): {result.Error}");
        }
    }
}
