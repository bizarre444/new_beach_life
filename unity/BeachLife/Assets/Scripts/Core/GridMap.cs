using System;

namespace BeachLife.Core
{
    public enum TileType : byte
    {
        Water = 0,
        Sand = 1,
        Grass = 2,
        Promenade = 3,
        Rock = 4,
    }

    /// <summary>Terrain plus building occupancy for the resort plot.</summary>
    public sealed class GridMap
    {
        public readonly int Width;
        public readonly int Height;

        private readonly TileType[] _tiles;
        private readonly int[] _occupancy; // building id, or -1

        public GridMap(int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            Width = width;
            Height = height;
            _tiles = new TileType[width * height];
            _occupancy = new int[width * height];
            for (int i = 0; i < _occupancy.Length; i++) _occupancy[i] = -1;
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public TileType GetTile(int x, int y) => _tiles[y * Width + x];

        public void SetTile(int x, int y, TileType type) => _tiles[y * Width + x] = type;

        public int GetOccupant(int x, int y) => _occupancy[y * Width + x];

        public bool IsWalkable(int x, int y)
        {
            if (!InBounds(x, y)) return false;
            var tile = GetTile(x, y);
            if (tile == TileType.Water || tile == TileType.Rock) return false;
            return GetOccupant(x, y) < 0;
        }

        /// <summary>True when a building of <paramref name="w"/>x<paramref name="h"/> fits with its origin at (x,y).</summary>
        public bool CanPlace(int x, int y, int w, int h)
        {
            for (int dy = 0; dy < h; dy++)
            {
                for (int dx = 0; dx < w; dx++)
                {
                    int tx = x + dx, ty = y + dy;
                    if (!InBounds(tx, ty)) return false;
                    if (GetOccupant(tx, ty) >= 0) return false;
                    var tile = GetTile(tx, ty);
                    if (tile == TileType.Water || tile == TileType.Rock) return false;
                }
            }
            return true;
        }

        public void Occupy(int x, int y, int w, int h, int buildingId)
        {
            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                    _occupancy[(y + dy) * Width + (x + dx)] = buildingId;
        }

        public void Clear(int x, int y, int w, int h)
        {
            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                    _occupancy[(y + dy) * Width + (x + dx)] = -1;
        }

        /// <summary>Beach plot: water along the south edge, sand, then grass inland.</summary>
        public static GridMap CreateDefaultBeach(int width, int height)
        {
            var map = new GridMap(width, height);
            int waterRows = Math.Max(2, height / 8);
            int sandRows = Math.Max(4, height / 3);
            for (int y = 0; y < height; y++)
            {
                TileType type;
                if (y < waterRows) type = TileType.Water;
                else if (y < waterRows + sandRows) type = TileType.Sand;
                else if (y == waterRows + sandRows) type = TileType.Promenade;
                else type = TileType.Grass;

                for (int x = 0; x < width; x++)
                    map.SetTile(x, y, type);
            }
            return map;
        }
    }
}
