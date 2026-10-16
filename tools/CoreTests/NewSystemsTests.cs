using System.Linq;
using BeachLife.Core;
using Xunit;

public class WeatherTests
{
    [Fact]
    public void WeatherTableCoversAllTypes()
    {
        foreach (WeatherType type in System.Enum.GetValues(typeof(WeatherType)))
        {
            var info = WeatherTable.Get(type);
            Assert.False(string.IsNullOrEmpty(info.DisplayName));
            Assert.True(info.ArrivalMultiplier > 0f);
        }
        Assert.True(WeatherTable.Get(WeatherType.Rainy).ArrivalMultiplier < WeatherTable.Get(WeatherType.Sunny).ArrivalMultiplier);
        Assert.True(WeatherTable.Get(WeatherType.Hot).ThirstMultiplier > 1f);
        Assert.Equal(0f, WeatherTable.Get(WeatherType.Rainy).TanMultiplier);
    }

    [Fact]
    public void FreshResortHasCurrentWeatherAndForecast()
    {
        var resort = new Resort(30, 24);
        Assert.True(System.Enum.IsDefined(typeof(WeatherType), resort.CurrentWeather));
        Assert.True(System.Enum.IsDefined(typeof(WeatherType), resort.ForecastWeather));
    }

    [Fact]
    public void RainyDaysBringFewerGuestsThanSunnyDays()
    {
        var sunny = ForceWeather(new Resort(40, 30, 5, new SimConfig { StartingCash = 200000 }), WeatherType.Sunny);
        var rainy = ForceWeather(new Resort(40, 30, 5, new SimConfig { StartingCash = 200000 }), WeatherType.Rainy);
        for (int i = 0; i < 600; i++) { sunny.Tick(1f); rainy.Tick(1f); }

        Assert.True(sunny.TotalVisitors > rainy.TotalVisitors,
            $"sunny={sunny.TotalVisitors} rainy={rainy.TotalVisitors}");
    }

    private static Resort ForceWeather(Resort resort, WeatherType weather)
    {
        var snapshot = resort.CaptureSnapshot();
        snapshot.Weather.Current = (int)weather;
        snapshot.Weather.Forecast = (int)weather;
        resort.ApplySnapshot(snapshot);
        return resort;
    }
}

public class LitterTests
{
    [Fact]
    public void GuestsDropLitterThatBinsWouldReduce()
    {
        var resort = new Resort(40, 30, 9, new SimConfig { StartingCash = 200000 });
        resort.TryBuild("burger_stand", 8, 22);
        for (int i = 0; i < 3000; i++) resort.Tick(1f);

        Assert.True(resort.Litter.Sum() > 0f, "guests never dropped litter");

        var covered = new Resort(40, 30, 9, new SimConfig { StartingCash = 200000 });
        covered.TryBuild("burger_stand", 8, 22);
        var firstGuestTile = covered.Map.GetTile(20, 21); // promenade below the venues
        Assert.True(covered.TryBuild("litter_bin", 20, 21).Success);
        Assert.True(covered.BinCovers(new GridPos(22, 23)));
        Assert.False(covered.BinCovers(new GridPos(38, 28)));
    }

    [Fact]
    public void CleanerClearsLitterAndDirtyVenues()
    {
        // No guests, so nothing gets dirty again while the cleaner works.
        var resort = new Resort(40, 30, 9, new SimConfig { StartingCash = 200000, ArrivalsPerMinuteBase = 0f });
        var bar = resort.TryBuild("beach_bar", 12, 22).Building;
        bar.Dirtiness = 0.8f;
        var litterSpots = new System.Collections.Generic.List<int>();
        for (int y = 18; y <= 22; y++)
            for (int x = 8; x <= 18; x++)
                litterSpots.Add(y * resort.Map.Width + x);
        foreach (var spot in litterSpots) resort.Litter[spot] = 1.5f;

        Assert.NotNull(resort.Hire(StaffType.Cleaner));
        for (int i = 0; i < 4000 && (bar.Dirtiness > 0f || litterSpots.Any(s => resort.Litter[s] > 0f)); i++)
            resort.Tick(1f);

        Assert.Equal(0f, bar.Dirtiness);
        Assert.True(litterSpots.All(s => resort.Litter[s] < 0.1f),
            $"litter remains: {litterSpots.Sum(s => resort.Litter[s]):0.00}");
    }

