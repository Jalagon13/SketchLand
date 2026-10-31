using System;
using UnityEngine;

namespace SketchLand
{
    public class MiningManager : MonoBehaviour
    {
        public static MiningManager Instance { get; private set; }

        private float _nextMineTime;
        private bool _isMining;

        private void Awake()
        {
            Instance = this;
        }
        
        private void Start() 
        {
            if (Player.Instance == null) return;

            Player.Instance.PlayerReady += OnPlayerReady;

            if (Player.Instance.Character != null)
                OnPlayerReady(Player.Instance.Character);
        }
        
        private void OnDestroy() 
        {
            if (Player.Instance == null) return;

            if(Player.Instance.Character != null)
                Player.Instance.Character.Health.CurrentLifeState.OnValueChanged -= OnLifeStateChanged;
                
            Player.Instance.PlayerReady -= OnPlayerReady;
        }

        private void OnPlayerReady(ServerCharacter player)
        {
            player.Health.CurrentLifeState.OnValueChanged += OnLifeStateChanged;
        }

        private void OnLifeStateChanged(LifeState previousValue, LifeState newValue)
        {
            if(newValue == LifeState.Dead)
            {
                StopMining();
            }
        }

        public void StartMining(ToolItemSO tool)
        {
            _isMining = true;
            
            TryPerformMine(tool); // Instantly attempt a mine on the first click
        }

        public void TickMining(ToolItemSO tool)
        {
            if (!_isMining) return;

            TryPerformMine(tool);
        }

        public void StopMining()
        {
            _isMining = false;
        }

        private bool TryPerformMine(ToolItemSO tool)
        {
            if (tool == null) return false;
            if (Time.time < _nextMineTime) return false; // Enforce tool cooldown even when spam-clicking
            if (WorldManager.Instance == null || WorldManager.Instance.MouseOverUI) return false;

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
                
                // Set interval
                float interval = tool.MineTicksPerSecond > 0f ? 1f / tool.MineTicksPerSecond : 0.25f;
                _nextMineTime = Time.time + interval;
                
                return true;
            }

            return false;
        }
    }
}
