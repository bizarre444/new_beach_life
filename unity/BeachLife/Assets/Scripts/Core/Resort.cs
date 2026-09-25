using System;
using System.Collections.Generic;

namespace BeachLife.Core
{
    public enum BuildError
    {
        None,
        UnknownBuilding,
        BlockedTiles,
        WrongTerrain,
        NotEnoughCash,
        WouldTrapGuests,
    }

    public readonly struct BuildResult
    {
        public readonly BuildError Error;
        public readonly Building Building;

        public BuildResult(BuildError error, Building building = null)
        {
            Error = error;
            Building = building;
        }

        public bool Success => Error == BuildError.None;

        public static BuildResult Fail(BuildError error) => new BuildResult(error);
    }

    /// <summary>
    /// The whole resort simulation: terrain, buildings, guests, money and time.
    /// Engine-agnostic on purpose — the Unity layer only renders its state and forwards input.
    /// </summary>
    public sealed class Resort
    {
        public readonly SimConfig Config;
        public readonly GridMap Map;
        public readonly Economy Economy;
        public readonly SimClock Clock;
        public readonly GridPos Entrance;

        public readonly Dictionary<int, Building> Buildings = new Dictionary<int, Building>();
        public readonly List<Tourist> Tourists = new List<Tourist>();
        public readonly List<string> EventLog = new List<string>();

        public float Reputation { get; private set; }
        public int TotalVisitors { get; private set; }
        public int TotalWalkouts { get; private set; }

        private readonly Random _rng;
        private int _nextBuildingId = 1;
        private int _nextTouristId = 1;
        private float _arrivalAccumulator;

        private static readonly string[] FirstNames =
        {
            "Alex", "Bea", "Chris", "Dana", "Emil", "Fran", "Gus", "Hana",
            "Ivan", "Jo", "Kira", "Luis", "Mia", "Nils", "Olga", "Pim",
        };

        public Resort(int width = 48, int height = 36, int seed = 12345, SimConfig config = null)
        {
            Config = config ?? SimConfig.Default;
            Map = GridMap.CreateDefaultBeach(width, height);
            Economy = new Economy(Config.StartingCash);
            Clock = new SimClock(Config);
            Reputation = Config.StartingReputation;
            Entrance = new GridPos(width / 2, height - 1);
            _rng = new Random(seed);
        }

        public int TouristCapacity =>
            Math.Max(Config.MinTouristCapacity, (int)(Reputation * Config.MaxTouristsPerReputation));

        // ---------------------------------------------------------------- building

        public BuildResult TryBuild(string defId, int x, int y)
        {
            if (!BuildingCatalog.TryGet(defId, out var def))
                return BuildResult.Fail(BuildError.UnknownBuilding);
            return TryBuild(def, x, y);
        }

        public BuildResult TryBuild(BuildingDef def, int x, int y)
        {
            if (!Map.CanPlace(x, y, def.Width, def.Height))
                return BuildResult.Fail(BuildError.BlockedTiles);

            for (int dy = 0; dy < def.Height; dy++)
                for (int dx = 0; dx < def.Width; dx++)
                    if (!def.AllowsTile(Map.GetTile(x + dx, y + dy)))
                        return BuildResult.Fail(BuildError.WrongTerrain);

            if (!Economy.CanAfford(def.BuildCost))
                return BuildResult.Fail(BuildError.NotEnoughCash);

            var building = new Building { Id = _nextBuildingId++, Def = def, Origin = new GridPos(x, y) };
            Map.Occupy(x, y, def.Width, def.Height, building.Id);

            if (!IsEntranceConnected())
            {
                Map.Clear(x, y, def.Width, def.Height);
                _nextBuildingId--;
                return BuildResult.Fail(BuildError.WouldTrapGuests);
            }

            Economy.TrySpendConstruction(def.BuildCost);
            Buildings[building.Id] = building;
            Log($"Built {def.DisplayName} for ${def.BuildCost}");
            return new BuildResult(BuildError.None, building);
        }

        public bool Demolish(int buildingId)
        {
            if (!Buildings.TryGetValue(buildingId, out var building)) return false;
            foreach (var tourist in Tourists)
            {
                if (tourist.TargetBuildingId != buildingId) continue;
                tourist.TargetBuildingId = -1;
                tourist.State = TouristState.Wandering;
                tourist.SetPath(null);
            }
            Map.Clear(building.Origin.X, building.Origin.Y, building.Def.Width, building.Def.Height);
            Buildings.Remove(buildingId);
            Economy.Refund(building.Def.BuildCost / 2);
            Log($"Demolished {building.Def.DisplayName}");
            return true;
        }

