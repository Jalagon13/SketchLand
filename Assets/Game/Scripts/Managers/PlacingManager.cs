using UnityEngine;

namespace SketchLand
{
    public class PlacingManager : MonoBehaviour
    {
        public static PlacingManager Instance { get; private set; }

        [SerializeField] private float _placementsPerSecond = 4f;

        private float _nextPlaceTime;
        private bool _isPlacing;

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
            if (newValue == LifeState.Dead)
            {
                StopPlacing();
            }
        }

        public void StartPlacing(TileItemSO tileItem)
        {
            _isPlacing = true;
            TryPerformPlace(tileItem); // Instantly attempt placement on first click
        }

        public void TickPlacing(TileItemSO tileItem)
        {
            if (!_isPlacing) return;

            TryPerformPlace(tileItem);
        }

        public void StopPlacing()
        {
            _isPlacing = false;
        }

        private bool TryPerformPlace(TileItemSO tileItem)
        {
            if (tileItem == null || tileItem.PlaceTileDataSO == null) return false;
            if (Time.time < _nextPlaceTime) return false;
            if (WorldManager.Instance == null || WorldManager.Instance.MouseOverUI) return false;

            Vector2 mouseWorldPos = WorldManager.MouseWorldPosition;
            Vector2Int mouseTilePos = WorldManager.MouseWorldTilePosition;

            float distanceToPlayer = Vector2.Distance(Player.Instance.Character.transform.position, mouseWorldPos);
            if (distanceToPlayer > Player.Instance.InteractRange) return false;

            var world = WorldManager.Instance;
            var activeLayer = world.ActiveLayer;
            var targetGrid = world.ActiveGrid;
            var tileConfig = tileItem.PlaceTileDataSO;

            // Only 1x1 non-multitile tiles can be placed on the background layer
            if (activeLayer == WorldManager.LayerType.Background && tileConfig.IsMultiTile) return false;

            // Check if the targeted layer already has a tile at this cell
            var targetTileData = targetGrid.GetTileData(mouseTilePos.x, mouseTilePos.y);
            if(targetTileData.HasTile) return false;

            Vector2Int placementSize = tileConfig.Size;

            // Make sure entity overlap check only works for solid foreground tiles
            if (activeLayer == WorldManager.LayerType.Foreground && tileConfig.IsSolid && HasEntityOverlappingTileArea(mouseTilePos, placementSize))
            {
                return false;
            }

            if (!tileConfig.CanPlace(world.FgGrid, world.BgGrid, mouseTilePos, activeLayer)) return false;

            // If everything is good then we can proceed to place the tile
            var tileToPlaceTd = new TileData(tileConfig.GetId());

            if (activeLayer == WorldManager.LayerType.Foreground && tileConfig.IsMultiTile)
            {
                bool placed = world.FgGrid.PlaceMultiTileData(mouseTilePos.x, mouseTilePos.y, tileToPlaceTd, refreshCurrentBounds: true, manualyPlaced: true);
                if (!placed) return false;
            }
            else
            {
                targetGrid.SetTileData(mouseTilePos.x, mouseTilePos.y, tileToPlaceTd, refreshCurrentBounds: true, manualyPlaced: true);
            }

            AudioManager.Instance.PlayOneShot(tileItem.PlaceTileDataSO.TileHitSFX, new(mouseTilePos.x, mouseTilePos.y));
            InventoryInputManager.Instance.GetActiveInvSlot().RemoveFromCurrentAmount(1);
            InventoryManager.Instance.RefreshInventory();

            // Resolve selection after consumption so the last item doesn't cancel the swing.
            var activeSlot = InventoryInputManager.Instance.GetActiveInvSlot();
            Player.Instance.ArmHandler.Swing(activeSlot.IsEmpty ? null : activeSlot.GetItemSO(), applyDamage: false);

            float interval = _placementsPerSecond > 0f ? 1f / _placementsPerSecond : 0.25f;
            _nextPlaceTime = Time.time + interval;
            
            return true;
        }

        private static bool HasEntityOverlappingTileArea(Vector2Int tilePosition, Vector2Int tileSize)
        {
            var entityManager = EntityManager.Instance;

            // Don't allow placement when entity occupancy can't be checked.
            if (entityManager == null) return true;

            Vector2 tileAreaCenter = new(tilePosition.x + tileSize.x * 0.5f, tilePosition.y + tileSize.y * 0.5f);
            Vector2 tileAreaSize = new(tileSize.x, tileSize.y);
            var entities = entityManager.Entities;

            for (int i = 0; i < entities.Count; i++)
            {
                Entity entity = entities[i];
                if (entity == null) continue;

                if (GridPhysics.IsOverlapping(tileAreaCenter, tileAreaSize, entity.transform.position, entity.ColliderSize))
                {
                    return true;
                }
            }

            return false;
        }

    }
}
