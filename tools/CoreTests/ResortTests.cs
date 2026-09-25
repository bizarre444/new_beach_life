using System.Linq;
using BeachLife.Core;
using Xunit;

public class GridMapTests
{
    [Fact]
    public void DefaultBeachHasWaterSandAndGrass()
    {
        var map = GridMap.CreateDefaultBeach(40, 32);
        Assert.Equal(TileType.Water, map.GetTile(0, 0));
        Assert.Contains(TileType.Sand, AllTiles(map));
        Assert.Contains(TileType.Grass, AllTiles(map));
        Assert.False(map.IsWalkable(0, 0));
    }

    [Fact]
    public void OccupiedTilesAreNotWalkableOrPlaceable()
    {
        var map = GridMap.CreateDefaultBeach(20, 20);
        Assert.True(map.CanPlace(5, 15, 2, 2));
        map.Occupy(5, 15, 2, 2, buildingId: 7);
        Assert.Equal(7, map.GetOccupant(6, 16));
        Assert.False(map.IsWalkable(6, 16));
        Assert.False(map.CanPlace(5, 15, 2, 2));

        map.Clear(5, 15, 2, 2);
        Assert.True(map.CanPlace(5, 15, 2, 2));
    }

    private static System.Collections.Generic.IEnumerable<TileType> AllTiles(GridMap map)
    {
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                yield return map.GetTile(x, y);
    }
}

public class PathfinderTests
{
    [Fact]
    public void FindsPathAcrossOpenGround()
    {
        var resort = new Resort(30, 24);
        var path = Pathfinder.FindPath(resort.Map, new GridPos(2, 20), new GridPos(20, 20));
        Assert.NotNull(path);
        Assert.Equal(new GridPos(20, 20), path.Last());
    }

    [Fact]
    public void ReturnsNullWhenTargetIsWater()
    {
        var resort = new Resort(30, 24);
        var path = Pathfinder.FindPath(resort.Map, new GridPos(2, 20), new GridPos(2, 0));
        Assert.Null(path);
    }

    [Fact]
    public void StopsNextToBlockingGoal()
    {
        var resort = new Resort(30, 24);
        var built = resort.TryBuild("toilets", 10, 20);
        Assert.True(built.Success);

        var path = Pathfinder.FindPath(resort.Map, new GridPos(2, 20), built.Building.Center, goalIsBlocking: true);
        Assert.NotNull(path);
        Assert.Equal(1, GridPos.Manhattan(path.Last(), built.Building.Center));
    }
}

public class AccessTileTests
{
    [Fact]
    public void LargeBuildingIsReachableFromOutsideItsFootprint()
    {
        var resort = new Resort(40, 30);
        var pool = resort.TryBuild("pool", 20, 20).Building;
        Assert.NotNull(pool);

        var from = new GridPos(2, 20);
        var access = resort.FindAccessTile(pool, from);
        Assert.NotNull(access);
        Assert.True(resort.Map.IsWalkable(access.Value.X, access.Value.Y));
        Assert.False(pool.Contains(access.Value.X, access.Value.Y));
        Assert.NotNull(Pathfinder.FindPath(resort.Map, from, access.Value));
    }

    [Fact]
    public void GuestsUseLargeVenues()
    {
        var resort = new Resort(40, 30, 5, new SimConfig { StartingCash = 200000 });
        var pool = resort.TryBuild("pool", 20, 20).Building;
        for (int i = 0; i < 4000; i++) resort.Tick(1f);
        Assert.True(pool.Revenue > 0, "guests never reached the 5x4 pool");
    }
}

public class BuildingTests
{
    private static GridPos FirstTile(Resort resort, TileType type)
    {
        for (int y = 0; y < resort.Map.Height; y++)
            for (int x = 0; x < resort.Map.Width; x++)
                if (resort.Map.GetTile(x, y) == type) return new GridPos(x, y);
        throw new System.InvalidOperationException($"no {type} tile on the map");
    }

    [Fact]
    public void BuildingChargesCashAndOccupiesTiles()
    {
        var resort = new Resort(30, 24);
        int before = resort.Economy.Cash;
        var result = resort.TryBuild("beach_bar", 8, 20);

        Assert.True(result.Success);
        Assert.Equal(before - BuildingCatalog.Get("beach_bar").BuildCost, resort.Economy.Cash);
        Assert.Same(result.Building, resort.BuildingAt(9, 20));
    }

    [Fact]
    public void SunLoungerOnlyFitsOnSand()
    {
        var resort = new Resort(30, 24);
        var sandTile = FirstTile(resort, TileType.Sand);
        var grassTile = FirstTile(resort, TileType.Grass);

        Assert.True(resort.TryBuild("sun_lounger", sandTile.X, sandTile.Y).Success);
        Assert.Equal(BuildError.WrongTerrain, resort.TryBuild("sun_lounger", grassTile.X, grassTile.Y).Error);
    }

