using UnityEngine;
using UnityEngine.Tilemaps;

namespace SketchLand
{
    [RequireComponent(typeof(Tilemap))]
    public sealed class DamageOverlayRenderer : MonoBehaviour
    {
        [SerializeField] private TileBase[] _crackTiles;

        private Tilemap _tilemap;
        private RectInt _previousBounds;

        private void Awake()
        {
            _tilemap = GetComponent<Tilemap>();
        }

        public void Render(TileGrid grid, RectInt bounds)
        {
            // Remove decals that left the rendered region.
            for (int y = _previousBounds.yMin; y < _previousBounds.yMax; y++)
            {
                for (int x = _previousBounds.xMin; x < _previousBounds.xMax; x++)
                {
                    if (!bounds.Contains(new Vector2Int(x, y)))
                    {
                        _tilemap.SetTile(new Vector3Int(x, y, 0), null);
                    }
                }
            }

            int minX = Mathf.Max(0, bounds.xMin);
            int minY = Mathf.Max(0, bounds.yMin);
            int maxX = Mathf.Min(grid.Width, bounds.xMax);
            int maxY = Mathf.Min(grid.Height, bounds.yMax);

            for (int y = minY; y < maxY; y++)
            {
                for (int x = minX; x < maxX; x++)
                {
                    _tilemap.SetTile(new Vector3Int(x, y, 0), GetCrackTile(grid, x, y));
                }
            }

            _previousBounds = new RectInt(minX, minY, Mathf.Max(0, maxX - minX), Mathf.Max(0, maxY - minY));
        }

        private TileBase GetCrackTile(TileGrid grid, int x, int y)
        {
            if (_crackTiles == null || _crackTiles.Length == 0)
                return null;

            TileData tile = grid.GetTileData(x, y);

            if (!tile.HasTile)
                return null;

            TileConfigSO config = tile.TileConfig;

            if (config == null || config.MaxHP <= 0)
                return null;

            Vector2Int damagePosition = config.IsMultiTile ? new Vector2Int(x - tile.OffsetX, y - tile.OffsetY) : new Vector2Int(x, y);

            if (!grid.DamagedTiles.TryGetValue(damagePosition, out TileDamageState damage) || damage.Damage <= 0)
            {
                return null;
            }

            float ratio = Mathf.Clamp01((float)damage.Damage / config.MaxHP);

            int index = Mathf.Clamp(Mathf.FloorToInt(ratio * _crackTiles.Length), 0, _crackTiles.Length - 1);

            return _crackTiles[index];
        }

        public void Clear()
        {
            if (_tilemap != null)
                _tilemap.ClearAllTiles();

            _previousBounds = default;
        }
    }
}