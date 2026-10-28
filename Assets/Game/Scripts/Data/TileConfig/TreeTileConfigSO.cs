using UnityEngine;

namespace SketchLand
{
    [CreateAssetMenu(fileName = "New TreeTileConfigSO", menuName = "OceanGame/TileConfig/TreeTileConfigSO")]
    public class TreeTileConfigSO : SpriteTileConfigSO
    {
        public override void OnTileDestroyed(TileGrid grid, int x, int y, TileData tileData, bool refreshCurrentBounds = false)
        {
            // Any cell can identify the tree's bottom-left root.
            int rootX = x - tileData.OffsetX;
            int rootY = y - tileData.OffsetY;

            for (int ox = 0; ox < Size.x; ox++)
            {
                for (int oy = 0; oy < Size.y; oy++)
                {
                    int targetX = rootX + ox;
                    int targetY = rootY + oy;

                    if (!grid.IsInBounds(targetX, targetY))
                        continue;

                    TileData current = grid.GetTileData(targetX, targetY);

                    // Only remove cells belonging to this tree instance.
                    if (current.TileId != tileData.TileId || current.OffsetX != ox || current.OffsetY != oy)
                    {
                        continue;
                    }

                    grid.SetTileDataDirect(targetX, targetY, TileData.Air);
                    grid.ClearDamage(targetX, targetY);

                    // Roll the loot table once at each occupied cell.
                    DropLoot(new Vector2(targetX + 0.5f, targetY + 0.5f));
                }
            }
        }

        public bool TryGrowTree(WorldGenContext ctx, int groundX, int groundY)
        {
            return TryGrowTree(ctx, groundX, groundY, ctx.Height - 1);
        }

        public bool TryGrowTree(WorldGenContext ctx, int groundX, int groundY, int maxGrowthY)
        {
            Vector2Int size = Size;

            // Offsets are stored as bytes: valid offsets are 0–255.
            if (size.x < 1 || size.y < 1 || size.x > 256 || size.y > 256)
            {
                return false;
            }

            int rootX = groundX;
            int rootY = groundY + 1;

            maxGrowthY = Mathf.Min(maxGrowthY, ctx.Height - 1);

            // Check the entire footprint fits inside the world/ceiling.
            if (rootX < 0 || groundY < 0 || rootX + size.x > ctx.Width || rootY + size.y - 1 > maxGrowthY)
            {
                return false;
            }

            // Require solid support beneath the footprint.
            for (int ox = 0; ox < size.x; ox++)
            {
                TileData ground = ctx.FgGrid[rootX + ox, groundY];

                if (!ground.HasTile || !ground.IsSolid)
                    return false;
            }

            // Check every cell before placing anything.
            for (int ox = 0; ox < size.x; ox++)
            {
                for (int oy = 0; oy < size.y; oy++)
                {
                    if (ctx.FgGrid[rootX + ox, rootY + oy].HasTile)
                        return false;
                }
            }

            ushort treeId = GetId();

            // Place one multi-tile instance, with offsets to its root.
            for (int ox = 0; ox < size.x; ox++)
            {
                for (int oy = 0; oy < size.y; oy++)
                {
                    ctx.FgGrid[rootX + ox, rootY + oy] =
                        new TileData(
                            treeId,
                            offsetX: (byte)ox,
                            offsetY: (byte)oy);
                }
            }

            return true;
        }
    }
}