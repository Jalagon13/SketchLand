using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SketchLand
{
    public sealed class TerrainMeshBuilder
    {
        private static readonly Vector2[] Corners =
        {
            Vector2.zero,
            Vector2.right,
            Vector2.one,
            Vector2.up
        };

        private static readonly Vector2Int[] Neighbours =
        {
            Vector2Int.down,
            Vector2Int.right,
            Vector2Int.up,
            Vector2Int.left
        };

        private readonly List<Vector3> _vertices = new();
        private readonly List<Vector2> _uvs = new();
        private readonly List<Color> _colors = new();

        private readonly List<Material> _materials = new();
        private readonly List<List<int>> _triangles = new();
        private readonly Dictionary<Material, int> _materialIndices = new();

        private readonly List<int> _inkTriangles = new();
        private readonly List<Vector2> _polygon = new();

        private readonly TerrainTileConfigSO[] _neighbourConfigs = new TerrainTileConfigSO[4];
        private readonly TileData[] _neighbourData = new TileData[4];
        private readonly List<Vector2> _edgePoints = new();
        private readonly HashSet<(Vector2, float, Color)> _inkJoints = new();

        private Vector2 _origin;

        public static TerrainTileConfigSO GetVisualConfig(TileData tile)
        {
            if (!tile.HasTile || GameDataRegistry.Instance == null)
                return null;

            if (tile.TileConfig is not TerrainTileConfigSO config)
                return null;

            // This renderer currently supports only 1x1 terrain cells.
            if (config.IsMultiTile)
                return null;

            return config;
        }

        public Material[] Build(
            TileGrid grid,
            RectInt region,
            Mesh mesh,
            Mesh inkMesh,
            Func<TerrainTileConfigSO, Material> getMaterial)
        {
            _vertices.Clear();
            _uvs.Clear();
            _colors.Clear();

            _materials.Clear();
            _triangles.Clear();
            _materialIndices.Clear();
            _inkTriangles.Clear();
            _inkJoints.Clear();

            _origin = new Vector2(region.xMin, region.yMin);

            for (int y = region.yMin; y < region.yMax; y++)
            {
                for (int x = region.xMin; x < region.xMax; x++)
                {
                    TileData tile = grid.GetTileData(x, y);
                    TerrainTileConfigSO config = GetVisualConfig(tile);

                    if (config == null)
                        continue;

                    Material material = getMaterial(config);
                    int materialIndex = GetMaterialIndex(material);

                    AddCell(grid, x, y, tile, config, materialIndex);
                }
            }

            mesh.Clear();
            mesh.indexFormat = IndexFormat.UInt32;

            mesh.SetVertices(_vertices);
            mesh.SetUVs(0, _uvs);
            mesh.SetColors(_colors);

            mesh.subMeshCount = _triangles.Count;

            for (int i = 0; i < _triangles.Count; i++)
            {
                mesh.SetTriangles(_triangles[i], i, false);
            }

            mesh.RecalculateBounds();

            inkMesh.Clear();
            inkMesh.indexFormat = IndexFormat.UInt32;
            if (_inkTriangles.Count > 0)
            {
                inkMesh.SetVertices(_vertices);
                inkMesh.SetUVs(0, _uvs);
                inkMesh.SetColors(_colors);
                inkMesh.subMeshCount = 1;
                inkMesh.SetTriangles(_inkTriangles, 0, false);
                inkMesh.RecalculateBounds();
            }

            return _materials.ToArray();
        }

        private int GetMaterialIndex(Material material)
        {
            if (_materialIndices.TryGetValue(material, out int index))
                return index;

            index = _materials.Count;

            _materialIndices.Add(material, index);
            _materials.Add(material);
            _triangles.Add(new List<int>());

            return index;
        }

        private void AddCell(
            TileGrid grid,
            int x,
            int y,
            TileData tile,
            TerrainTileConfigSO config,
            int materialIndex)
        {
            for (int i = 0; i < 4; i++)
            {
                Vector2Int offset = Neighbours[i];

                _neighbourData[i] =
                    grid.GetTileData(x + offset.x, y + offset.y);

                _neighbourConfigs[i] =
                    GetVisualConfig(_neighbourData[i]);
            }

            Vector2 cellOrigin = new Vector2(x, y);

            float radius = Mathf.Clamp(config.CornerSoftness, 0f, 0.2f);

            _polygon.Clear();

            for (int edge = 0; edge < 4; edge++)
            {
                int next = (edge + 1) % 4;
                int previous = (edge + 3) % 4;

                Vector2 a = cellOrigin + Corners[edge];
                Vector2 b = cellOrigin + Corners[next];
                TerrainTileConfigSO neighbour = _neighbourConfigs[edge];

                bool boundary = neighbour != config;
                bool exposed = neighbour == null;

                bool roundStart =
                    exposed && _neighbourConfigs[previous] == null;

                bool roundEnd =
                    exposed && _neighbourConfigs[next] == null;

                TerrainTileConfigSO edgeStyle = ResolveStyle(
                    config,
                    tile.TileId,
                    neighbour,
                    _neighbourData[edge].TileId);

                BuildEdgePoints(a, b,
                    roundStart ? radius : 0f,
                    roundEnd ? radius : 0f,
                    boundary, edgeStyle);

                _polygon.AddRange(_edgePoints);

                // Each shared edge is owned by its left/bottom cell.
                bool ownsBoundary =
                    exposed || edge == 1 || edge == 2;

                if (boundary && ownsBoundary)
                {
                    float width = edgeStyle.OutlineWidth;

                    if (!exposed)
                        width *= 0.65f;

                    for (int i = 0; i < _edgePoints.Count - 1; i++)
                    {
                        AddInkStrip(
                            _edgePoints[i],
                            _edgePoints[i + 1],
                            width,
                            edgeStyle.OutlineColor);
                    }
                }

                if (roundEnd)
                {
                    Vector2 following =
                        b +
                        (Corners[(next + 1) % 4] - Corners[next])
                        * radius;

                    Vector2 start = _edgePoints[_edgePoints.Count - 1];
                    Vector2 last = start;
                    // The fill and ink follow exactly the same rounded corner.
                    for (int i = 1; i <= 6; i++)
                    {
                        float t = i / 6f;
                        Vector2 point = (1f - t) * (1f - t) * start
                            + 2f * (1f - t) * t * b + t * t * following;
                        if (i < 6)
                            _polygon.Add(point);
                        AddInkStrip(last, point, config.OutlineWidth, config.OutlineColor);
                        last = point;
                    }
                }
            }

            Vector2 textureSize = new Vector2(
                Mathf.Max(0.01f, config.TextureWorldSize.x),
                Mathf.Max(0.01f, config.TextureWorldSize.y));

            int center = AddFillVertex(
                cellOrigin + Vector2.one * 0.5f,
                config,
                textureSize);

            List<int> triangles = _triangles[materialIndex];

            for (int i = 0; i < _polygon.Count; i++)
            {
                int a = AddFillVertex(
                    _polygon[i], config, textureSize);

                int b = AddFillVertex(
                    _polygon[(i + 1) % _polygon.Count],
                    config,
                    textureSize);

                triangles.Add(center);
                triangles.Add(a);
                triangles.Add(b);
            }
        }

        private static TerrainTileConfigSO ResolveStyle(
            TerrainTileConfigSO a,
            ushort aId,
            TerrainTileConfigSO b,
            ushort bId)
        {
            if (b == null)
                return a;

            float aWobble = Mathf.Max(0f, a.OutlineWobble);
            float bWobble = Mathf.Max(0f, b.OutlineWobble);

            if (aWobble < bWobble)
                return a;

            if (bWobble < aWobble)
                return b;

            // Stable tie-breaker, independent of processing order.
            return aId <= bId ? a : b;
        }

        private void BuildEdgePoints(Vector2 a, Vector2 b, float startCut,
            float endCut, bool boundary, TerrainTileConfigSO style)
        {
            _edgePoints.Clear();
            Vector2 direction = b - a;
            _edgePoints.Add(a + direction * startCut);
            float wavelength = Mathf.Max(0.1f, style.OutlineWavelength);
            int segments = boundary && style.OutlineWobble > 0f
                ? Mathf.Clamp(Mathf.CeilToInt(6f / wavelength), 8, 64) : 1;
            bool horizontal = Mathf.Abs(direction.x) > 0.5f;
            bool forward = horizontal ? b.x > a.x : b.y > a.y;
            Vector2 minimum = Vector2.Min(a, b);
            Vector2 axis = horizontal ? Vector2.right : Vector2.up;
            for (int i = 1; i < segments; i++)
            {
                float t = i / (float)segments;
                if (t <= startCut || t >= 1f - endCut)
                    continue;
                // Canonical coordinates make both sides of a material seam identical.
                int canonicalIndex = forward ? i : segments - i;
                Vector2 point = minimum + axis * (canonicalIndex / (float)segments);
                float localT = (t - startCut) / (1f - startCut - endCut);
                _edgePoints.Add(WarpEdge(point, horizontal, style,
                    Mathf.Sin(localT * Mathf.PI)));
            }
            _edgePoints.Add(b - direction * endCut);
        }

        private static Vector2 WarpEdge(Vector2 point, bool horizontal,
        TerrainTileConfigSO style, float envelope)
        {
            float amplitude = Mathf.Clamp(style.OutlineWobble, 0f, 0.08f);
            float wavelength = Mathf.Max(0.1f, style.OutlineWavelength);
            float along = horizontal ? point.x : point.y;
            float across = horizontal ? point.y : point.x;
            float phase = along * (2f * Mathf.PI / wavelength) + across * 1.73f;
            float wave = (Mathf.Sin(phase)
                + 0.35f * Mathf.Sin(phase * 1.93f + across * 0.37f)) / 1.35f;
            return point + (horizontal ? Vector2.up : Vector2.right)
                * (wave * amplitude * envelope);
        }

        private int AddFillVertex(
            Vector2 point,
            TerrainTileConfigSO config,
            Vector2 textureSize)
        {
            Vector2 uv = new Vector2(
                point.x / textureSize.x,
                point.y / textureSize.y);

            uv += config.TextureOffset;

            return AddVertex(point, uv, config.FillTint);
        }

        private int AddVertex(Vector2 point, Vector2 uv, Color color)
        {
            int index = _vertices.Count;

            _vertices.Add(new Vector3(
                point.x - _origin.x,
                point.y - _origin.y,
                0f));

            _uvs.Add(uv);
            _colors.Add(color);

            return index;
        }

        private void AddInkStrip(
            Vector2 a,
            Vector2 b,
            float width,
            Color color)
        {
            Vector2 direction = b - a;

            if (direction.sqrMagnitude < 0.000001f)
                return;

            width = Mathf.Clamp(width, 0.001f, 0.12f);

            // Center ink on the shared fill boundary, hiding its rasterized edge.
            Vector2 inward = new Vector2(
                -direction.y, direction.x).normalized;

            float feather = 0.012f;
            float halfWidth = width * 0.5f;

            float[] distances =
            {
                -halfWidth - feather,
                -halfWidth,
                halfWidth,
                halfWidth + feather
            };

            int first = _vertices.Count;

            for (int i = 0; i < distances.Length; i++)
            {
                Color vertexColor = color;

                if (i == 0 || i == distances.Length - 1)
                    vertexColor.a = 0f;

                Vector2 offset = inward * distances[i];

                AddVertex(a + offset, Vector2.zero, vertexColor);
                AddVertex(b + offset, Vector2.zero, vertexColor);
            }

            for (int i = 0; i < 3; i++)
            {
                int k = first + i * 2;

                _inkTriangles.Add(k);
                _inkTriangles.Add(k + 1);
                _inkTriangles.Add(k + 2);

                _inkTriangles.Add(k + 1);
                _inkTriangles.Add(k + 3);
                _inkTriangles.Add(k + 2);
            }
            AddInkJoint(a, width, color);
            AddInkJoint(b, width, color);
        }

        private void AddInkJoint(Vector2 point, float width, Color color)
        {
            if (!_inkJoints.Add((point, width, color)))
                return;
            const int segments = 16;
            float radius = width * 0.5f;
            Color transparent = color;
            transparent.a = 0f;
            int center = AddVertex(point, Vector2.zero, color);
            int first = _vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * (2f * Mathf.PI / segments);
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                AddVertex(point + direction * radius, Vector2.zero, color);
                AddVertex(point + direction * (radius + 0.012f), Vector2.zero, transparent);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = first + i * 2;
                _inkTriangles.Add(center);
                _inkTriangles.Add(a);
                _inkTriangles.Add(a + 2);
                _inkTriangles.Add(a);
                _inkTriangles.Add(a + 1);
                _inkTriangles.Add(a + 2);
                _inkTriangles.Add(a + 1);
                _inkTriangles.Add(a + 3);
                _inkTriangles.Add(a + 2);
            }
        }
    }
}
