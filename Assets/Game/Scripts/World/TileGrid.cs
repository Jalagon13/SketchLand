using System;
using System.Collections.Generic;
using UnityEngine;

namespace SketchLand
{
    public class TileGrid
    {
        public event Action<Vector2Int> OnTileDestroyed;
        public event Action<Vector2Int> OnTilePlaced;
        public event Action<Vector2Int> OnTileDataChanged;
        public event Action OnDamageChanged;

        private readonly TileData[] _tiles;
        private readonly int _width;
        private readonly int _height;
        private readonly List<Vector2Int> _expiredDamagePositions = new();

        public int Width => _width;
        public int Height => _height;

        public Dictionary<Vector2Int, TileDamageState> DamagedTiles { get; } = new();

        public TileGrid(int width, int height)
        {
            _width = width;
            _height = height;

            _tiles = new TileData[width * height];
        }

        public TileData GetTileData(int x, int y)
        {
            if(!IsInBounds(x, y)) return TileData.OutOfBounds;
        
            return _tiles[y * _width + x];
        }

        public void DamageTile(int x, int y, int damageAmount, bool refreshCurrentBounds = true)
        {
            if (!IsInBounds(x, y)) return;

            TileData targetTile = GetTileData(x, y);
            if (targetTile.IsAir) return;

            TileConfigSO tc = targetTile.TileConfig;
            if (tc == null || tc.Indestructible || tc.MaxHP <= 0) return;

            // For rigid multi-tiles that share HP across the whole structure,
            // route damage to the root cell. (Trees won't use IsMultiTile so they route damage to (x, y)!)
            int damageX = tc.IsMultiTile ? x - targetTile.OffsetX : x;
            int damageY = tc.IsMultiTile ? y - targetTile.OffsetY : y;
            Vector2Int damagePos = new(damageX, damageY);

            DamagedTiles.TryGetValue(damagePos, out TileDamageState damageState);
            int currentDamage = damageState.Damage;
            currentDamage += damageAmount;

            if (currentDamage >= tc.MaxHP)
            {
                // Clear damage record for this tile/root
                DamagedTiles.Remove(damagePos);

                // Destroy the tile via DestroyTile (which delegates to TileConfigSO)
                DestroyTile(x, y, refreshCurrentBounds);
            }
            else
            {
                DamagedTiles[damagePos] = new TileDamageState(currentDamage, Time.time);
                OnDamageChanged?.Invoke();

                AudioManager.Instance.PlayOneShot(tc.TileHitSFX, new(damagePos.x, damagePos.y));

                if (refreshCurrentBounds && PlayerCamera.Instance.IsPositionInBounds(x, y))
                {
                    PlayerCamera.Instance.InvokeCurrentBoundsRefresh();
                }
            }
        }

        public void RestoreExpiredDamage(float currentTime, float repairDelay)
        {
            _expiredDamagePositions.Clear();

            foreach (var entry in DamagedTiles)
            {
                if (currentTime - entry.Value.LastDamagedTime >= repairDelay)
                    _expiredDamagePositions.Add(entry.Key);
            }

            if (_expiredDamagePositions.Count == 0)
                return;

            bool refreshedVisibleDamage = false;
            for (int i = 0; i < _expiredDamagePositions.Count; i++)
            {
                Vector2Int position = _expiredDamagePositions[i];
                DamagedTiles.Remove(position);

                if (!refreshedVisibleDamage && IsDamagePositionVisible(position))
                {
                    refreshedVisibleDamage = true;
                }
            }

            OnDamageChanged?.Invoke();

            if (refreshedVisibleDamage)
                PlayerCamera.Instance.InvokeCurrentBoundsRefresh();
        }

