using UnityEngine;

namespace ShelfMod
{
    public enum ShelfType
    {
        Small,
        Medium,
        Large,
        Wide
    }

    public static class ShelfConfig
    {
        public struct ShelfDimensions
        {
            public float Width;
            public float Depth;
            public int Tiers;

            public ShelfDimensions(float w, float d, int t)
            {
                Width = w;
                Depth = d;
                Tiers = t;
            }
        }

        public static ShelfDimensions GetDimensions(ShelfType type)
        {
            return type switch
            {
                ShelfType.Small  => new ShelfDimensions(0.8f, 0.35f, 2),
                ShelfType.Medium => new ShelfDimensions(1.2f, 0.40f, 3),
                ShelfType.Large  => new ShelfDimensions(1.8f, 0.45f, 4),
                ShelfType.Wide   => new ShelfDimensions(2.4f, 0.40f, 3),
                _ => new ShelfDimensions(1.2f, 0.40f, 3),
            };
        }

        public static int GetSlotsPerTier(float shelfWidth)
        {
            return Mathf.Max(1, Mathf.FloorToInt(shelfWidth / 0.4f));
        }

        public const float MaxTierLoadKg = 25f;
        public const float SnapRadius = 0.5f;
    }
}
