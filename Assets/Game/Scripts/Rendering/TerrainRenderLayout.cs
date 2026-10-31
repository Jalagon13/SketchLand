using UnityEngine;

namespace SketchLand
{
    /// <summary>
    /// Read-only rendering view. Background walls are the union of centered 1.8x1.8
    /// footprints, partitioned at footprint edges and material midpoints so fills never overlap.
    /// Gameplay, placement, lighting and persistence continue to use the original grid.
    /// </summary>
    internal readonly struct TerrainRenderLayout
    {
        private readonly TileGrid _grid;
        private readonly bool _background;
        private const float BackgroundOverhang = 0.4f;

        private float Coordinate(int index)
        {
            if (!_background) return index;
            int cell = Mathf.FloorToInt(index / 4f);
            int part = index - cell * 4;
            return cell + (part == 0 ? 0f : part == 1 ? BackgroundOverhang : part == 2 ? 0.5f : 1f - BackgroundOverhang);
        }

        public Rect GetCellBounds(int x, int y)
        {
            return Rect.MinMaxRect(Coordinate(x), Coordinate(y), Coordinate(x + 1), Coordinate(y + 1));
        }

        public TerrainRenderLayout(TileGrid grid, bool background)
        {
            _grid = grid;
            _background = background;
        }

        public RectInt GetRegion(RectInt region)
        {
            if (!_background) return region;

            // Chunk ownership remains disjoint. Only world-edge chunks own the
            // 0.4-unit overhang outside the data grid.
            var result = new RectInt();
            result.SetMinMax(new Vector2Int(
                region.xMin * 4 - (region.xMin == 0 ? 1 : 0),
                region.yMin * 4 - (region.yMin == 0 ? 1 : 0)), new Vector2Int(
                region.xMax * 4 + (region.xMax == _grid.Width ? 1 : 0),
                region.yMax * 4 + (region.yMax == _grid.Height ? 1 : 0)));
            return result;
        }

        public TileData Sample(int x, int y)
        {
            if (!_background) return _grid.GetTileData(x, y);

            return TryGetSource(GetCellBounds(x, y).center, out Vector2Int source)
                ? _grid.GetTileData(source.x, source.y)
                : TileData.Air;
        }

        /// <summary>Maps a visual footprint back to its original data cell.</summary>
        public bool TryGetSource(Vector2 worldPoint, out Vector2Int source)
        {
            source = Vector2Int.FloorToInt(worldPoint);
            if (!_background)
                return _grid.GetTileData(source.x, source.y).HasTile;

            // Use the same partition as the mesh, including at diagonal overlaps.
            // Picking at the raw point could choose a different source within a face.
            Vector2 point = GetCellBounds(RenderIndex(worldPoint.x), RenderIndex(worldPoint.y)).center;
            int firstX = Mathf.FloorToInt(point.x - 0.5f);
            int firstY = Mathf.FloorToInt(point.y - 0.5f);
            bool found = false;
            float nearest = float.PositiveInfinity;

            // At most four source footprints can cover a rendering cell. Prefer the
            // nearest source center, with stable bottom/left ownership on ties.
            // This preserves material seams without drawing ink inside one material.
            for (int sy = firstY; sy <= firstY + 1; sy++)
            {
                for (int sx = firstX; sx <= firstX + 1; sx++)
                {
                    TileData tile = _grid.GetTileData(sx, sy);
                    if (TerrainMeshBuilder.GetVisualConfig(tile) == null) continue;
                    Vector2 delta = point - new Vector2(sx + 0.5f, sy + 0.5f);
                    float halfSize = 0.5f + BackgroundOverhang;
                    if (Mathf.Abs(delta.x) > halfSize || Mathf.Abs(delta.y) > halfSize) continue;
                    float distance = delta.sqrMagnitude;
                    if (distance >= nearest) continue;
                    nearest = distance;
                    source = new Vector2Int(sx, sy);
                    found = true;
                }
            }
            return found;
        }

        private static int RenderIndex(float coordinate)
        {
            int cell = Mathf.FloorToInt(coordinate);
            float fraction = coordinate - cell;
            int part = fraction < BackgroundOverhang ? 0 : fraction < 0.5f ? 1 : fraction < 1f - BackgroundOverhang ? 2 : 3;
            return cell * 4 + part;
        }
    }
}
