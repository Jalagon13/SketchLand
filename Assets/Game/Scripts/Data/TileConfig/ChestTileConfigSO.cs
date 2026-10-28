using UnityEngine;

namespace SketchLand
{
    [CreateAssetMenu(fileName = "New ChestTileConfigSO", menuName = "OceanGame/TileConfig/ChestTileConfigSO")]
    public class ChestTileConfigSO : SpriteTileConfigSO
    {
        public override void OnTileDestroyed(TileGrid grid, int x, int y, TileData tileData, bool refreshCurrentBounds = false)
        {
            int rootX = x - tileData.OffsetX;
            int rootY = y - tileData.OffsetY;

            // Spills all stored items onto the ground and removes the chest from ChestManager
            ChestManager.Instance.DestroyChest(new Vector2Int(rootX, rootY));

            // Clear multi-tile cells from the grid and drop the chest item itself
            base.OnTileDestroyed(grid, x, y, tileData, refreshCurrentBounds);
        }
    }
}