    [Fact]
    public void LitterNearVenuesHurtsMoodAndAttractiveness()
    {
        var resort = new Resort(40, 30, 5, new SimConfig { StartingCash = 200000 });
        var bar = resort.TryBuild("beach_bar", 12, 22).Building;
        var center = bar.Center;
        float before = resort.AmbienceAt(center);

        // Cover the whole area both NearbyLitter and AmbienceAt sample.
        for (int dy = -1; dy <= bar.Def.Height; dy++)
            for (int dx = -1; dx <= bar.Def.Width; dx++)
                resort.Litter[(bar.Origin.Y + dy) * resort.Map.Width + (bar.Origin.X + dx)] = resort.Config.LitterMaxPerTile;

        Assert.True(resort.NearbyLitter(bar) > resort.Config.LitterMaxPerTile * 0.9f);
        Assert.True(resort.AmbienceAt(center) < before, "litter did not drag the local mood down");
    }
}

public class StaffTests
{
    [Fact]
    public void HiringCostsFeeAndWagesAreChargedDaily()
    {
        var resort = new Resort(30, 24, 1, new SimConfig { StartingCash = 20000 });
        int before = resort.Economy.Cash;

        var cleaner = resort.Hire(StaffType.Cleaner);
        Assert.NotNull(cleaner);
        Assert.Equal(before - StaffTable.Get(StaffType.Cleaner).HireFee, resort.Economy.Cash);

        int dayMinutes = resort.Config.DayEndMinute - resort.Config.DayStartMinute;
        for (int i = 0; i < dayMinutes + 5; i++) resort.Tick(1f);

        Assert.Single(resort.Economy.History);
        Assert.Equal(StaffTable.Get(StaffType.Cleaner).WagePerDay, resort.Economy.History[0].Wages);

        resort.Fire(cleaner.Id);
        Assert.DoesNotContain(cleaner, resort.StaffMembers);
    }

    [Fact]
    public void AttendantBoostsVenueCapacity()
    {
        var resort = new Resort(30, 24, 1, new SimConfig { StartingCash = 20000 });
        var bar = resort.TryBuild("beach_bar", 12, 22).Building;
        int baseCapacity = bar.Def.Capacity;

        var attendant = resort.Hire(StaffType.Attendant);
        Assert.Equal(baseCapacity, resort.EffectiveCapacity(bar));

        Assert.True(resort.AssignAttendant(attendant.Id, bar.Id));
        Assert.Equal(baseCapacity + resort.Config.AttendantCapacityBonus, resort.EffectiveCapacity(bar));
        Assert.Same(attendant, resort.AttendantFor(bar));
        Assert.True(resort.HasFreeSlot(bar));

        // A second attendant cannot double-book the same venue.
        var second = resort.Hire(StaffType.Attendant);
        Assert.True(resort.AssignAttendant(second.Id, bar.Id));
        Assert.Equal(-1, attendant.AssignedBuildingId);
        Assert.Same(second, resort.AttendantFor(bar));
    }

    [Fact]
    public void DemolishingVenueUnassignsItsAttendant()
    {
        var resort = new Resort(30, 24, 1, new SimConfig { StartingCash = 20000 });
        var bar = resort.TryBuild("beach_bar", 12, 22).Building;
        var attendant = resort.Hire(StaffType.Attendant);
        resort.AssignAttendant(attendant.Id, bar.Id);

        resort.Demolish(bar.Id);
        Assert.Equal(-1, attendant.AssignedBuildingId);
    }
}

public class MarketingTests
{
    [Fact]
    public void CampaignsIncreaseArrivalsAndCostMoneyDaily()
    {
        var plain = StarterResort(seed: 5);
        var advertised = StarterResort(seed: 5);
        advertised.SetCampaign(CampaignLevel.Regional);

        for (int i = 0; i < 600; i++) { plain.Tick(1f); advertised.Tick(1f); }
        Assert.True(advertised.TotalVisitors > plain.TotalVisitors,
            $"plain={plain.TotalVisitors} advertised={advertised.TotalVisitors}");

        int dayMinutes = advertised.Config.DayEndMinute - advertised.Config.DayStartMinute;
        for (int i = 0; i < dayMinutes + 5; i++) advertised.Tick(1f);
        Assert.Equal(CampaignTable.Get(CampaignLevel.Regional).CostPerDay, advertised.Economy.History[0].Marketing);
    }

    [Fact]
    public void CampaignTableIsMonotonic()
    {
        Assert.True(CampaignTable.Get(CampaignLevel.Starter).ArrivalMultiplier < CampaignTable.Get(CampaignLevel.Regional).ArrivalMultiplier);
        Assert.True(CampaignTable.Get(CampaignLevel.Regional).ArrivalMultiplier < CampaignTable.Get(CampaignLevel.National).ArrivalMultiplier);
        Assert.Equal(0, CampaignTable.Get(CampaignLevel.None).CostPerDay);
    }

