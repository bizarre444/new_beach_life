using System;
using System.Collections.Generic;

namespace BeachLife.Core
{
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public readonly int X;
        public readonly int Y;

        public GridPos(int x, int y) { X = x; Y = y; }

        public bool Equals(GridPos other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPos other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => $"({X},{Y})";

        public static int Manhattan(GridPos a, GridPos b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }

    /// <summary>4-directional A* over walkable tiles.</summary>
    public static class Pathfinder
    {
        private static readonly int[] DX = { 1, -1, 0, 0 };
        private static readonly int[] DY = { 0, 0, 1, -1 };

        /// <summary>
        /// Path from <paramref name="start"/> to <paramref name="goal"/>, excluding the start tile.
        /// The goal may be blocked (e.g. a building footprint); in that case the path stops on an
        /// adjacent walkable tile. Returns null when unreachable.
        /// </summary>
        public static List<GridPos> FindPath(GridMap map, GridPos start, GridPos goal, bool goalIsBlocking = false)
        {
            if (!map.InBounds(start.X, start.Y) || !map.InBounds(goal.X, goal.Y)) return null;
            if (start.Equals(goal)) return new List<GridPos>();

            var cameFrom = new Dictionary<GridPos, GridPos>();
            var gScore = new Dictionary<GridPos, int> { [start] = 0 };
            var open = new PriorityQueue();
            open.Push(start, GridPos.Manhattan(start, goal));

            while (open.TryPop(out GridPos current))
            {
                if (current.Equals(goal) || (goalIsBlocking && GridPos.Manhattan(current, goal) == 1))
                    return Reconstruct(cameFrom, current, start);

                int currentG = gScore[current];
                for (int i = 0; i < 4; i++)
                {
                    var next = new GridPos(current.X + DX[i], current.Y + DY[i]);
                    bool isGoal = next.Equals(goal);
                    if (!isGoal && !map.IsWalkable(next.X, next.Y)) continue;
                    if (isGoal && goalIsBlocking) continue;
                    if (isGoal && !map.IsWalkable(next.X, next.Y)) continue;

                    int tentative = currentG + 1;
                    if (gScore.TryGetValue(next, out int known) && tentative >= known) continue;

                    cameFrom[next] = current;
                    gScore[next] = tentative;
                    open.Push(next, tentative + GridPos.Manhattan(next, goal));
                }
            }
            return null;
        }

        private static List<GridPos> Reconstruct(Dictionary<GridPos, GridPos> cameFrom, GridPos end, GridPos start)
        {
            var path = new List<GridPos>();
            var node = end;
            while (!node.Equals(start))
            {
                path.Add(node);
                if (!cameFrom.TryGetValue(node, out node)) break;
            }
            path.Reverse();
            return path;
        }

        private sealed class PriorityQueue
        {
            private readonly List<(GridPos pos, int priority)> _items = new List<(GridPos, int)>();

            public void Push(GridPos pos, int priority)
            {
                _items.Add((pos, priority));
                int i = _items.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (_items[parent].priority <= _items[i].priority) break;
                    (_items[parent], _items[i]) = (_items[i], _items[parent]);
                    i = parent;
                }
            }

            public bool TryPop(out GridPos pos)
            {
                if (_items.Count == 0) { pos = default; return false; }
                pos = _items[0].pos;
                _items[0] = _items[_items.Count - 1];
                _items.RemoveAt(_items.Count - 1);
                int i = 0;
                while (true)
                {
                    int left = 2 * i + 1, right = left + 1, smallest = i;
                    if (left < _items.Count && _items[left].priority < _items[smallest].priority) smallest = left;
                    if (right < _items.Count && _items[right].priority < _items[smallest].priority) smallest = right;
                    if (smallest == i) break;
                    (_items[smallest], _items[i]) = (_items[i], _items[smallest]);
                    i = smallest;
                }
                return true;
            }
        }
    }
}
