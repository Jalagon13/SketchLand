using System;
using System.Collections;
using UnityEngine;

namespace SketchLand
{
    [Serializable]
    public class CaveGenStep : WorldGenStep
    {
        [SerializeField, Min(0.01f)] private float _scale = 50f;
        [SerializeField, Min(0f)] private float _frequency = 1f;
        [SerializeField, Range(0f, 1f), Tooltip("Cave noise threshold in Underground and lower zones. Higher values carve fewer caves.")]
        private float _caveThreshold = 0.6f;
        [SerializeField, Range(0f, 1f), Tooltip("Cave noise threshold in the Surface zone. Higher values make surface caves rarer; never uses a value below Cave Threshold. Regenerate the world to apply.")]
        private float _surfaceCaveThreshold = 0.85f;

        public override IEnumerator Execute(WorldGenContext ctx)
        {
            float offsetX = (float)ctx.Random.NextDouble() * 10000f;
            float offsetY = (float)ctx.Random.NextDouble() * 10000f;
            int columnsPerFrame = Mathf.Max(1, ctx.GenColumnsPerFrame);
            var thresholdsByY = new float[ctx.Height];
            for (int y = 0; y < ctx.Height; y++)
            {
                thresholdsByY[y] = _caveThreshold;
                foreach (var zone in ctx.Zones)
                    if (zone.AllowsNaturalDaylight && y >= zone.MinY && y < zone.MaxY)
                    {
                        thresholdsByY[y] = Mathf.Max(_caveThreshold, _surfaceCaveThreshold);
                        break;
                    }
            }

            for (int x = 0; x < ctx.Width; x++)
            {
                for (int y = 0; y < ctx.SurfaceHeightMap[x] + 1; y++)
                {
                    float noise = Mathf.PerlinNoise((x * _frequency / _scale) + offsetX, (y * _frequency / _scale) + offsetY);

                    if (noise > thresholdsByY[y])
                    {
                        ctx.FgGrid[x, y] = TileData.Air;
                    }
                }

                if ((x + 1) % columnsPerFrame == 0) yield return null;
            }
        }
    }
}