    private static Resort StarterResort(int seed)
    {
        // Capacity far above the arrival volume so the multiplier is not masked by the guest cap.
        var resort = new Resort(40, 30, seed, new SimConfig { StartingCash = 200000, MaxTouristsPerReputation = 20f });
        resort.TryBuild("burger_stand", 8, 22);
        resort.TryBuild("beach_bar", 14, 22);
        resort.TryBuild("toilets", 20, 22);
        return resort;
    }
}

public class GuestTypeTests
{
    [Fact]
    public void ArchetypesCoverTheirMoneyRanges()
    {
        var resort = new Resort(40, 30, 21, new SimConfig
        {
            StartingCash = 200000,
            ArrivalsPerMinuteBase = 2f,
            MaxTouristsPerReputation = 10f,
        });
        for (int i = 0; i < 900; i++) resort.Tick(1f);

        var budget = resort.Tourists.Where(t => t.GuestType == GuestType.Budget).ToList();
        var vip = resort.Tourists.Where(t => t.GuestType == GuestType.Vip).ToList();
        Assert.NotEmpty(budget);
        Assert.NotEmpty(vip);
        Assert.All(budget, t => Assert.InRange(t.Money, 40, 110));
        Assert.All(vip, t => Assert.InRange(t.Money, 180, 600));
        Assert.True(vip[0].PriceTolerance > budget[0].PriceTolerance);
        Assert.True(budget[0].LitterMultiplier < vip[0].LitterMultiplier);
    }
}

public class HotelTests
{
    [Fact]
    public void OvernightGuestsBookBedsAndPayForTheNight()
    {
        var resort = new Resort(48, 36, 33, new SimConfig { StartingCash = 300000 });
        resort.TryBuild("burger_stand", 8, 24);
        resort.TryBuild("beach_bar", 14, 24);
        resort.TryBuild("toilets", 20, 24);
        var hotel = resort.TryBuild("hotel_block", 30, 20).Building;

        bool sawBooking = false, sawSleep = false;
        for (int i = 0; i < 3 * 1200; i++)
        {
            resort.Tick(1f);
            sawBooking |= hotel.LongStayers.Count > 0;
            sawSleep |= hotel.Occupants.Count > 0;
        }

        Assert.True(sawBooking, "nobody ever booked the hotel");
        Assert.True(sawSleep, "nobody ever slept in the hotel");
        Assert.True(hotel.Revenue > 0, "hotel earned nothing");
        Assert.True(resort.HasOvernightGuests || hotel.Revenue > 0);
    }

    [Fact]
    public void DemolishingTheHotelReleasesItsGuests()
    {
        var resort = new Resort(48, 36, 33, new SimConfig { StartingCash = 300000 });
        resort.TryBuild("burger_stand", 8, 24);
        var hotel = resort.TryBuild("hotel_block", 30, 20).Building;
        for (int i = 0; i < 1200 && hotel.LongStayers.Count == 0; i++) resort.Tick(1f);
        Assert.True(hotel.LongStayers.Count > 0, "no bookings to release");

        resort.Demolish(hotel.Id);
        Assert.All(resort.Tourists, t => Assert.Equal(-1, t.HotelBuildingId));
        Assert.All(resort.Tourists, t => Assert.Equal(0, t.StayDaysRemaining));
    }
}

public class GoalTests
{
    [Fact]
    public void GoalsCompleteAndPayTheirReward()
    {
        var resort = new Resort(40, 30, 3, new SimConfig
        {
            StartingCash = 250000, // instantly satisfies Cash200k
            ArrivalsPerMinuteBase = 2f,
            MaxTouristsPerReputation = 3f, // capacity 90, enough to reach 50 guests in a day
        });
        resort.TryBuild("burger_stand", 8, 22);
        resort.TryBuild("beach_bar", 14, 22);
        resort.TryBuild("toilets", 20, 22);

        int dayMinutes = resort.Config.DayEndMinute - resort.Config.DayStartMinute;
        for (int i = 0; i < dayMinutes; i++) resort.Tick(1f);

        Assert.Contains(GoalId.Cash200k, resort.Goals.Achieved);
        Assert.Contains(GoalId.Guests50, resort.Goals.Achieved);
        int expectedReward = GoalTable.Get(GoalId.Cash200k).RewardCash + GoalTable.Get(GoalId.Guests50).RewardCash;
        Assert.True(resort.Economy.Today.Income >= expectedReward || resort.Economy.History.Any(d => d.Income >= expectedReward),
            "goal rewards were never paid");
    }
}

