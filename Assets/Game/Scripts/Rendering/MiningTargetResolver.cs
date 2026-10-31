using UnityEngine;

namespace SketchLand
{
    /// <summary>
    /// Converts pointer coordinates to a source tile before mining rules run.
    /// Visual coverage affects selection only, never grid occupancy or damage storage.
    /// </summary>
    public static class MiningTargetResolver
    {
        public static bool TryResolve(TileGrid grid, Vector2 worldPoint, bool background, out Vector2Int position)
        {
            position = Vector2Int.FloorToInt(worldPoint);
            if (grid == null) return false;

            TileData directTile = grid.GetTileData(position.x, position.y);

            // Sprite and multi-tile backgrounds retain their existing grid targeting.
            if (!background || (directTile.HasTile && TerrainMeshBuilder.GetVisualConfig(directTile) == null))
                return directTile.HasTile;

            return new TerrainRenderLayout(grid, true).TryGetSource(worldPoint, out position);
        }
    }
}
