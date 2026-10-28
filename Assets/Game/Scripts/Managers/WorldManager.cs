using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

namespace SketchLand
{
    public class WorldManager : MonoBehaviour
    {
        public static WorldManager Instance { get; private set; }

        public event Action OnWorldReady;
        public event Action<LayerType> OnActiveLayerChanged;

        [Header("World References")]
        [field: SerializeField] public WorldGenerator WorldGen { get; private set; }
        [SerializeField] private ParallaxBackground _surfaceBg;

        [Header("Tile Repair")]
        [SerializeField, Min(0f)] private float _tileRepairDelay = 5f;

        [Header("Debug Testing")]
        [SerializeField] private TreeTileConfigSO _debugTreeConfig;
        [SerializeField] private ProjectileSO _testProjectile;

        public enum LayerType { Foreground, Background }
        public TileGrid FgGrid { get; private set; }
        public TileGrid BgGrid { get; private set; }
        public LayerType ActiveLayer { get; private set; } = LayerType.Foreground;
        public TileGrid ActiveGrid => ActiveLayer == LayerType.Foreground ? FgGrid : BgGrid;
        
        public static Vector2Int MouseWorldTilePosition { get; private set; }
        public static Vector2 MouseWorldPosition { get; private set; }
        public bool MouseOverUI { get; private set; }
        public bool IsWorldReady { get; private set; } = false;
        public int SeaLevel { get; private set; }


        private void Awake()
        {
            Instance = this;
        }
        
        private void Start() 
        {
            GameInput.Instance.OnToggleBuildLayer += ToggleBuildLayer;
        }
        
        private void OnDestroy() 
        {
            GameInput.Instance.OnToggleBuildLayer -= ToggleBuildLayer;
        }

        private void Update()
        {
            if(!IsWorldReady) return;
        
            FgGrid.RestoreExpiredDamage(Time.time, _tileRepairDelay);
            BgGrid.RestoreExpiredDamage(Time.time, _tileRepairDelay);

            MouseOverUI = EventSystem.current.IsPointerOverGameObject();
            MouseWorldPosition = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            MouseWorldTilePosition = new(Mathf.FloorToInt(MouseWorldPosition.x), Mathf.FloorToInt(MouseWorldPosition.y));

            if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            {
                ProjectileManager.SpawnProjectile(_testProjectile, Player.Instance.Character.transform.position, Vector2.right, sourceEntity: Player.Instance.Character);
            }
        }

        private void ToggleBuildLayer()
        {
            ActiveLayer = (ActiveLayer == LayerType.Foreground) ? LayerType.Background : LayerType.Foreground;
            OnActiveLayerChanged?.Invoke(ActiveLayer);

            if (Player.Instance == null || Player.Instance.Character == null)
            {
                Debug.LogError("Cannot show the active layer popup because the player character is not available.", this);
                return;
            }

            WorldTextPopupManager.Instance.SpawnTextPopup(Player.Instance.Character.transform.position + new Vector3(0, 1.5f, 0), $"Targeting {ActiveLayer}");
        }

        public void LoadGeneratedWorld(WorldGenContext context)
        {
            var width = context.Width;
            var height = context.Height;
            SeaLevel = context.RoughSurfaceHeight;

            // Create fresh layers matching generated dimensions
            FgGrid = new TileGrid(width, height);
            BgGrid = new TileGrid(width, height);

            // Copy generated 2D tile arrays into layers
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    FgGrid.SetTileData(x, y, context.FgGrid[x, y]);
                    BgGrid.SetTileData(x, y, context.BgGrid[x, y]);
                }
            }

            _surfaceBg.Initialize();

            IsWorldReady = true;
            OnWorldReady?.Invoke();

            // Refresh rendering and camera bounds
            PlayerCamera.Instance.InvokeCurrentBoundsRefresh();
        }

        public bool TryGetPlayerSpawnPosition(Vector2 colliderSize, out Vector2 spawnPosition)
        {
            spawnPosition = default;

            if (!IsWorldReady || FgGrid == null)
                return false;

            // SmallIslandsGenStep generates an island at this center column.
            int centerX = FgGrid.Width / 2;
            Vector2 halfSize = colliderSize * 0.5f;

            // Start at the top of the world and search down the center column.
            for (int y = FgGrid.Height - 1; y >= 0; y--)
            {
                TileData ground = FgGrid.GetTileData(centerX, y);

                if (!ground.HasTile || !ground.IsSolid)
                    continue;

                // Put the character's feet right on top of the solid tile.
                Vector2 candidate = new(centerX + 0.5f, y + 1f + halfSize.y);

                if (!IsSpawnAreaClear(candidate, colliderSize))
                    continue;

                spawnPosition = candidate;
                return true;
            }

            return false;
        }

        private bool IsSpawnAreaClear(Vector2 position, Vector2 colliderSize)
        {
            Vector2 halfSize = colliderSize * 0.5f;

            int minX = Mathf.FloorToInt(position.x - halfSize.x);
            int maxX = Mathf.CeilToInt(position.x + halfSize.x) - 1;
            int minY = Mathf.FloorToInt(position.y - halfSize.y);
            int maxY = Mathf.CeilToInt(position.y + halfSize.y) - 1;

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    if (!FgGrid.IsInBounds(x, y))
                        return false;

                    TileData tile = FgGrid.GetTileData(x, y);

                    if (tile.HasTile && tile.IsSolid)
                        return false;
                }
            }

            return true;
        }

        public void DamageTile(Vector2Int position, int damageAmount, LayerType layerType, bool refreshCurrentBounds = false)
        {
            TileGrid layer = layerType == LayerType.Foreground ? FgGrid : BgGrid;
            layer.DamageTile(position.x, position.y, damageAmount, refreshCurrentBounds);
        }
    }
    
    
}
