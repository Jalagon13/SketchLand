using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SketchLand
{
    [RequireComponent(typeof(Tilemap))]
    public sealed class SpriteTileRenderer : MonoBehaviour
    {
        private Tilemap _tilemap;

        private readonly HashSet<Vector3Int> _drawn = new();
        private readonly HashSet<Vector3Int> _next = new();

        private void Awake()
        {
            _tilemap = GetComponent<Tilemap>();
        }

        public void Render(TileGrid grid, RectInt bounds)
        {
            _next.Clear();

            int minX = Mathf.Max(0, bounds.xMin);
            int minY = Mathf.Max(0, bounds.yMin);
            int maxX = Mathf.Min(grid.Width, bounds.xMax);
            int maxY = Mathf.Min(grid.Height, bounds.yMax);

            for (int y = minY; y < maxY; y++)
            {
                for (int x = minX; x < maxX; x++)
                {
                    TileData tile = grid.GetTileData(x, y);

                    if (!tile.HasTile || tile.TileConfig is not SpriteTileConfigSO config)
                    {
                        continue;
                    }

                    Vector3Int root = config.IsMultiTile ? new Vector3Int(x - tile.OffsetX, y - tile.OffsetY, 0) : new Vector3Int(x, y, 0);

                    // Several visible cells can reference the same object.
                    if (!_next.Add(root))
                        continue;

                    TileData rootTile = grid.GetTileData(root.x, root.y);

                    _tilemap.SetTile(root, config.GetStateInterpretedTileForRendering(rootTile.State));
                }
            }

            foreach (Vector3Int position in _drawn)
            {
                if (!_next.Contains(position))
                {
                    _tilemap.SetTile(position, null);
                }
            }

            _drawn.Clear();
            _drawn.UnionWith(_next);
        }

        public void Clear()
        {
            if (_tilemap != null)
                _tilemap.ClearAllTiles();

            _drawn.Clear();
            _next.Clear();
        }
    }
}