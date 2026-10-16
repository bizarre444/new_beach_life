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
    /// The whole resort simulation: terrain, buildings, guests, staff, money, weather and time.
    /// Engine-agnostic on purpose — the Unity layer only renders its state and forwards input.
    /// </summary>
    public sealed class Resort
    {
        public readonly SimConfig Config;
        public readonly GridMap Map;
        public readonly Economy Economy;
        public readonly SimClock Clock;
        public readonly GridPos Entrance;
        public readonly Goals Goals = new Goals();

        public readonly Dictionary<int, Building> Buildings = new Dictionary<int, Building>();
        public readonly List<Tourist> Tourists = new List<Tourist>();
        public readonly List<Staff> StaffMembers = new List<Staff>();
        public readonly List<string> EventLog = new List<string>();

        /// <summary>Litter per tile, 0 is clean; bins, cleaners and rain fight it.</summary>
        public readonly float[] Litter;

        public WeatherType CurrentWeather { get; private set; } = WeatherType.Sunny;
        public WeatherType ForecastWeather { get; private set; } = WeatherType.Cloudy;
        public CampaignLevel Campaign { get; private set; } = CampaignLevel.None;

        public float Reputation { get; private set; }
        public int TotalVisitors { get; private set; }
        public int TotalWalkouts { get; private set; }
        /// <summary>Consecutive closed days ending in the red; at <see cref="SimConfig.BankruptcyGraceDays"/> the resort goes bust.</summary>
        public int NegativeCashDays { get; private set; }
        public bool Bankrupt { get; private set; }

        public WeatherInfo Weather => WeatherTable.Get(CurrentWeather);
        public CampaignInfo CampaignInfo => CampaignTable.Get(Campaign);

        private readonly Random _rng;
        private int _nextBuildingId = 1;
        private int _nextTouristId = 1;
        private int _nextStaffId = 1;
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
            Litter = new float[width * height];
            _rng = new Random(seed);
            CurrentWeather = RollWeather();
            ForecastWeather = RollWeather();
        }

        public int TouristCapacity =>
            Math.Max(Config.MinTouristCapacity, (int)(Reputation * Config.MaxTouristsPerReputation));

        public bool HasOvernightGuests
        {
            get
            {
                foreach (var tourist in Tourists)
                    if (tourist.StayDaysRemaining > 0) return true;
                foreach (var building in Buildings.Values)
                    if (building.LongStayers.Count > 0) return true;
                return false;
            }
        }

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

            var building = new Building { Id = _nextBuildingId, Def = def, Origin = new GridPos(x, y) };
            Map.Occupy(x, y, def.Width, def.Height, building.Id);

            if (!IsEntranceConnected())
            {
                Map.Clear(x, y, def.Width, def.Height);
                return BuildResult.Fail(BuildError.WouldTrapGuests);
            }

            Economy.TrySpendConstruction(def.BuildCost);
            Buildings[building.Id] = building;
            _nextBuildingId++;
            Log($"Built {def.DisplayName} for ${def.BuildCost}");
            return new BuildResult(BuildError.None, building);
        }

        public bool Demolish(int buildingId)
        {
            if (!Buildings.TryGetValue(buildingId, out var building)) return false;
            foreach (var tourist in Tourists)
            {
                if (tourist.TargetBuildingId == buildingId)
                {
                    tourist.TargetBuildingId = -1;
                    tourist.HeadingToBed = false;
                    tourist.Sleeping = false;
                    tourist.State = TouristState.Wandering;
                    tourist.SetPath(null);
                }
                if (tourist.HotelBuildingId == buildingId)
                {
                    // The booked hotel is gone: overnight guests become disappointed day visitors.
                    tourist.HotelBuildingId = -1;
                    tourist.StayDaysRemaining = 0;
                    tourist.Mood = Needs.Clamp01(tourist.Mood - 0.2f);
                }
            }
            foreach (var member in StaffMembers)
                if (member.AssignedBuildingId == buildingId)
                    member.AssignedBuildingId = -1;
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

        // ---------------------------------------------------------------- staff

        public bool CanHire(StaffType type) => Economy.CanAfford(StaffTable.Get(type).HireFee);

        public Staff Hire(StaffType type)
        {
            var info = StaffTable.Get(type);
            if (!Economy.TrySpendConstruction(info.HireFee)) return null;

            var spawn = FindWalkableNear(Entrance) ?? Entrance;
            var member = new Staff
            {
                Id = _nextStaffId++,
                Type = type,
                Name = $"{info.DisplayName} #{_nextStaffId - 1}",
                X = spawn.X,
                Y = spawn.Y,
                State = StaffState.Idle,
            };
            StaffMembers.Add(member);
            Log($"Hired a {info.DisplayName} (${info.HireFee:n0} fee, ${info.WagePerDay}/day)");
            return member;
        }

        public bool Fire(int staffId)
        {
            for (int i = 0; i < StaffMembers.Count; i++)
            {
                if (StaffMembers[i].Id != staffId) continue;
                Log($"Fired {StaffMembers[i].Name}");
                StaffMembers.RemoveAt(i);
                return true;
            }
            return false;
        }

        /// <summary>Points an attendant at a venue; each venue takes at most one attendant.</summary>
        public bool AssignAttendant(int staffId, int buildingId)
        {
            Staff member = null;
            foreach (var candidate in StaffMembers)
                if (candidate.Id == staffId && candidate.Type == StaffType.Attendant) { member = candidate; break; }
            if (member == null || !Buildings.ContainsKey(buildingId)) return false;
            foreach (var other in StaffMembers)
                if (other != member && other.AssignedBuildingId == buildingId)
                    other.AssignedBuildingId = -1;
            member.AssignedBuildingId = buildingId;
            return true;
        }

        public Staff AttendantFor(Building building)
        {
            foreach (var member in StaffMembers)
                if (member.Type == StaffType.Attendant && member.AssignedBuildingId == building.Id)
                    return member;
            return null;
        }

        public int EffectiveCapacity(Building building) =>
            building.Def.Capacity + (AttendantFor(building) != null ? Config.AttendantCapacityBonus : 0);

        public bool HasFreeSlot(Building building) =>
            building.Occupants.Count + building.LongStayers.Count < EffectiveCapacity(building);

        // ---------------------------------------------------------------- litter

        public float LitterAt(int x, int y) => Map.InBounds(x, y) ? Litter[y * Map.Width + x] : 0f;

        public bool BinCovers(GridPos pos)
        {
            foreach (var building in Buildings.Values)
            {
                if (building.Def.Id != "litter_bin") continue;
                if (GridPos.Manhattan(pos, building.Center) <= Config.BinCoverageRadius) return true;
            }
            return false;
        }

        /// <summary>Average litter around a venue; guests shun filthy surroundings.</summary>
        public float NearbyLitter(Building building)
        {
            float total = 0f;
            int count = 0;
            for (int dy = -1; dy <= building.Def.Height; dy++)
            {
                for (int dx = -1; dx <= building.Def.Width; dx++)
                {
                    total += LitterAt(building.Origin.X + dx, building.Origin.Y + dy);
                    count++;
                }
            }
            return count > 0 ? total / count : 0f;
        }

        private void DropLitter(Tourist tourist, float minutes)
        {
            float chance = Config.LitterChancePerMinute * tourist.LitterMultiplier * minutes;
            if (BinCovers(tourist.Tile)) chance *= Config.BinLitterReduction;
            if (_rng.NextDouble() >= chance) return;
            int index = tourist.Tile.Y * Map.Width + tourist.Tile.X;
            Litter[index] = Math.Min(Config.LitterMaxPerTile, Litter[index] + 0.25f);
        }

        private void DecayLitter(float minutes)
        {
            float decay = Config.LitterNaturalDecayPerMinute * minutes;
            for (int i = 0; i < Litter.Length; i++)
                if (Litter[i] > 0f) Litter[i] = Math.Max(0f, Litter[i] - decay);
        }

        // ---------------------------------------------------------------- simulation

        public void Tick(float realSeconds)
        {
            if (Bankrupt) return;

            float minutes = Clock.Advance(realSeconds, out bool dayRolled);
            if (minutes <= 0f) return;

            if (dayRolled) OnNewDay();

            SpawnArrivals(minutes);
            DecayLitter(minutes);

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

            UpdateStaff(minutes);
            UpdateSafety(minutes);
            Goals.Evaluate(this);
        }

        private void OnNewDay()
        {
            foreach (var tourist in Tourists)
                if (tourist.StayDaysRemaining > 0) tourist.StayDaysRemaining--;

            CurrentWeather = ForecastWeather;
            ForecastWeather = RollWeather();
            if (CurrentWeather == WeatherType.Rainy)
                for (int i = 0; i < Litter.Length; i++) Litter[i] *= 0.75f;

            CloseDay();
        }

        private WeatherType RollWeather()
        {
            int roll = _rng.Next(100);
            if (roll < 40) return WeatherType.Sunny;
            if (roll < 55) return WeatherType.Hot;
            if (roll < 80) return WeatherType.Cloudy;
            return WeatherType.Rainy;
        }

        public void SetCampaign(CampaignLevel level)
        {
            if (Campaign == level) return;
            Campaign = level;
            Log(Campaign == CampaignLevel.None
                ? "Marketing campaign stopped"
                : $"Started {CampaignTable.Get(level).DisplayName} ({CampaignTable.Get(level).CostPerDay:n0}/day)");
        }

        private void SpawnArrivals(float minutes)
        {
            if (!Clock.IsOpen || Clock.Hour >= 22) return;
            if (Tourists.Count >= TouristCapacity) return;

            float rate = Config.ArrivalsPerMinuteBase * (0.4f + Reputation / 100f);
            rate *= Weather.ArrivalMultiplier * CampaignInfo.ArrivalMultiplier;
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

            var type = RollGuestType();
            var tourist = new Tourist
            {
                Id = _nextTouristId++,
                Name = FirstNames[_rng.Next(FirstNames.Length)] + " " + (char)('A' + _rng.Next(26)) + ".",
                GuestType = type,
                X = spawn.X,
                Y = spawn.Y,
                Needs = Needs.CreateFresh(_rng),
                Mood = 0.6f + (float)_rng.NextDouble() * 0.2f,
                State = TouristState.Arriving,
            };
            switch (type)
            {
                case GuestType.Budget:
                    tourist.Money = _rng.Next(40, 110);
                    break;
                case GuestType.Vip:
                    tourist.Money = _rng.Next(180, 600);
                    break;
                default:
                    tourist.Money = _rng.Next(Config.TouristStartMoneyMin, Config.TouristStartMoneyMax);
                    break;
            }

            TryBookHotel(tourist);
            Tourists.Add(tourist);
            TotalVisitors++;
            Economy.Today.Visitors++;
            return tourist;
        }

        private GuestType RollGuestType()
        {
            int roll = _rng.Next(100);
            if (roll < 30) return GuestType.Budget;
            if (roll < 80) return GuestType.Regular;
            return GuestType.Vip;
        }

        /// <summary>Reserves a hotel bed on spawn so overnight guests are never locked out at night.</summary>
        private void TryBookHotel(Tourist tourist)
        {
            if (_rng.NextDouble() >= Config.HotelGuestChance) return;

            Building hotel = null;
            foreach (var building in Buildings.Values)
            {
                if (building.Def.Category != BuildingCategory.Accommodation) continue;
                if (!HasFreeSlot(building)) continue;
                hotel = building;
                if (_rng.NextDouble() < 0.5f) break;
            }
            if (hotel == null) return;

            tourist.StayDaysRemaining = _rng.Next(1, Config.MaxStayDays + 1);
            tourist.HotelBuildingId = hotel.Id;
            hotel.LongStayers.Add(tourist.Id);
            tourist.Money += 60 * tourist.StayDaysRemaining; // holiday budget on top
        }

        private void UpdateTourist(Tourist tourist, float minutes)
        {
            if (tourist.State != TouristState.UsingBuilding)
            {
                tourist.Needs.Decay(Config, minutes);
                var weather = Weather;
                tourist.Needs.Set(NeedType.Thirst,
                    tourist.Needs.Get(NeedType.Thirst) - Config.NeedDecay[(int)NeedType.Thirst] * (weather.ThirstMultiplier - 1f) * minutes);
                tourist.Needs.Set(NeedType.Tan,
                    tourist.Needs.Get(NeedType.Tan) - Config.NeedDecay[(int)NeedType.Tan] * (weather.TanMultiplier - 1f) * minutes);
                DropLitter(tourist, minutes);
            }

            UpdateMood(tourist, minutes);

            switch (tourist.State)
            {
                case TouristState.Arriving:
                    tourist.State = TouristState.Wandering;
                    break;

                case TouristState.Wandering:
                    if (ShouldGoHome(tourist)) { BeginLeaving(tourist); break; }
                    if (WantsToSleep(tourist) && TryGoToBed(tourist)) break;
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
                        if (tourist.HeadingToBed || HasFreeSlot(target)) EnterBuilding(tourist, target, tourist.HeadingToBed);
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
                    if (queued.Waiting.Count > 0 && queued.Waiting.Peek() == tourist.Id && HasFreeSlot(queued))
                    {
                        queued.Waiting.Dequeue();
                        EnterBuilding(tourist, queued, false);
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
                    var venue = FindBuilding(tourist.TargetBuildingId);
                    float speed = venue != null && AttendantFor(venue) != null
                        ? Config.AttendantServiceMultiplier : 1f;
                    tourist.ServiceRemaining -= minutes * speed;
                    if (tourist.ServiceRemaining <= 0f) ExitBuilding(tourist);
                    break;
                }

                case TouristState.Leaving:
                    if (tourist.StepAlongPath(Config.TouristWalkSpeed * minutes))
                        tourist.State = TouristState.Gone;
                    break;
            }
        }

        private Building FindBuilding(int id) =>
            id >= 0 && Buildings.TryGetValue(id, out var building) ? building : null;

        private bool WantsToSleep(Tourist tourist) =>
            tourist.StayDaysRemaining > 0
            && Clock.MinuteOfDay >= 20 * 60
            && tourist.Needs.Get(NeedType.Energy) < Config.SleepEnergyThreshold;

        /// <summary>Walks an overnight guest to their booked hotel for the night.</summary>
        private bool TryGoToBed(Tourist tourist)
        {
            var hotel = FindBuilding(tourist.HotelBuildingId);
            if (hotel == null)
            {
                tourist.HotelBuildingId = -1;
                return false;
            }
            var access = FindAccessTile(hotel, tourist.Tile);
            if (access == null) return false;
            var path = Pathfinder.FindPath(Map, tourist.Tile, access.Value);
            if (path == null) return false;

            tourist.TargetBuildingId = hotel.Id;
            tourist.PursuedNeed = NeedType.Energy;
            tourist.HeadingToBed = true;
            tourist.SetPath(path);
            tourist.State = TouristState.WalkingToBuilding;
            return true;
        }

        private float ComputeSleepMinutes()
        {
            float untilClosing = Config.DayEndMinute - Clock.MinuteOfDay;
            float untilWake = Config.WakeUpMinute - Config.DayStartMinute;
            return Math.Max(60f, untilClosing + untilWake);
        }

        private void UpdateMood(Tourist tourist, float minutes)
        {
            float weatherMood = Weather.MoodMultiplier;
            int critical = tourist.Needs.CountBelow(Config.CriticalNeedThreshold);
            if (critical > 0)
                tourist.Mood -= Config.MoodLossPerCriticalNeedMinute * critical * minutes * weatherMood;
            else if (tourist.Needs.Average() > 0.6f)
                tourist.Mood += Config.MoodGainPerSatisfiedMinute * minutes * weatherMood;

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

            float litter = 0f;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    litter += LitterAt(pos.X + dx, pos.Y + dy);
            total -= litter / 9f * Config.MoodLossPerLitter;
            return total;
        }

        private bool ShouldGoHome(Tourist tourist)
        {
            bool hasHotel = tourist.HotelBuildingId >= 0 && Buildings.ContainsKey(tourist.HotelBuildingId);
            if (tourist.StayDaysRemaining > 0 && hasHotel)
                return tourist.Mood <= Config.MoodLeaveThreshold; // overnight guests only flee when miserable
            if (Clock.MinuteOfDay > Config.DayEndMinute - 90f) return true;
            return tourist.Mood <= Config.MoodLeaveThreshold;
        }

        private void BeginLeaving(Tourist tourist)
        {
            tourist.State = TouristState.Leaving;
            tourist.HeadingToBed = false;
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
                if (EffectiveCapacity(building) <= 0) continue;
                if (building.Price > tourist.Money) continue;

                int distance = GridPos.Manhattan(tourist.Tile, building.Center);
                float score = building.Def.Satisfies[need.Value] * building.Attractiveness() / (1f + distance * 0.06f);

                if (building.PriceMultiplier > tourist.PriceTolerance)
                    score *= Config.OverpricedScorePenalty;
                score *= 1f - Config.LitterVenuePenalty * (NearbyLitter(building) / Config.LitterMaxPerTile);
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
            tourist.HeadingToBed = false;
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

        private void EnterBuilding(Tourist tourist, Building building, bool sleeping)
        {
            building.Occupants.Add(tourist.Id);
            tourist.State = TouristState.UsingBuilding;
            tourist.X = building.Center.X;
            tourist.Y = building.Center.Y;

            int price = building.Price;
            int paid = sleeping ? Math.Min(price, tourist.Money) : price;
            if (paid > 0)
            {
                tourist.Money -= paid;
                tourist.Spent += paid;
                building.Revenue += paid;
                Economy.Earn(paid);
                if (building.PriceMultiplier > 1.4f)
                    tourist.Mood = Needs.Clamp01(tourist.Mood - 0.03f);
            }

            if (sleeping)
            {
                tourist.Sleeping = true;
                tourist.ServiceRemaining = ComputeSleepMinutes();
            }
            else
            {
                tourist.ServiceRemaining = building.Def.ServiceMinutes;
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

                bool sleptHere = tourist.Sleeping && building.Def.Category == BuildingCategory.Accommodation;
                tourist.Sleeping = false;
                tourist.HeadingToBed = false;

                if (sleptHere)
                {
                    if (tourist.StayDaysRemaining <= 0 || tourist.Mood <= Config.MoodLeaveThreshold)
                    {
                        tourist.TargetBuildingId = -1;
                        tourist.PursuedNeed = null;
                        tourist.SetPath(null);
                        BeginLeaving(tourist);
                        return;
                    }
                    Log($"{tourist.Name} woke up at the {building.Def.DisplayName}");
                }
            }
            tourist.TargetBuildingId = -1;
            tourist.PursuedNeed = null;
            tourist.SetPath(null);
            tourist.State = TouristState.Wandering;
        }

        private void OnTouristLeft(Tourist tourist)
        {
            if (tourist.HotelBuildingId >= 0 && Buildings.TryGetValue(tourist.HotelBuildingId, out var hotel))
                hotel.LongStayers.Remove(tourist.Id);

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

        // ---------------------------------------------------------------- staff & safety

        private void UpdateStaff(float minutes)
        {
            foreach (var member in StaffMembers)
            {
                switch (member.State)
                {
                    case StaffState.Idle:
                        AssignNextTask(member);
                        break;

                    case StaffState.Walking:
                        if (member.StepAlongPath(Config.StaffWalkSpeed * minutes))
                            member.State = StaffState.Working;
                        break;

                    case StaffState.Working:
                        member.WorkRemaining -= minutes;
                        if (member.WorkRemaining > 0f) break;
                        CompleteTask(member);
                        member.State = StaffState.Idle;
                        break;
                }
            }
        }

        private void AssignNextTask(Staff member)
        {
            if (member.Type != StaffType.Cleaner) return;

            Building dirtiest = null;
            foreach (var building in Buildings.Values)
            {
                if (building.Dirtiness < 0.3f) continue;
                if (dirtiest == null || building.Dirtiness > dirtiest.Dirtiness) dirtiest = building;
            }
            if (dirtiest != null)
            {
                var spot = FindWalkableNear(dirtiest.Center);
                if (spot != null)
                {
                    var path = Pathfinder.FindPath(Map, member.Tile, spot.Value);
                    if (path != null)
                    {
                        member.WorkTile = spot.Value;
                        member.TargetBuildingId = dirtiest.Id;
                        member.WorkRemaining = dirtiest.Dirtiness * Config.CleanerVenueMinutes;
                        member.SetPath(path);
                        member.State = StaffState.Walking;
                        return;
                    }
                }
            }

            int bestIndex = -1;
            float bestLitter = 0.2f;
            for (int i = 0; i < Litter.Length; i++)
            {
                if (Litter[i] <= bestLitter) continue;
                bestLitter = Litter[i];
                bestIndex = i;
            }
            if (bestIndex < 0) return;

            var target = new GridPos(bestIndex % Map.Width, bestIndex / Map.Width);
            var stand = FindWalkableNear(target);
            if (stand == null) return;
            var litterPath = Pathfinder.FindPath(Map, member.Tile, stand.Value);
            if (litterPath == null) return;

            member.WorkTile = target;
            member.TargetBuildingId = -1;
            member.WorkRemaining = Config.CleanerTileMinutes;
            member.SetPath(litterPath);
            member.State = StaffState.Walking;
        }

        private void CompleteTask(Staff member)
        {
            if (member.Type != StaffType.Cleaner) return;
            var building = FindBuilding(member.TargetBuildingId);
            member.TargetBuildingId = -1;
            if (building != null && building.Dirtiness > 0.2f)
            {
                building.Dirtiness = 0f;
                return;
            }
            int index = member.WorkTile.Y * Map.Width + member.WorkTile.X;
            if (index >= 0 && index < Litter.Length) Litter[index] = 0f;
        }

        private void UpdateSafety(float minutes)
        {
            foreach (var tourist in Tourists)
            {
                if (tourist.State == TouristState.UsingBuilding) continue;
                if (Map.GetTile(tourist.Tile.X, tourist.Tile.Y) != TileType.Sand) continue;
                if (_rng.NextDouble() >= Config.BeachIncidentChancePerGuestMinute * minutes) continue;

                bool covered = false;
                foreach (var building in Buildings.Values)
                {
                    if (!building.Def.ProvidesSafety) continue;
                    if (GridPos.Manhattan(tourist.Tile, building.Center) <= Config.LifeguardCoverageRadius)
                    {
                        covered = true;
                        break;
                    }
                }
                if (covered) continue;

                tourist.Mood = Needs.Clamp01(tourist.Mood - Config.IncidentMoodPenalty);
                Reputation = Math.Max(1f, Reputation - Config.IncidentReputationLoss);
                Log($"Accident on the beach near {tourist.Name} - no lifeguard in sight!");
            }
        }

        // ---------------------------------------------------------------- day & money

        private void CloseDay()
        {
            int upkeep = 0;
            foreach (var building in Buildings.Values)
            {
                upkeep += building.Def.DailyUpkeep;
                building.Dirtiness = Needs.Clamp01(building.Dirtiness - 0.35f);
            }
            Economy.PayUpkeep(upkeep);

            int wages = 0;
            foreach (var member in StaffMembers) wages += member.WagePerDay;
            Economy.PayWages(wages);
            Economy.PayMarketing(CampaignInfo.CostPerDay);

            var ledger = Economy.CloseDay();
            Log($"Day {ledger.Day} closed: income ${ledger.Income:n0}, upkeep ${ledger.Upkeep:n0}, "
                + $"wages ${ledger.Wages:n0}, marketing ${ledger.Marketing:n0}, profit ${ledger.Profit:n0}");

            if (Economy.Cash < 0)
            {
                NegativeCashDays++;
                if (!Bankrupt && NegativeCashDays >= Config.BankruptcyGraceDays)
                {
                    Bankrupt = true;
                    Log("The resort is bankrupt! Time to start over.");
                    return;
                }
                Log($"The books are in the red ({NegativeCashDays}/{Config.BankruptcyGraceDays}) - turn a profit soon!");
            }
            else
            {
                NegativeCashDays = 0;
            }
        }

        private void Log(string message)
        {
            EventLog.Add($"[{Clock.TimeLabel}] {message}");
            if (EventLog.Count > 200) EventLog.RemoveAt(0);
        }

        // ---------------------------------------------------------------- connectivity

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

        // ---------------------------------------------------------------- save / load

        public ResortSnapshot CaptureSnapshot()
        {
            var snapshot = new ResortSnapshot
            {
                Seed = 0,
                Reputation = Reputation,
                TotalVisitors = TotalVisitors,
                TotalWalkouts = TotalWalkouts,
                NegativeCashDays = NegativeCashDays,
                Bankrupt = Bankrupt,
                Clock = new ClockSnapshot { Day = Clock.Day, MinuteOfDay = Clock.MinuteOfDay, Speed = Clock.Speed },
                Map = new MapSnapshot { Width = Map.Width, Height = Map.Height, Tiles = Map.ExportTiles() },
                Weather = new WeatherSnapshot { Current = (int)CurrentWeather, Forecast = (int)ForecastWeather },
                Marketing = new MarketingSnapshot { Level = (int)Campaign },
                Litter = (float[])Litter.Clone(),
            };
            foreach (var pair in Goals.Achieved) snapshot.Goals.Achieved.Add((int)pair);
            foreach (var ledger in Economy.History)
                snapshot.Economy.History.Add(LedgerSnapshotOf(ledger));
            snapshot.Economy.Cash = Economy.Cash;
            snapshot.Economy.Today = LedgerSnapshotOf(Economy.Today);

            foreach (var building in Buildings.Values)
            {
                var bs = new BuildingSnapshot
                {
                    Id = building.Id,
                    DefId = building.Def.Id,
                    X = building.Origin.X,
                    Y = building.Origin.Y,
                    PriceMultiplier = building.PriceMultiplier,
                    Revenue = building.Revenue,
                    Dirtiness = building.Dirtiness,
                };
                bs.LongStayers.AddRange(building.LongStayers);
                snapshot.Buildings.Add(bs);
                snapshot.NextBuildingId = Math.Max(snapshot.NextBuildingId, building.Id + 1);
            }

            foreach (var tourist in Tourists)
            {
                if (tourist.State == TouristState.Gone) continue;
                var ts = new TouristSnapshot
                {
                    Id = tourist.Id,
                    Name = tourist.Name,
                    GuestType = (int)tourist.GuestType,
                    X = tourist.X,
                    Y = tourist.Y,
                    Needs = (float[])tourist.Needs.Values.Clone(),
                    Mood = tourist.Mood,
                    Money = tourist.Money,
                    Spent = tourist.Spent,
                    State = (int)(tourist.State == TouristState.UsingBuilding ? TouristState.UsingBuilding : TouristState.Wandering),
                    TargetBuildingId = tourist.TargetBuildingId,
                    PursuedNeed = tourist.PursuedNeed.HasValue ? (int)tourist.PursuedNeed.Value : -1,
                    ServiceRemaining = tourist.ServiceRemaining,
                    WaitedMinutes = tourist.WaitedMinutes,
                    StayDaysRemaining = tourist.StayDaysRemaining,
                };
                // HotelBuildingId is recoverable from the bed reservation on the building.
                snapshot.Tourists.Add(ts);
                snapshot.NextTouristId = Math.Max(snapshot.NextTouristId, tourist.Id + 1);
            }

            foreach (var member in StaffMembers)
            {
                snapshot.Staff.Add(new StaffSnapshot
                {
                    Id = member.Id,
                    Type = (int)member.Type,
                    Name = member.Name,
                    X = member.X,
                    Y = member.Y,
                    AssignedBuildingId = member.AssignedBuildingId,
                });
                snapshot.NextStaffId = Math.Max(snapshot.NextStaffId, member.Id + 1);
            }

            snapshot.EventLog.AddRange(EventLog);
            return snapshot;
        }

        private static LedgerSnapshot LedgerSnapshotOf(DayLedger ledger) => new LedgerSnapshot
        {
            Day = ledger.Day,
            Income = ledger.Income,
            Upkeep = ledger.Upkeep,
            Construction = ledger.Construction,
            Marketing = ledger.Marketing,
            Wages = ledger.Wages,
            Visitors = ledger.Visitors,
            LeftAngry = ledger.LeftAngry,
        };

        public void ApplySnapshot(ResortSnapshot snapshot)
        {
            if (snapshot.Map == null || snapshot.Map.Tiles == null)
                throw new ArgumentException("Snapshot has no map data");

            Reputation = snapshot.Reputation;
            TotalVisitors = snapshot.TotalVisitors;
            TotalWalkouts = snapshot.TotalWalkouts;
            NegativeCashDays = snapshot.NegativeCashDays;
            Bankrupt = snapshot.Bankrupt;
            CurrentWeather = (WeatherType)snapshot.Weather.Current;
            ForecastWeather = (WeatherType)snapshot.Weather.Forecast;
            Campaign = (CampaignLevel)snapshot.Marketing.Level;

            Clock.Restore(snapshot.Clock.Day, snapshot.Clock.MinuteOfDay, snapshot.Clock.Speed);
            Economy.Restore(snapshot.Economy.Cash, LedgerOf(snapshot.Economy.Today), LedgersOf(snapshot.Economy.History));

            int width = snapshot.Map.Width, height = snapshot.Map.Height;
            if (Litter == null || Litter.Length != width * height)
                throw new ArgumentException("Snapshot map size does not match this resort");
            Map.SetTiles(snapshot.Map.Tiles);
            Map.ClearAllOccupancy();
            for (int i = 0; i < Litter.Length; i++)
                Litter[i] = snapshot.Litter != null && i < snapshot.Litter.Length ? snapshot.Litter[i] : 0f;

            Buildings.Clear();
            Tourists.Clear();
            StaffMembers.Clear();
            Goals.Achieved.Clear();

            foreach (var goal in snapshot.Goals.Achieved)
                Goals.Achieved.Add((GoalId)goal);

            _nextBuildingId = 1;
            foreach (var bs in snapshot.Buildings)
            {
                BuildingDef def;
                try { def = BuildingCatalog.Get(bs.DefId); }
                catch (KeyNotFoundException) { continue; }

                var building = new Building
                {
                    Id = bs.Id,
                    Def = def,
                    Origin = new GridPos(bs.X, bs.Y),
                    PriceMultiplier = bs.PriceMultiplier,
                    Revenue = bs.Revenue,
                    Dirtiness = bs.Dirtiness,
                };
                building.LongStayers.AddRange(bs.LongStayers);
                Buildings[building.Id] = building;
                Map.OccupyIfValid(bs.X, bs.Y, def.Width, def.Height, building.Id);
                _nextBuildingId = Math.Max(_nextBuildingId, building.Id + 1);
            }

            _nextTouristId = 1;
            foreach (var ts in snapshot.Tourists)
            {
                var tourist = new Tourist
                {
                    Id = ts.Id,
                    Name = ts.Name,
                    GuestType = (GuestType)ts.GuestType,
                    X = ts.X,
                    Y = ts.Y,
                    Needs = new Needs { Values = (float[])ts.Needs.Clone() },
                    Mood = ts.Mood,
                    Money = ts.Money,
                    Spent = ts.Spent,
                    State = (TouristState)ts.State,
                    TargetBuildingId = ts.TargetBuildingId,
                    PursuedNeed = ts.PursuedNeed >= 0 ? (NeedType?)ts.PursuedNeed : null,
                    ServiceRemaining = ts.ServiceRemaining,
                    WaitedMinutes = ts.WaitedMinutes,
                    StayDaysRemaining = ts.StayDaysRemaining,
                };
                if (tourist.State == TouristState.UsingBuilding && FindBuilding(tourist.TargetBuildingId) is Building inside)
                {
                    tourist.X = inside.Center.X;
                    tourist.Y = inside.Center.Y;
                    inside.Occupants.Add(tourist.Id);
                    tourist.Sleeping = true; // conservative: mid-service guests resume inside
                }
                else
                {
                    tourist.State = TouristState.Wandering;
                    tourist.TargetBuildingId = -1;
                }

                // Recover the hotel reservation from whichever building holds this guest's bed.
                foreach (var building in Buildings.Values)
                    if (building.LongStayers.Contains(tourist.Id)) tourist.HotelBuildingId = building.Id;

                Tourists.Add(tourist);
                _nextTouristId = Math.Max(_nextTouristId, tourist.Id + 1);
            }

            _nextStaffId = 1;
            foreach (var ss in snapshot.Staff)
            {
                var member = new Staff
                {
                    Id = ss.Id,
                    Type = (StaffType)ss.Type,
                    Name = ss.Name,
                    X = ss.X,
                    Y = ss.Y,
                    AssignedBuildingId = ss.AssignedBuildingId,
                    State = StaffState.Idle,
                };
                member.SetPath(null);
                StaffMembers.Add(member);
                _nextStaffId = Math.Max(_nextStaffId, member.Id + 1);
            }

            EventLog.Clear();
            foreach (var entry in snapshot.EventLog) EventLog.Add(entry);
        }

        private static DayLedger LedgerOf(LedgerSnapshot s) => new DayLedger
        {
            Day = s.Day,
            Income = s.Income,
            Upkeep = s.Upkeep,
            Construction = s.Construction,
            Marketing = s.Marketing,
            Wages = s.Wages,
            Visitors = s.Visitors,
            LeftAngry = s.LeftAngry,
        };

        private static List<DayLedger> LedgersOf(IEnumerable<LedgerSnapshot> snapshots)
        {
            var result = new List<DayLedger>();
            foreach (var s in snapshots) result.Add(LedgerOf(s));
            return result;
        }
    }
}