    [Fact]
    public void OverlappingBuildIsRejected()
    {
        var resort = new Resort(30, 24);
        Assert.True(resort.TryBuild("toilets", 8, 20).Success);
        Assert.Equal(BuildError.BlockedTiles, resort.TryBuild("toilets", 9, 20).Error);
    }

    [Fact]
    public void CannotAffordExpensiveBuilding()
    {
        var config = new SimConfig { StartingCash = 100 };
        var resort = new Resort(30, 24, 1, config);
        Assert.Equal(BuildError.NotEnoughCash, resort.TryBuild("nightclub", 5, 20).Error);
    }

    [Fact]
    public void WallOfBuildingsThatSealsOffAreaIsRejected()
    {
        var resort = new Resort(12, 14, 1, new SimConfig { StartingCash = 500000 });
        // Fence off the bottom strip completely except the very last tile.
        for (int x = 0; x < 11; x++)
            Assert.True(resort.TryBuild("palm_tree", x, 10).Success);

        Assert.Equal(BuildError.WouldTrapGuests, resort.TryBuild("palm_tree", 11, 10).Error);
    }

    [Fact]
    public void DemolishRefundsHalfAndFreesTiles()
    {
        var resort = new Resort(30, 24);
        var result = resort.TryBuild("restaurant", 6, 20);
        int cashAfterBuild = resort.Economy.Cash;

        Assert.True(resort.Demolish(result.Building.Id));
        Assert.Equal(cashAfterBuild + result.Building.Def.BuildCost / 2, resort.Economy.Cash);
        Assert.Null(resort.BuildingAt(6, 20));
    }
}

public class SimulationTests
{
    private static Resort BuildStarterResort(int seed = 7)
    {
        var resort = new Resort(40, 30, seed, new SimConfig { StartingCash = 200000 });
        Assert.True(resort.TryBuild("burger_stand", 6, 22).Success);
        Assert.True(resort.TryBuild("beach_bar", 12, 22).Success);
        Assert.True(resort.TryBuild("toilets", 18, 22).Success);
        Assert.True(resort.TryBuild("showers", 22, 22).Success);
        Assert.True(resort.TryBuild("pool", 26, 20).Success);
        return resort;
    }

    [Fact]
    public void TouristsArriveDuringOpeningHours()
    {
        var resort = BuildStarterResort();
        for (int i = 0; i < 600; i++) resort.Tick(1f);
        Assert.True(resort.Tourists.Count > 0, "expected guests to arrive");
        Assert.True(resort.TotalVisitors > 0);
    }

    [Fact]
    public void GuestsSpendMoneyAtVenues()
    {
        var resort = BuildStarterResort();
        for (int i = 0; i < 2000; i++) resort.Tick(1f);
        Assert.True(resort.Economy.Today.Income > 0 || resort.Economy.History.Any(d => d.Income > 0),
            "expected venue income");
    }

    [Fact]
    public void ClockRollsOverAndChargesUpkeep()
    {
        var resort = BuildStarterResort();
        int dayLengthMinutes = resort.Config.DayEndMinute - resort.Config.DayStartMinute;
        for (int i = 0; i < dayLengthMinutes + 10; i++) resort.Tick(1f);

        Assert.Equal(2, resort.Clock.Day);
        Assert.Single(resort.Economy.History);
        Assert.True(resort.Economy.History[0].Upkeep > 0);
    }

    [Fact]
    public void GuestsWithoutAnyFacilitiesBecomeUnhappyAndLeave()
    {
        var resort = new Resort(30, 24, 3);
        for (int i = 0; i < 3000; i++) resort.Tick(1f);
        Assert.True(resort.TotalWalkouts > 0, "expected angry walkouts with no facilities");
    }

    [Fact]
    public void WellRunResortKeepsGuestsHappierThanEmptyPlot()
    {
        var good = BuildStarterResort(11);
        var empty = new Resort(40, 30, 11, new SimConfig { StartingCash = 200000 });
        for (int i = 0; i < 2500; i++) { good.Tick(1f); empty.Tick(1f); }

        Assert.True(good.Reputation > empty.Reputation,
            $"good={good.Reputation:0.0} empty={empty.Reputation:0.0}");
    }

    [Fact]
    public void RaisingPricesReducesAttractiveness()
    {
        var resort = BuildStarterResort();
        var bar = resort.Buildings.Values.First(b => b.Def.Id == "beach_bar");
        float baseline = bar.Attractiveness();
        bar.PriceMultiplier = 2f;
        Assert.True(bar.Attractiveness() < baseline);
    }
}
