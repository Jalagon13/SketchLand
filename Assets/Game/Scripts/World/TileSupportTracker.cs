using System;
using UnityEngine;

namespace SketchLand
{
    /// <summary>Rechecks foreground attachments when either layer changes, including offscreen cells.</summary>
    public sealed class TileSupportTracker : IDisposable
    {
        private readonly TileGrid _foreground;
        private readonly TileGrid _background;

        public TileSupportTracker(TileGrid foreground, TileGrid background)
        {
            _foreground = foreground;
            _background = background;
            _foreground.OnTileDataChanged += OnTileChanged;
            _background.OnTileDataChanged += OnTileChanged;
        }

        public void Dispose()
        {
            _foreground.OnTileDataChanged -= OnTileChanged;
            _background.OnTileDataChanged -= OnTileChanged;
        }

        private void OnTileChanged(Vector2Int position)
        {
            Check(position);
            Check(position + Vector2Int.left);
            Check(position + Vector2Int.right);
            Check(position + Vector2Int.down);
            Check(position + Vector2Int.up);
        }

        private void Check(Vector2Int position)
        {
            TileData tile = _foreground.GetTileData(position.x, position.y);
            if (tile.HasTile)
                tile.TileConfig?.OnSupportChanged(_foreground, _background, position);
        }
    }
}
