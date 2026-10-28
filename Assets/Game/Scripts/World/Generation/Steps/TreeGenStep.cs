using System.Collections;
using UnityEngine;

namespace SketchLand
{
    public class TreeGenStep : WorldGenStep
    {
        [SerializeField] private TreeTileConfigSO _tree;
        [SerializeField] private int _minTreeSpace = 1;
        [SerializeField] private int _maxTreeSpace = 4;
    
        public override IEnumerator Execute(WorldGenContext ctx)
        {
            for (int x = 0; x < ctx.Width; x++)
            {
                int startY = ctx.Height - 2;

                // Travel from the top of the mpa to sea level
                for (int y = startY; y >= ctx.RoughSurfaceHeight; y--)
                {
                    bool hasTile = ctx.FgGrid[x, y].HasTile;
                    bool hasTileAbove = ctx.FgGrid[x, y + 1].HasTile;

                    // Only place trees on top-facing exposed tiles
                    if (!hasTile || hasTileAbove)
                    {
                        continue;
                    }

                    // Attempt to grow a tree at this surface position
                    if (_tree.TryGrowTree(ctx, x, y))
                    {
                        // Successfully placed! Pick a random space and jump ahead
                        int space = ctx.Random.Next(_minTreeSpace, _maxTreeSpace + 1);
                        x += space;
                    }

                    // Stop scanning downward in this column (we reached the surface)
                    break;
                }

                if (x % ctx.GenColumnsPerFrame == 0) yield return null;
            }

            
        }
        
        
    }
}
