using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SketchLand
{
    [RequireComponent(typeof(Tilemap), typeof(TilemapRenderer))]
    public sealed class DamageOverlayRenderer : MonoBehaviour
    {
        [SerializeField] private TileBase[] _crackTiles;

        private sealed class Overlay
        {
            public GameObject Root;
            public Mesh Mesh;
            public MeshRenderer Renderer;
        }

        private Tilemap _tilemap;
        private TilemapRenderer _tilemapRenderer;
        private readonly Dictionary<Vector2Int, Overlay> _overlays = new();
        private readonly HashSet<Vector2Int> _visible = new();
        private readonly List<Vector2Int> _remove = new();
        private readonly List<Vector2> _crackUVs = new();
        private readonly TerrainMeshBuilder _terrainBuilder = new();
        private Mesh _scratchInk;
        private MaterialPropertyBlock _properties;
        private bool _tilemapWasEnabled;

        private void Awake()
        {
            _tilemap = GetComponent<Tilemap>();
            _tilemapRenderer = GetComponent<TilemapRenderer>();
            _properties = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            _tilemapWasEnabled = _tilemapRenderer.enabled;
            // Child meshes supply the separate mask/crack UVs a tilemap cannot provide.
            _tilemapRenderer.enabled = false;
        }

        public void Render(TileGrid grid, RectInt bounds, Tilemap objects)
        {
            _visible.Clear();
            foreach (var entry in grid.DamagedTiles)
            {
                Vector2Int position = entry.Key;
                TileData tile = grid.GetTileData(position.x, position.y);
                TileConfigSO config = tile.HasTile ? tile.TileConfig : null;
                if (config == null || config.MaxHP <= 0 || entry.Value.Damage <= 0
                    || _crackTiles == null || _crackTiles.Length == 0
                    || !new RectInt(position, config.Size).Overlaps(bounds))
                    continue;

                float ratio = Mathf.Clamp01((float)entry.Value.Damage / config.MaxHP);
                int stage = Mathf.Clamp(Mathf.FloorToInt(ratio * _crackTiles.Length), 0, _crackTiles.Length - 1);
                Sprite crack = GetCrackSprite(_crackTiles[stage]);
                if (crack == null)
                    continue;

                Overlay overlay = GetOverlay(position);
                Texture mask;
                float alpha;
                if (config is SpriteTileConfigSO)
                {
                    Sprite sprite = objects.GetSprite((Vector3Int)position);
                    if (sprite == null)
                        continue;
                    BuildSprite(overlay.Mesh, sprite, position, objects);
                    mask = sprite.texture;
                    alpha = objects.color.a * objects.GetColor((Vector3Int)position).a;
                }
                else if (config is TerrainTileConfigSO terrain && !config.IsMultiTile)
                {
                    if (_scratchInk == null)
                        _scratchInk = new Mesh { name = "Damage terrain scratch ink" };
                    // Reuse the actual rounded/wavy terrain boundary, including neighbours.
                    _terrainBuilder.Build(grid, new RectInt(position, Vector2Int.one),
                        overlay.Mesh, _scratchInk, _ => _tilemapRenderer.sharedMaterial);
                    Vector3[] vertices = overlay.Mesh.vertices;
                    Vector3 origin = _tilemap.CellToLocal((Vector3Int)position);
                    for (int i = 0; i < vertices.Length; i++)
                        vertices[i] += origin;
                    overlay.Mesh.vertices = vertices;
                    mask = terrain.FillTexture != null ? terrain.FillTexture : Texture2D.whiteTexture;
                    alpha = terrain.FillTint.a;
                }
                else
                    continue;

                // UV0 follows the object texture; UV1 repeats cracks per grid cell.
                _crackUVs.Clear();
                foreach (Vector3 vertex in overlay.Mesh.vertices)
                    _crackUVs.Add(_tilemap.LocalToCellInterpolated(vertex));
                overlay.Mesh.SetUVs(1, _crackUVs);
                overlay.Mesh.RecalculateBounds();
                Vector4 uv = UnityEngine.Sprites.DataUtility.GetOuterUV(crack);
                Vector4 padding = UnityEngine.Sprites.DataUtility.GetPadding(crack);
                Rect frame = crack.rect;
                _properties.Clear();
                _properties.SetTexture("_MainTex", mask);
                _properties.SetTexture("_CrackTex", crack.texture);
                _properties.SetVector("_CrackRect", new Vector4(uv.x, uv.y, uv.z - uv.x, uv.w - uv.y));
                // Outer UVs can be tightly cropped around the ink. Keep that crop
                // in its original sprite canvas instead of stretching it over a cell.
                _properties.SetVector("_CrackFrame", new Vector4(
                    padding.x / frame.width, padding.y / frame.height,
                    (frame.width - padding.x - padding.z) / frame.width,
                    (frame.height - padding.y - padding.w) / frame.height));
                Color tint = _tilemap.color;
                tint.a *= alpha;
                _properties.SetColor("_Color", tint);
                overlay.Renderer.SetPropertyBlock(_properties);
                overlay.Renderer.sharedMaterial = _tilemapRenderer.sharedMaterial;
                overlay.Renderer.sortingLayerID = _tilemapRenderer.sortingLayerID;
                overlay.Renderer.sortingOrder = _tilemapRenderer.sortingOrder;
                overlay.Root.layer = gameObject.layer;
                overlay.Root.SetActive(true);
                _visible.Add(position);
            }

            _remove.Clear();
            foreach (Vector2Int position in _overlays.Keys)
                if (!_visible.Contains(position))
                    _remove.Add(position);
            foreach (Vector2Int position in _remove)
            {
                Release(_overlays[position]);
                _overlays.Remove(position);
            }
        }

        private static Sprite GetCrackSprite(TileBase tile)
        {
            // These damage stages are static RuleTiles, not neighbour-dependent tiles.
            if (tile is RuleTile ruleTile)
                return ruleTile.m_DefaultSprite;
            if (tile is Tile simpleTile)
                return simpleTile.sprite;
            return null;
        }

        private Overlay GetOverlay(Vector2Int position)
        {
            if (_overlays.TryGetValue(position, out Overlay overlay))
                return overlay;
            var root = new GameObject($"Masked cracks [{position.x}, {position.y}]");
            root.transform.SetParent(transform, false);
            var mesh = new Mesh { name = root.name };
            mesh.MarkDynamic();
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            overlay = new Overlay { Root = root, Mesh = mesh, Renderer = root.AddComponent<MeshRenderer>() };
            overlay.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            overlay.Renderer.receiveShadows = false;
            _overlays.Add(position, overlay);
            return overlay;
        }

        private void BuildSprite(Mesh mesh, Sprite sprite, Vector2Int position, Tilemap objects)
        {
            Vector2[] spriteVertices = sprite.vertices;
            var vertices = new Vector3[spriteVertices.Length];
            Matrix4x4 tileTransform = objects.orientationMatrix * objects.GetTransformMatrix((Vector3Int)position);
            Vector3 origin = objects.GetCellCenterLocal((Vector3Int)position);
            Matrix4x4 toOverlay = transform.worldToLocalMatrix * objects.transform.localToWorldMatrix;
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = toOverlay.MultiplyPoint3x4(origin + tileTransform.MultiplyPoint3x4(spriteVertices[i]));
            ushort[] spriteTriangles = sprite.triangles;
            var triangles = new int[spriteTriangles.Length];
            for (int i = 0; i < triangles.Length; i++)
                triangles[i] = spriteTriangles[i];
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.uv = sprite.uv;
            mesh.triangles = triangles;
        }

        private void Release(Overlay overlay)
        {
            overlay.Root.SetActive(false);
            Destroy(overlay.Root);
            Destroy(overlay.Mesh);
        }

        public void Clear()
        {
            foreach (Overlay overlay in _overlays.Values)
                Release(overlay);
            _overlays.Clear();
            _visible.Clear();
            if (_tilemap != null)
                _tilemap.ClearAllTiles();
        }

        private void OnDisable()
        {
            Clear();
            _tilemapRenderer.enabled = _tilemapWasEnabled;
        }

        private void OnDestroy()
        {
            if (_scratchInk != null)
                Destroy(_scratchInk);
        }
    }
}
