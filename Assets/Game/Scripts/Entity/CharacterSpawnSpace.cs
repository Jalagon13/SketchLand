using UnityEngine;

namespace SketchLand
{
    public static class CharacterSpawnSpace
    {
        // Reserve the entire body plus its eight surrounding neighbors, not only the center tile.
        public static bool IsOpenSurfaceAir(TileGrid foreground, TileGrid background, WorldZone zone, Vector2 center, Vector2 size)
        {
            if (!zone.AllowsNaturalDaylight) return false;
            Vector2 half = size * 0.5f;
            int minX = Mathf.FloorToInt(center.x - half.x) - 1;
            int maxX = Mathf.CeilToInt(center.x + half.x);
            int minY = Mathf.FloorToInt(center.y - half.y) - 1;
            int maxY = Mathf.CeilToInt(center.y + half.y);
            for (int x = minX; x <= maxX; x++)
            for (int y = minY; y <= maxY; y++)
            {
                if (!foreground.IsInBounds(x, y) || !background.IsInBounds(x, y) || y < zone.MinY || y >= zone.MaxY)
                    return false;
                if (foreground.GetTileData(x, y).HasTile || background.GetTileData(x, y).HasTile)
                    return false;
            }
            return true;
        }
    }
}
