using System.Collections.Generic;

namespace BeachLife.Core
{
    public enum TouristState
    {
        Arriving,
        Wandering,
        WalkingToBuilding,
        Queueing,
        UsingBuilding,
        Leaving,
        Gone,
    }

    /// <summary>Spending personality, rolled at spawn. Drives budgets, price tolerance and messiness.</summary>
    public enum GuestType
    {
        Budget = 0,
        Regular = 1,
        Vip = 2,
    }

    public sealed class Tourist
    {
        public int Id;
        public string Name;
        public GuestType GuestType = GuestType.Regular;
        public float X;
        public float Y;
        public Needs Needs;
        /// <summary>Overall happiness in [0,1]; drives reputation and spending.</summary>
        public float Mood = 0.7f;
        public int Money;
        public int Spent;
        public TouristState State = TouristState.Arriving;
        public int TargetBuildingId = -1;
        public NeedType? PursuedNeed;
        public float ServiceRemaining;
        public float WaitedMinutes;
        public List<GridPos> Path = new List<GridPos>();
        public int PathIndex;

        /// <summary>Days left of a hotel stay; 0 means a day visitor. Hotel beds are reserved on spawn.</summary>
        public int StayDaysRemaining;
        /// <summary>Hotel holding this guest's bed reservation; -1 when none.</summary>
        public int HotelBuildingId = -1;
        /// <summary>True while sleeping off the night inside a hotel.</summary>
        public bool Sleeping;
        /// <summary>True while walking to the hotel for a booked night (guaranteed entry despite queues).</summary>
        public bool HeadingToBed;

        /// <summary>Venues priced above this multiplier are unattractive to this guest.</summary>
        public float PriceTolerance => GuestType == GuestType.Budget ? 0.9f : GuestType == GuestType.Vip ? 1.8f : 1.2f;
        /// <summary>Litter drop chance multiplier for this guest.</summary>
        public float LitterMultiplier => GuestType == GuestType.Budget ? 0.7f : GuestType == GuestType.Vip ? 1.4f : 1f;

        public GridPos Tile => new GridPos((int)(X + 0.5f), (int)(Y + 0.5f));

        public bool HasPath => Path != null && PathIndex < Path.Count;

        public void SetPath(List<GridPos> path)
        {
            Path = path ?? new List<GridPos>();
            PathIndex = 0;
        }

        /// <summary>Advances along the current path. Returns true once the path is exhausted.</summary>
        public bool StepAlongPath(float tilesToMove)
        {
            while (tilesToMove > 0f && HasPath)
            {
                var next = Path[PathIndex];
                float dx = next.X - X;
                float dy = next.Y - Y;
                float dist = (float)System.Math.Sqrt(dx * dx + dy * dy);
                if (dist <= tilesToMove || dist < 0.0001f)
                {
                    X = next.X;
                    Y = next.Y;
                    tilesToMove -= dist;
                    PathIndex++;
                }
                else
                {
                    X += dx / dist * tilesToMove;
                    Y += dy / dist * tilesToMove;
                    tilesToMove = 0f;
                }
            }
            return !HasPath;
        }
    }
}
