using System.Collections.Generic;

namespace BeachLife.Core
{
    /// <summary>A placed instance of a <see cref="BuildingDef"/>.</summary>
    public sealed class Building
    {
        public int Id;
        public BuildingDef Def;
        public GridPos Origin;
        /// <summary>Price multiplier the player sets; 1 is the catalog price.</summary>
        public float PriceMultiplier = 1f;
        public int Revenue;
        public readonly List<int> Occupants = new List<int>();
        public readonly Queue<int> Waiting = new Queue<int>();
        /// <summary>Hotel guests holding a bed here but not physically inside right now.</summary>
        public readonly List<int> LongStayers = new List<int>();
        /// <summary>0 is spotless, 1 is filthy; dirty buildings repel guests.</summary>
        public float Dirtiness;

        public bool HasFreeSlot => Occupants.Count + LongStayers.Count < Def.Capacity;

        public int Price => (int)(Def.TicketPrice * PriceMultiplier);

        public GridPos Center => new GridPos(Origin.X + Def.Width / 2, Origin.Y + Def.Height / 2);

        public bool Contains(int x, int y) =>
            x >= Origin.X && y >= Origin.Y && x < Origin.X + Def.Width && y < Origin.Y + Def.Height;

        /// <summary>Guests refuse a filthy or overpriced venue.</summary>
        public float Attractiveness()
        {
            float price = 1f - 0.45f * (PriceMultiplier - 1f);
            float clean = 1f - Dirtiness * 0.6f;
            float crowd = Def.Capacity <= 0 ? 1f : 1f - 0.5f * (Waiting.Count / (float)(Def.Capacity + 1));
            float value = price * clean * crowd;
            return value < 0f ? 0f : value;
        }
    }
}