        public Building BuildingAt(int x, int y)
        {
            int id = Map.GetOccupant(x, y);
            return id >= 0 && Buildings.TryGetValue(id, out var b) ? b : null;
        }

        /// <summary>Flood fill from the entrance to make sure walkable ground is not cut in two.</summary>
        private bool IsEntranceConnected()
        {
            var start = FindWalkableNear(Entrance);
            if (start == null) return false;

            var seen = new HashSet<GridPos> { start.Value };
            var stack = new Stack<GridPos>();
            stack.Push(start.Value);
            int reached = 0;
            while (stack.Count > 0)
            {
                var p = stack.Pop();
                reached++;
                TryVisit(p.X + 1, p.Y);
                TryVisit(p.X - 1, p.Y);
                TryVisit(p.X, p.Y + 1);
                TryVisit(p.X, p.Y - 1);

                void TryVisit(int nx, int ny)
                {
                    if (!Map.IsWalkable(nx, ny)) return;
                    var np = new GridPos(nx, ny);
                    if (seen.Add(np)) stack.Push(np);
                }
            }

            int walkable = 0;
            for (int y = 0; y < Map.Height; y++)
                for (int x = 0; x < Map.Width; x++)
                    if (Map.IsWalkable(x, y)) walkable++;

            return reached == walkable;
        }

        /// <summary>
        /// Walkable tile next to a building's footprint, closest to <paramref name="from"/>.
        /// Guests path to this tile rather than into the footprint itself.
        /// </summary>
        public GridPos? FindAccessTile(Building building, GridPos from)
        {
            var def = building.Def;
            var origin = building.Origin;
            GridPos? best = null;
            int bestDistance = int.MaxValue;

            void Consider(int x, int y)
            {
                if (!Map.IsWalkable(x, y)) return;
                int distance = GridPos.Manhattan(new GridPos(x, y), from);
                if (distance >= bestDistance) return;
                bestDistance = distance;
                best = new GridPos(x, y);
            }

            for (int dx = -1; dx <= def.Width; dx++)
            {
                Consider(origin.X + dx, origin.Y - 1);
                Consider(origin.X + dx, origin.Y + def.Height);
            }
            for (int dy = -1; dy <= def.Height; dy++)
            {
                Consider(origin.X - 1, origin.Y + dy);
                Consider(origin.X + def.Width, origin.Y + dy);
            }
            return best;
        }

