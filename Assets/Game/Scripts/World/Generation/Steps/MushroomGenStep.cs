using System;
using System.Collections;
using UnityEngine;

namespace SketchLand
{
    [Serializable]
    public class MushroomGenStep : WorldGenStep
    {
        [SerializeField] private SpriteTileConfigSO _mushroom;
        [SerializeField, Range(0f, 1f)] private float _placementChance = 0.15f;

        public override IEnumerator Execute(WorldGenContext ctx)
        {
            if (_mushroom == null) yield break;

            ushort mushroomId = _mushroom.GetId();
            int columnsPerFrame = Mathf.Max(1, ctx.GenColumnsPerFrame);
            for (int x = 0; x < ctx.Width; x++)
            {
                foreach (var zone in ctx.Zones)
                {
                    if (!zone.AllowsNaturalDaylight &&
                        !string.Equals(zone.Appearance?.Name, "Underground", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Empty cells below the original terrain surface are natural caves.
                    int top = Mathf.Min(ctx.Height, zone.MaxY, ctx.SurfaceHeightMap[x]);
                    for (int y = Mathf.Max(1, zone.MinY); y < top; y++)
                    {
                        if (!ctx.FgGrid[x, y].HasTile && ctx.FgGrid[x, y - 1].IsSolid &&
                            ctx.Random.NextDouble() < _placementChance)
                            ctx.FgGrid[x, y] = new TileData(mushroomId);
                    }
                }

                if ((x + 1) % columnsPerFrame == 0) yield return null;
            }
        }
    }
}
