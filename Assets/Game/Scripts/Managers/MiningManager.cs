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
        
        // Called once by the swing animation at impact. Resolve the current hover,
        // so moving the mouse during windup changes which tile receives the hit.
        public bool TryPerformMine(ToolItemSO tool)
        {
            if (!isActiveAndEnabled || tool == null) return false;
            if (WorldManager.Instance == null || !WorldManager.Instance.IsWorldReady || WorldManager.Instance.MouseOverUI) return false;
            if (Player.Instance == null || Player.Instance.Character == null) return false;
            if (Player.Instance.Character.Health.CurrentLifeState.Value == LifeState.Dead) return false;

            Vector2 mouseWorldPos = WorldManager.MouseWorldPosition;

            float distanceToPlayer = Vector2.Distance(Player.Instance.Character.transform.position, mouseWorldPos);
            if (distanceToPlayer > Player.Instance.InteractRange) return false;

            var targetLayer = WorldManager.Instance.ActiveLayer;
            var grid = targetLayer == WorldManager.LayerType.Foreground ? WorldManager.Instance.FgGrid : WorldManager.Instance.BgGrid;
            
            if (!MiningTargetResolver.TryResolve(grid, mouseWorldPos, targetLayer == WorldManager.LayerType.Background, out Vector2Int targetPosition))
                return false;

            var tileData = grid.GetTileData(targetPosition.x, targetPosition.y);
            
            if (tileData.HasTile)
            {
                TileConfigSO tileConfig = tileData.TileConfig;
                if (tileConfig == null) return false;

                // If the tile requires a specific tool, verify the equipped tool matches
                if (tileConfig.RequiredHarvestType != HarvestType.None && tool.HarvestType != tileConfig.RequiredHarvestType)
                {
                    return false; // Incompatible tool (e.g. pickaxe hitting tree, axe hitting stone)
                }

                WorldManager.Instance.DamageTile(targetPosition, tool.MiningDamage, targetLayer, refreshCurrentBounds: true);
                
                return true;
            }

            return false;
        }
    }
}
