using System.Collections.Generic;

namespace BeachLife.Core
{
    public enum StaffType
    {
        Cleaner = 0,
        Attendant = 1,
    }

    public enum StaffState
    {
        Idle,
        Walking,
        Working,
    }

    /// <summary>Static description of a hireable staff type.</summary>
    public readonly struct StaffInfo
    {
        public readonly StaffType Type;
        public readonly string DisplayName;
        public readonly string Description;
        /// <summary>One-off hiring fee.</summary>
        public readonly int HireFee;
        /// <summary>Daily wage, charged at the end of every day.</summary>
        public readonly int WagePerDay;

        public StaffInfo(StaffType type, string displayName, string description, int hireFee, int wagePerDay)
        {
            Type = type;
            DisplayName = displayName;
            Description = description;
            HireFee = hireFee;
            WagePerDay = wagePerDay;
        }
    }

    public static class StaffTable
    {
        public static readonly StaffInfo[] All =
        {
            new StaffInfo(StaffType.Cleaner, "Cleaner",
                "Wanders the resort removing litter and scrubbing venues.", 400, 60),
            new StaffInfo(StaffType.Attendant, "Attendant",
                "Works one venue: +3 capacity, faster service and happier queues.", 700, 90),
        };

        public static StaffInfo Get(StaffType type) => All[(int)type];

        public static int MaxAttendantsPerVenue => 1;
    }

    /// <summary>A hired member of staff. Cleaners pick their own tasks; attendants are assigned to one venue.</summary>
    public sealed class Staff
    {
        public int Id;
        public StaffType Type;
        public string Name;
        public float X;
        public float Y;
        public StaffState State = StaffState.Idle;
        /// <summary>Venue the attendant works at; -1 when unassigned or for cleaners.</summary>
        public int AssignedBuildingId = -1;
        /// <summary>Venue a cleaner is scrubbing; -1 when the task is a litter tile.</summary>
        public int TargetBuildingId = -1;
        /// <summary>Litter tile a cleaner is heading for, or its current work spot.</summary>
        public GridPos WorkTile;
        public List<GridPos> Path = new List<GridPos>();
        public int PathIndex;
        /// <summary>Remaining work minutes before the current task completes.</summary>
        public float WorkRemaining;

        public GridPos Tile => new GridPos((int)(X + 0.5f), (int)(Y + 0.5f));
        public bool HasPath => Path != null && PathIndex < Path.Count;
        public int WagePerDay => StaffTable.Get(Type).WagePerDay;

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
