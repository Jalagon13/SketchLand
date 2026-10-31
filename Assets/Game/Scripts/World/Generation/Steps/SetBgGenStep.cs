using System;
using System.Collections;
using UnityEngine;

namespace SketchLand
{
    [Serializable]
    public class SetBgGenStep : WorldGenStep
    {
        [SerializeField] private TileConfigSO _dirtTile;
        [SerializeField] private int _belowSurfaceOffset = 1;

        public override IEnumerator Execute(WorldGenContext ctx)
        {
            int bottomLevel = Mathf.Max(0, ctx.UndergroundBottomLevel);
            int columnsPerFrame = Mathf.Max(1, ctx.GenColumnsPerFrame);

            for (int x = 0; x < ctx.Width; x++)
            {
                for (int y = bottomLevel; y < ctx.SurfaceHeightMap[x] - _belowSurfaceOffset; y++)
                {
                    ctx.BgGrid[x, y] = new TileData(_dirtTile.GetId());
                }

                if ((x + 1) % columnsPerFrame == 0) yield return null;
            }
        }
    }
}
