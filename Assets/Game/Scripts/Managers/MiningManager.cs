using UnityEngine;

namespace SketchLand
{
    public class MiningManager : MonoBehaviour
    {
        public static MiningManager Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }
        
        public readonly struct SwingTarget
        {
            public readonly Vector2Int Cell;
            public readonly WorldManager.LayerType Layer;
            public readonly Vector2 AimPoint;
            public readonly ushort TileId;

            public SwingTarget(Vector2Int cell, WorldManager.LayerType layer, Vector2 aimPoint, ushort tileId)
            {
                Cell = cell;
                Layer = layer;
                AimPoint = aimPoint;
                TileId = tileId;
            }
        }

        public SwingTarget? CaptureTarget(Player player, ToolItemSO tool, Vector2 aimPoint)
        {
            var world = WorldManager.Instance;
            if (!CanMine(player, tool, aimPoint) || world.MouseOverUI) return null;

            var layer = world.ActiveLayer;
            var grid = world.ActiveGrid;
            if (!MiningTargetResolver.TryResolve(grid, aimPoint, layer == WorldManager.LayerType.Background, out var cell))
                return null;
            if (layer == WorldManager.LayerType.Background && !HasAdjacentAir(grid, cell))
                return null;

            var tile = grid.GetTileData(cell.x, cell.y);
            return tile.HasTile && tool.CanHarvest(tile.TileConfig)
                ? new SwingTarget(cell, layer, aimPoint, tile.TileId) : null;
        }

        public bool TryPerformMine(Player player, ToolItemSO tool, SwingTarget target)
        {
            if (!CanMine(player, tool, target.AimPoint)) return false;

            var world = WorldManager.Instance;
            var grid = target.Layer == WorldManager.LayerType.Foreground ? world.FgGrid : world.BgGrid;
            var tile = grid.GetTileData(target.Cell.x, target.Cell.y);
            if (!tile.HasTile || tile.TileId != target.TileId) return false;
            // Recheck at impact: an opening may have been filled during the windup.
            if (target.Layer == WorldManager.LayerType.Background && !HasAdjacentAir(grid, target.Cell))
                return false;

            var config = tile.TileConfig;
            if (!tool.CanHarvest(config)) return false;
            world.DamageTile(target.Cell, tool.MiningDamage, target.Layer, refreshCurrentBounds: true);
            return true;
        }

        private static bool HasAdjacentAir(TileGrid grid, Vector2Int cell)
        {
            // Only edge-sharing cells in this layer count; diagonals and the world boundary do not.
            bool IsAir(int x, int y) => grid.IsInBounds(x, y) && grid.GetTileData(x, y).IsAir;
            return IsAir(cell.x - 1, cell.y) || IsAir(cell.x + 1, cell.y)
                || IsAir(cell.x, cell.y - 1) || IsAir(cell.x, cell.y + 1);
        }

        private bool CanMine(Player player, ToolItemSO tool, Vector2 point)
        {
            return isActiveAndEnabled && tool != null
                && WorldManager.Instance != null && WorldManager.Instance.IsWorldReady
                && player != null && player.Character != null && player.Character.Health.CurrentLifeState.Value != LifeState.Dead
                && Vector2.Distance(player.Character.transform.position, point) <= player.InteractRange;
        }
    }
}
