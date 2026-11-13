using System;
using System.Collections;
using UnityEngine;

namespace SketchLand
{
    [Serializable]
    public class BerryBushGenStep : WorldGenStep
    {
        [SerializeField] private SpriteTileConfigSO _berryBush;
        [SerializeField, Range(0f, 1f)] private float _placementChance = 0.08f;

        public override IEnumerator Execute(WorldGenContext ctx)
        {
            if (_berryBush == null) yield break;

            ushort bushId = _berryBush.GetId();
            int columnsPerFrame = Mathf.Max(1, ctx.GenColumnsPerFrame);
            for (int x = 0; x < ctx.Width; x++)
            {
                int y = ctx.SurfaceHeightMap[x];
                if (y >= 0 && y + 1 < ctx.Height && ctx.FgGrid[x, y].IsSolid &&
                    !ctx.FgGrid[x, y + 1].HasTile && ctx.Random.NextDouble() < _placementChance)
                {
                    ctx.FgGrid[x, y + 1] = new TileData(bushId);
                }

                if ((x + 1) % columnsPerFrame == 0) yield return null;
            }
        }
    }
}
