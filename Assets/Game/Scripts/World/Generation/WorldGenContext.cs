using UnityEngine;

namespace SketchLand
{
    public class WorldGenContext
    {
        public int Width { get; }
        public int Height { get; }
        public int Seed { get; }
        public int RoughSurfaceHeight { get; }
        public int UndergroundBottomLevel { get; }
        public int GenColumnsPerFrame { get; }
        public System.Random Random { get; }

        public TileData[,] FgGrid { get; set; }
        public TileData[,] BgGrid { get; set; }
        public int[] SurfaceHeightMap { get; }
        public WorldZone[] Zones { get; }

        public WorldGenContext(int width, int height, int seed, int undergroundBottomLevel, int genColumnsPerFrame, int roughSurfaceHeight, WorldZone[] zones = null)
        {
            Width = width;
            Height = height;
            Seed = seed;
            RoughSurfaceHeight = roughSurfaceHeight;
            UndergroundBottomLevel = undergroundBottomLevel;
            GenColumnsPerFrame = genColumnsPerFrame;
            FgGrid = new TileData[width, height];
            BgGrid = new TileData[width, height];
            SurfaceHeightMap = new int[width];
            Zones = zones ?? new[] { new WorldZone(new WorldZoneAppearance(), 0, height, true) };
            Random = new(Seed);
        }
    }
}