public class BankruptcyTests
{
    [Fact]
    public void ResortGoesBustAfterTooManyDaysInDebt()
    {
        // No guests (zero arrivals) so the ledger is purely upkeep; the nightclub's $520/day
        // upkeep pushes $500 of remaining cash into the red on day one.
        var resort = new Resort(30, 24, 1, new SimConfig
        {
            StartingCash = 14500,
            ArrivalsPerMinuteBase = 0f,
            BankruptcyGraceDays = 2,
        });
        Assert.True(resort.TryBuild("nightclub", 12, 20).Success);
        Assert.Equal(500, resort.Economy.Cash);

        int dayMinutes = resort.Config.DayEndMinute - resort.Config.DayStartMinute;
        for (int i = 0; i < 4 * (dayMinutes + 5) && !resort.Bankrupt; i++) resort.Tick(1f);

        Assert.True(resort.Bankrupt, $"cash {resort.Economy.Cash}, negative days {resort.NegativeCashDays}");
        int visitorsAtBankruptcy = resort.TotalVisitors;
        resort.Tick(100f);
        Assert.Equal(visitorsAtBankruptcy, resort.TotalVisitors); // frozen once bankrupt
    }
}

public class SnapshotTests
{
    [Fact]
    public void SnapshotRoundTripsTheWholeState()
    {
        var resort = new Resort(48, 36, 77, new SimConfig { StartingCash = 300000 });
        resort.TryBuild("beach_bar", 14, 24);
        resort.TryBuild("hotel_block", 30, 20);
        resort.TryBuild("toilets", 20, 24);
        resort.Hire(StaffType.Cleaner);
        var attendant = resort.Hire(StaffType.Attendant);
        resort.AssignAttendant(attendant.Id, resort.Buildings.Values.First(b => b.Def.Id == "beach_bar").Id);
        resort.SetCampaign(CampaignLevel.Starter);
        for (int i = 0; i < 900; i++) resort.Tick(1f);

        var snapshot = resort.CaptureSnapshot();
        var copy = new Resort(48, 36, 1);
        copy.ApplySnapshot(snapshot);

        Assert.Equal(resort.Economy.Cash, copy.Economy.Cash);
        Assert.Equal(resort.Buildings.Count, copy.Buildings.Count);
        Assert.Equal(resort.Tourists.Count, copy.Tourists.Count);
        Assert.Equal(resort.StaffMembers.Count, copy.StaffMembers.Count);
        Assert.Equal(resort.Clock.Day, copy.Clock.Day);
        Assert.Equal(resort.Clock.MinuteOfDay, copy.Clock.MinuteOfDay);
        Assert.Equal(resort.Reputation, copy.Reputation, precision: 3);
        Assert.Equal(resort.CurrentWeather, copy.CurrentWeather);
        Assert.Equal(resort.Campaign, copy.Campaign);
        Assert.Equal(resort.Litter, copy.Litter);
        Assert.Equal(resort.Goals.Achieved.Count, copy.Goals.Achieved.Count);
        foreach (var building in resort.Buildings.Values)
        {
            var restored = copy.Buildings[building.Id];
            Assert.Equal(building.Origin, restored.Origin);
            Assert.Equal(building.Dirtiness, restored.Dirtiness, precision: 4);
            Assert.Equal(building.LongStayers, restored.LongStayers);
        }
        foreach (var tourist in resort.Tourists)
        {
            var restored = copy.Tourists.First(t => t.Id == tourist.Id);
            Assert.Equal(tourist.GuestType, restored.GuestType);
            Assert.Equal(tourist.Money, restored.Money);
            Assert.Equal(tourist.StayDaysRemaining, restored.StayDaysRemaining);
        }
    }

    [Fact]
    public void LoadedResortKeepsRunning()
    {
        var resort = new Resort(40, 30, 9, new SimConfig { StartingCash = 200000 });
        resort.TryBuild("burger_stand", 8, 22);
        resort.TryBuild("beach_bar", 14, 22);
        for (int i = 0; i < 800; i++) resort.Tick(1f);

        var copy = new Resort(40, 30, 1);
        copy.ApplySnapshot(resort.CaptureSnapshot());
        int visitorsBefore = copy.TotalVisitors;
        for (int i = 0; i < 600; i++) copy.Tick(1f);

        Assert.True(copy.TotalVisitors >= visitorsBefore, "loaded resort stopped receiving guests");
        Assert.True(copy.Clock.MinuteOfDay >= resort.Config.DayStartMinute);
    }
}
