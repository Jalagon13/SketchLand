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

            var config = tile.TileConfig;
            if (!tool.CanHarvest(config)) return false;
            world.DamageTile(target.Cell, tool.MiningDamage, target.Layer, refreshCurrentBounds: true);
            return true;
        }

        private bool CanMine(Player player, ToolItemSO tool, Vector2 point)
        {
            return isActiveAndEnabled && tool != null && tool.CanBreakTilesAndObjects
                && WorldManager.Instance != null && WorldManager.Instance.IsWorldReady
                && player != null && player.Character != null && player.Character.Health.CurrentLifeState.Value != LifeState.Dead
                && Vector2.Distance(player.Character.transform.position, point) <= player.InteractRange;
        }
    }
}