        private bool IsDamagePositionVisible(Vector2Int position)
        {
            if (PlayerCamera.Instance == null) return false;

            TileData tileData = GetTileData(position.x, position.y);
            TileConfigSO tileConfig = tileData.HasTile ? tileData.TileConfig : null;
            if (tileConfig == null || !tileConfig.IsMultiTile)
            {
                return PlayerCamera.Instance.IsPositionInBounds(position.x, position.y);
            }

            Vector2Int size = tileConfig.Size;
            for (int offsetX = 0; offsetX < size.x; offsetX++)
            {
                for (int offsetY = 0; offsetY < size.y; offsetY++)
                {
                    if (PlayerCamera.Instance.IsPositionInBounds(position.x + offsetX, position.y + offsetY))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public void DestroyTile(int x, int y, bool refreshCurrentBounds = false)
        {
            if (!IsInBounds(x, y)) return;

            TileData targetTile = _tiles[y * _width + x];
            if (targetTile.IsAir) return;

            TileConfigSO tc = targetTile.TileConfig;

            if (tc != null)
            {
                // Let the TileConfig handle clearing the cells and dropping items!
                tc.OnTileDestroyed(this, x, y, targetTile, refreshCurrentBounds);

                // NTFS: Probably move this to a client side game feel area later
                AudioManager.Instance.PlayOneShot(tc.TileDestroyedSFX, new(x, y)); 
            }
            else
            {
                // Fallback for tiles without config
                WriteTile(x, y, TileData.Air);
                ClearDamage(x, y);
            }

            // Grid-level event notification (for audio, chunk updates, etc.)
            OnTileDestroyed?.Invoke(new Vector2Int(x, y));

            if (refreshCurrentBounds && PlayerCamera.Instance.IsPositionInBounds(x, y))
            {
                PlayerCamera.Instance.InvokeCurrentBoundsRefresh();
            }
        }

        public void SetTileData(int x, int y, TileData newTileData, bool refreshCurrentBounds = false, bool manualyPlaced = false)
        {
            if (!IsInBounds(x, y)) return;

            WriteTile(x, y, newTileData);

            if (manualyPlaced)
            {
                OnTilePlaced?.Invoke(new(x, y));
            }

            if (refreshCurrentBounds && PlayerCamera.Instance.IsPositionInBounds(x, y))
            {
                PlayerCamera.Instance.InvokeCurrentBoundsRefresh();
            }
        }

        public void SetTileDataDirect(int x, int y, TileData newTileData)
        {
            if (!IsInBounds(x, y)) return;
            WriteTile(x, y, newTileData);
        }

        public void ChangeMultiTileData(int x, int y, TileData newTileData, bool refreshCurrentBounds = false)
        {
            if (!IsInBounds(x, y)) return;

            TileConfigSO newTc = newTileData.TileConfig;

            if (!newTc.IsMultiTile) return;

            Vector2Int size = newTc.Size;
            
            int rootX = x - newTileData.OffsetX;
            int rootY = y - newTileData.OffsetY;

            // Update the State on ALL cells of the multi-tile
            for (int ox = 0; ox < size.x; ox++)
            {
                for (int oy = 0; oy < size.y; oy++)
                {
                    int targetX = rootX + ox;
                    int targetY = rootY + oy;

                    if (IsInBounds(targetX, targetY))
                    {
                        newTileData.OffsetX = (byte)ox;
                        newTileData.OffsetY = (byte)oy;
                        WriteTile(targetX, targetY, newTileData);
                    }
                }
            }

            if (refreshCurrentBounds && PlayerCamera.Instance.IsPositionInBounds(x, y))
            {
                PlayerCamera.Instance.InvokeCurrentBoundsRefresh();
            }
        }

        public bool PlaceMultiTileData(int x, int y, TileData newTileData, bool refreshCurrentBounds = false, bool manualyPlaced = false)
        {
            if (!IsInBounds(x, y)) return false;
            
            TileConfigSO newTc = newTileData.TileConfig;
            
            if(!newTc.IsMultiTile) return false;
            if(!CanMultiTileFit(x, y, newTc)) return false;

            Vector2Int size = newTc.Size;

            // Update the State on ALL cells of the multi-tile
            for (int ox = 0; ox < size.x; ox++)
            {
                for (int oy = 0; oy < size.y; oy++)
                {
                    int targetX = x + ox;
                    int targetY = y + oy;

                    if (IsInBounds(targetX, targetY))
                    {
                        newTileData.OffsetX = (byte)ox;
                        newTileData.OffsetY = (byte)oy;
                        WriteTile(targetX, targetY, newTileData);
                    }
                }
            }
            
            if(manualyPlaced)
            {
                OnTilePlaced?.Invoke(new(x, y));
            }

            if (refreshCurrentBounds && PlayerCamera.Instance.IsPositionInBounds(x, y))
            {
                PlayerCamera.Instance.InvokeCurrentBoundsRefresh();
            }

            return true;
        }

        private bool CanMultiTileFit(int x, int y, TileConfigSO tc)
        {
            var size = tc.Size;
            
            for (int ox = 0; ox < size.x; ox++)
            {
                for (int oy = 0; oy < size.y; oy++)
                {
                    int offSetX = x + ox;
                    int offSetY = y + oy;
                    
                    if(!IsInBounds(offSetX, offSetY) || _tiles[offSetY * _width + offSetX].HasTile)
                    {
                        return false;
                    }
                }
            }
            
            return true;
        }

        public bool IsInBounds(int x, int y)
        {
            if (x < 0 || x >= _width || y < 0 || y >= _height)
            {
                return false;
            }

            return true;
        }

        public void ClearDamage(int x, int y)
        {
            if (DamagedTiles.Remove(new Vector2Int(x, y)))
            {
                OnDamageChanged?.Invoke();
            }
        }

        private void WriteTile(int x, int y, TileData value)
        {
            int index = y * _width + x;
            TileData previous = _tiles[index];

            _tiles[index] = value;

            bool visualChanged =
                previous.TileId != value.TileId ||
                previous.State != value.State ||
                previous.OffsetX != value.OffsetX ||
                previous.OffsetY != value.OffsetY;

            if (visualChanged)
            {
                OnTileDataChanged?.Invoke(new Vector2Int(x, y));
            }
        }

    }

    public readonly struct TileDamageState
    {
        public int Damage { get; }
        public float LastDamagedTime { get; }

        public TileDamageState(int damage, float lastDamagedTime)
        {
            Damage = damage;
            LastDamagedTime = lastDamagedTime;
        }
    }

    public struct TileData
    {
        public ushort TileId; // 2 bytes (0 -> 65,535 tile types)
        public byte OffsetX; // 1 byte  (multi-tile width up to 0 -> 255)
        public byte OffsetY; // 1 byte  (multi-tile height up to 0 -> 255)
        public byte State; // 1 byte  (0 -> 255 state variants)
        public byte LightLevel; // 1 byte  (0 -> 255 light emission/block light)
        public bool IsSolid; // 1 byte

        public const ushort AIR_ID = 0;
        public const ushort OUT_OF_BOUNDS_ID = ushort.MaxValue;
        
        public static readonly TileData OutOfBounds = new TileData { TileId = OUT_OF_BOUNDS_ID };
        public static readonly TileData Air = new TileData { TileId = AIR_ID };
        
        public bool IsAir => TileId == AIR_ID;
        public bool IsOutOfBounds => TileId == OUT_OF_BOUNDS_ID;
        public bool HasTile => TileId > AIR_ID && TileId < OUT_OF_BOUNDS_ID;
        public bool IsMultiTileRoot => TileConfig.IsMultiTile && OffsetX == 0 && OffsetY == 0;
        public TileConfigSO TileConfig => GameDataRegistry.Instance.GetTileConfigSOFromTileId(TileId);

        public TileData(ushort tileId, byte offsetX = 0, byte offsetY = 0, byte state = 0)
        {
            this = default; // Need to do this bc i cant access this keyword for initializing some of the stuff below
        
            TileId = tileId;
            OffsetX = offsetX;
            OffsetY = offsetY;
            State = state;
            
            // Config variables automatically set if tileconfig exists
            if(TileConfig != null)
            {
                LightLevel = (byte)(TileConfig.LightLevel * 100); // LightLevel here should be a byte between 0 and 100
                IsSolid = TileConfig.IsSolid;
            }
            else
            {
                Debug.Log($"U fucked up");
            }
        }
    }
}