        private GridPos? FindWalkableNear(GridPos pos)
        {
            for (int radius = 0; radius < 6; radius++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int x = pos.X + dx, y = pos.Y + dy;
                        if (Map.IsWalkable(x, y)) return new GridPos(x, y);
                    }
                }
            }
            return null;
        }

        // ---------------------------------------------------------------- simulation

        public void Tick(float realSeconds)
        {
            float minutes = Clock.Advance(realSeconds, out bool dayRolled);
            if (minutes <= 0f) return;

            SpawnArrivals(minutes);

            for (int i = Tourists.Count - 1; i >= 0; i--)
            {
                var tourist = Tourists[i];
                UpdateTourist(tourist, minutes);
                if (tourist.State == TouristState.Gone)
                {
                    OnTouristLeft(tourist);
                    Tourists.RemoveAt(i);
                }
            }

            foreach (var building in Buildings.Values)
            {
                if (building.Occupants.Count > 0)
                    building.Dirtiness = Needs.Clamp01(building.Dirtiness + 0.0006f * minutes * building.Occupants.Count);
            }

            if (dayRolled) CloseDay();
        }

        private void SpawnArrivals(float minutes)
        {
            if (!Clock.IsOpen || Clock.Hour >= 22) return;
            if (Tourists.Count >= TouristCapacity) return;

            float rate = Config.ArrivalsPerMinuteBase * (0.4f + Reputation / 100f);
            _arrivalAccumulator += rate * minutes;
            while (_arrivalAccumulator >= 1f && Tourists.Count < TouristCapacity)
            {
                _arrivalAccumulator -= 1f;
                SpawnTourist();
            }
        }

        private Tourist SpawnTourist()
        {
            var spawn = FindWalkableNear(Entrance) ?? Entrance;
            var tourist = new Tourist
            {
                Id = _nextTouristId++,
                Name = FirstNames[_rng.Next(FirstNames.Length)] + " " + (char)('A' + _rng.Next(26)) + ".",
                X = spawn.X,
                Y = spawn.Y,
                Needs = Needs.CreateFresh(_rng),
                Money = _rng.Next(Config.TouristStartMoneyMin, Config.TouristStartMoneyMax),
                Mood = 0.6f + (float)_rng.NextDouble() * 0.2f,
                State = TouristState.Arriving,
            };
            Tourists.Add(tourist);
            TotalVisitors++;
            Economy.Today.Visitors++;
            return tourist;
        }

        private void UpdateTourist(Tourist tourist, float minutes)
        {
            if (tourist.State != TouristState.UsingBuilding)
                tourist.Needs.Decay(Config, minutes);

            UpdateMood(tourist, minutes);

            switch (tourist.State)
            {
                case TouristState.Arriving:
                    tourist.State = TouristState.Wandering;
                    break;

                case TouristState.Wandering:
                    if (ShouldGoHome(tourist)) { BeginLeaving(tourist); break; }
                    if (!TryChooseDestination(tourist)) Stroll(tourist, minutes);
                    break;

                case TouristState.WalkingToBuilding:
                {
                    if (!Buildings.TryGetValue(tourist.TargetBuildingId, out var target))
                    {
                        tourist.State = TouristState.Wandering;
                        break;
                    }
                    if (tourist.StepAlongPath(Config.TouristWalkSpeed * minutes))
                    {
                        if (target.HasFreeSlot) EnterBuilding(tourist, target);
                        else
                        {
                            target.Waiting.Enqueue(tourist.Id);
                            tourist.State = TouristState.Queueing;
                            tourist.WaitedMinutes = 0f;
                        }
                    }
                    break;
                }

                case TouristState.Queueing:
                {
                    if (!Buildings.TryGetValue(tourist.TargetBuildingId, out var queued))
                    {
                        tourist.State = TouristState.Wandering;
                        break;
                    }
                    tourist.WaitedMinutes += minutes;
                    tourist.Mood = Needs.Clamp01(tourist.Mood - Config.MoodLossPerQueueMinute * minutes);
                    if (queued.HasFreeSlot && queued.Waiting.Count > 0 && queued.Waiting.Peek() == tourist.Id)
                    {
                        queued.Waiting.Dequeue();
                        EnterBuilding(tourist, queued);
                    }
                    else if (tourist.WaitedMinutes > 30f)
                    {
                        tourist.State = TouristState.Wandering;
                        tourist.TargetBuildingId = -1;
                        tourist.Mood = Needs.Clamp01(tourist.Mood - 0.08f);
                    }
                    break;
                }

                case TouristState.UsingBuilding:
                {
                    tourist.ServiceRemaining -= minutes;
                    if (tourist.ServiceRemaining <= 0f) ExitBuilding(tourist);
                    break;
                }

                case TouristState.Leaving:
                    if (tourist.StepAlongPath(Config.TouristWalkSpeed * minutes))
                        tourist.State = TouristState.Gone;
                    break;
            }
        }

        private void UpdateMood(Tourist tourist, float minutes)
        {
            int critical = tourist.Needs.CountBelow(Config.CriticalNeedThreshold);
            if (critical > 0)
                tourist.Mood -= Config.MoodLossPerCriticalNeedMinute * critical * minutes;
            else if (tourist.Needs.Average() > 0.6f)
                tourist.Mood += Config.MoodGainPerSatisfiedMinute * minutes;

            tourist.Mood += AmbienceAt(tourist.Tile) * minutes * 0.05f;
            tourist.Mood = Needs.Clamp01(tourist.Mood);
        }

        public float AmbienceAt(GridPos pos)
        {
            float total = 0f;
            foreach (var building in Buildings.Values)
            {
                if (building.Def.AmbienceBonus <= 0f) continue;
                int distance = GridPos.Manhattan(pos, building.Center);
                if (distance <= building.Def.AmbienceRadius)
                    total += building.Def.AmbienceBonus * (1f - distance / (float)(building.Def.AmbienceRadius + 1));
            }
            return total;
        }

        private bool ShouldGoHome(Tourist tourist)
        {
            if (tourist.Mood <= Config.MoodLeaveThreshold) return true;
            return Clock.MinuteOfDay > Config.DayEndMinute - 90f;
        }

        private void BeginLeaving(Tourist tourist)
        {
            tourist.State = TouristState.Leaving;
            tourist.SetPath(Pathfinder.FindPath(Map, tourist.Tile, Entrance));
            if (!tourist.HasPath) tourist.State = TouristState.Gone;
        }

        private bool TryChooseDestination(Tourist tourist)
        {
            var need = tourist.Needs.MostUrgent(Config.UrgentNeedThreshold);
            if (need == null) return false;

            Building best = null;
            float bestScore = 0f;
            List<GridPos> bestPath = null;

            foreach (var building in Buildings.Values)
            {
                if (!building.Def.CanSatisfy(need.Value)) continue;
                if (building.Def.Capacity <= 0) continue;
                if (building.Price > tourist.Money) continue;

                int distance = GridPos.Manhattan(tourist.Tile, building.Center);
                float score = building.Def.Satisfies[need.Value] * building.Attractiveness() / (1f + distance * 0.06f);
                if (score <= bestScore) continue;

                var access = FindAccessTile(building, tourist.Tile);
                if (access == null) continue;
                var path = Pathfinder.FindPath(Map, tourist.Tile, access.Value);
                if (path == null) continue;

                best = building;
                bestScore = score;
                bestPath = path;
            }

            if (best == null)
            {
                if (tourist.Needs.Get(need.Value) < Config.CriticalNeedThreshold)
                    tourist.Mood = Needs.Clamp01(tourist.Mood - 0.01f);
                return false;
            }

            tourist.TargetBuildingId = best.Id;
            tourist.PursuedNeed = need;
            tourist.SetPath(bestPath);
            tourist.State = TouristState.WalkingToBuilding;
            return true;
        }

        private void Stroll(Tourist tourist, float minutes)
        {
            if (tourist.HasPath)
            {
                tourist.StepAlongPath(Config.TouristWalkSpeed * minutes);
                return;
            }
            for (int attempt = 0; attempt < 8; attempt++)
            {
                int x = tourist.Tile.X + _rng.Next(-6, 7);
                int y = tourist.Tile.Y + _rng.Next(-6, 7);
                if (!Map.IsWalkable(x, y)) continue;
                var path = Pathfinder.FindPath(Map, tourist.Tile, new GridPos(x, y));
                if (path == null || path.Count == 0) continue;
                tourist.SetPath(path);
                return;
            }
        }

        private void EnterBuilding(Tourist tourist, Building building)
        {
            building.Occupants.Add(tourist.Id);
            tourist.State = TouristState.UsingBuilding;
            tourist.ServiceRemaining = building.Def.ServiceMinutes;
            tourist.X = building.Center.X;
            tourist.Y = building.Center.Y;

            int price = building.Price;
            if (price > 0)
            {
                tourist.Money -= price;
                tourist.Spent += price;
                building.Revenue += price;
                Economy.Earn(price);
                if (building.PriceMultiplier > 1.4f)
                    tourist.Mood = Needs.Clamp01(tourist.Mood - 0.03f);
            }
        }

        private void ExitBuilding(Tourist tourist)
        {
            if (Buildings.TryGetValue(tourist.TargetBuildingId, out var building))
            {
                building.Occupants.Remove(tourist.Id);
                foreach (var pair in building.Def.Satisfies)
                    tourist.Needs.Add(pair.Key, pair.Value);

                tourist.Mood = Needs.Clamp01(tourist.Mood + 0.04f * building.Attractiveness());

                var exit = FindWalkableNear(building.Center);
                if (exit != null) { tourist.X = exit.Value.X; tourist.Y = exit.Value.Y; }
            }
            tourist.TargetBuildingId = -1;
            tourist.PursuedNeed = null;
            tourist.SetPath(null);
            tourist.State = TouristState.Wandering;
        }

        private void OnTouristLeft(Tourist tourist)
        {
            bool angry = tourist.Mood <= Config.MoodLeaveThreshold + 0.05f;
            if (angry)
            {
                TotalWalkouts++;
                Economy.Today.LeftAngry++;
            }
            float target = tourist.Mood * 100f;
            Reputation += (target - Reputation) * Config.ReputationSmoothing;
            Reputation = Math.Max(1f, Math.Min(100f, Reputation));
        }

        private void CloseDay()
        {
            int upkeep = 0;
            foreach (var building in Buildings.Values)
            {
                upkeep += building.Def.DailyUpkeep;
                building.Dirtiness = Needs.Clamp01(building.Dirtiness - 0.35f);
            }
            Economy.PayUpkeep(upkeep);
            var ledger = Economy.CloseDay();
            Log($"Day {ledger.Day} closed: income ${ledger.Income}, upkeep ${ledger.Upkeep}, profit ${ledger.Profit}");
        }

        private void Log(string message)
        {
            EventLog.Add($"[{Clock.TimeLabel}] {message}");
            if (EventLog.Count > 200) EventLog.RemoveAt(0);
        }
    }
}
