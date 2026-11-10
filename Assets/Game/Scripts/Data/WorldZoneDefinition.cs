using System;
using UnityEngine;

namespace SketchLand
{
    [Serializable]
    public class WorldZoneAppearance
    {
        public string Name = "Surface";
        [Tooltip("Repeating terrain fill behind background walls. Empty uses parallax in Surface, or a black backdrop in lower zones.")]
        public TerrainTileConfigSO BackgroundTerrain;
    }

    [Serializable]
    public sealed class WorldZoneLayer : WorldZoneAppearance
    {
        [Tooltip("World Y below which this zone begins. It extends to the next lower zone, or the world bottom. Regenerate the world after changing boundaries.")]
        [Min(0)] public int StartsBelowY = 312;

        public WorldZoneLayer()
        {
            Name = "Underground";
        }
    }

    /// <summary>A generated horizontal band. MinY is inclusive, MaxY is exclusive.</summary>
    public readonly struct WorldZone
    {
        public WorldZoneAppearance Appearance { get; }
        public int MinY { get; }
        public int MaxY { get; }
        public bool AllowsNaturalDaylight { get; }

        public WorldZone(WorldZoneAppearance appearance, int minY, int maxY, bool allowsNaturalDaylight)
        {
            Appearance = appearance;
            MinY = minY;
            MaxY = maxY;
            AllowsNaturalDaylight = allowsNaturalDaylight;
        }
    }
}
