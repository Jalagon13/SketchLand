using UnityEngine;
using UnityEngine.InputSystem;

namespace SketchLand
{
    public class MiningManager : MonoBehaviour
    {
        public static MiningManager Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }
        
        private Player _miningPlayer;
        private ToolItemSO _miningTool;
        private double _lastUpdateTime;
        private double _tickProgress;

        public bool IsMining => _miningTool != null;

        public void StartMining(Player player, ToolItemSO tool)
        {
            if (!isActiveAndEnabled || player == null || tool == null) return;
            if (_miningPlayer == player && _miningTool == tool) return;
            _miningPlayer = player;
            _miningTool = tool;
            _tickProgress = 0d;
            _lastUpdateTime = Time.timeAsDouble;
        }

        public void StopMining(Player player, ToolItemSO tool)
        {
            if (_miningPlayer == player && _miningTool == tool) ClearMining();
        }

        private void ClearMining()
        {
            _miningPlayer = null;
            _miningTool = null;
            _tickProgress = 0d;
        }

        private void OnDisable() => ClearMining();

        private void Update()
        {
            if (!IsMining) return;
            var input = InventoryInputManager.Instance;
            if (_miningPlayer == null || _miningPlayer.Character == null
                || _miningPlayer.Character.Health.CurrentLifeState.Value == LifeState.Dead
                || GameInput.Instance == null || !GameInput.Instance.PrimaryActionHeld
                || input == null || !input.isActiveAndEnabled
                || input.GetActiveInvSlot()?.GetItemSO() != _miningTool
                || WorldManager.Instance == null || !WorldManager.Instance.IsWorldReady)
            {
                ClearMining();
                return;
            }

            double now = Time.timeAsDouble;
            _tickProgress += (now - _lastUpdateTime) * Mathf.Max(0.01f, _miningTool.GetMiningTickRate(_miningPlayer.Food));
            _lastUpdateTime = now;
            while (_tickProgress >= 1d)
            {
                _tickProgress -= 1d;
                MineHoveredTile();
            }
        }

        private void MineHoveredTile()
        {
            if (Camera.main == null || Mouse.current == null) return;
            Vector2 point = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            var target = CaptureTarget(_miningPlayer, _miningTool, point);
            if (target.HasValue) TryPerformMine(_miningPlayer, _miningTool, target.Value);
        }

        public readonly struct MiningTarget
        {
            public readonly Vector2Int Cell;
            public readonly WorldManager.LayerType Layer;
            public readonly Vector2 AimPoint;
            public readonly ushort TileId;

            public MiningTarget(Vector2Int cell, WorldManager.LayerType layer, Vector2 aimPoint, ushort tileId)
            {
                Cell = cell;
                Layer = layer;
                AimPoint = aimPoint;
                TileId = tileId;
            }
        }

        public MiningTarget? CaptureTarget(Player player, ToolItemSO tool, Vector2 aimPoint)
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
                ? new MiningTarget(cell, layer, aimPoint, tile.TileId) : null;
        }

        public bool TryPerformMine(Player player, ToolItemSO tool, MiningTarget target)
        {
            if (!CanMine(player, tool, target.AimPoint)) return false;

            var world = WorldManager.Instance;
            var grid = target.Layer == WorldManager.LayerType.Foreground ? world.FgGrid : world.BgGrid;
            var tile = grid.GetTileData(target.Cell.x, target.Cell.y);
            if (!tile.HasTile || tile.TileId != target.TileId) return false;
            // Keep background mining restricted to exposed edges.
            if (target.Layer == WorldManager.LayerType.Background && !HasAdjacentAir(grid, target.Cell))
                return false;

            var config = tile.TileConfig;
            if (!tool.CanHarvest(config) || config.Indestructible) return false;
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
