using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering;

namespace SketchLand
{
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class TerrainChunkRenderer : MonoBehaviour
    {
        public static TerrainChunkRenderer Instance { get; private set; }

        public const int ChunkSize = 16;

        [Header("Rendering")]
        public bool UseIllustratedRendering = true;

        [SerializeField] private Camera _camera;
        [SerializeField] private Shader _terrainShader;

        [Header("Camera coverage")]
        [SerializeField, Min(1)] private int _paddingCells = 2;
        [SerializeField, Range(0, 3)] private int _prefetchChunks = 1;

        private sealed class Chunk
        {
            public GameObject Root;
            public Mesh Mesh;
            public Mesh InkMesh;
            public MeshRenderer InkRenderer;
            public MeshRenderer Renderer;
            public bool Dirty = true;
        }

        private sealed class Layer
        {
            public TileGrid Grid;
            public GameObject Root;
            public SortingGroup Sorting;
            public Action<Vector2Int> Changed;
            public readonly Dictionary<Vector2Int, Chunk> Chunks = new();
        }

        private readonly TerrainMeshBuilder _builder = new();

        private readonly Dictionary<TerrainTileConfigSO, Material>
        _fillMaterials = new();

        private readonly List<Vector2Int> _removeKeys = new();
        private MaterialPropertyBlock _properties;

        private Material _inkMaterial;
        private Layer _foreground;
        private Layer _background;

        private bool _refreshRequested = true;
        private bool _lastUse;

        private void Awake()
        {
            Instance = this;

            _properties = new MaterialPropertyBlock();

            if (_camera == null)
                _camera = Camera.main;
        }

        private void OnEnable()
        {
            TileConfigSO.VisualsChanged += RequestRefresh;
            _refreshRequested = true;
        }

        private void OnValidate()
        {
            _refreshRequested = true;
        }

        [ContextMenu("Rebuild Visible Terrain")]
        public void RequestRefresh()
        {
            _refreshRequested = true;
        }

        public static bool ShouldRender(TileData tile)
        {
            var renderer = Instance;

            return renderer != null &&
                   renderer.isActiveAndEnabled &&
                   renderer.UseIllustratedRendering &&
                   renderer._terrainShader != null &&
                   renderer._camera != null &&
                   renderer._camera.orthographic &&
                   TerrainMeshBuilder.GetVisualConfig(tile) != null;
        }

        private void LateUpdate()
        {
            var world = WorldManager.Instance;

            if (world == null || !world.IsWorldReady)
                return;

            if (_refreshRequested || _lastUse != UseIllustratedRendering)
            {
                _refreshRequested = false;
                _lastUse = UseIllustratedRendering;

                ReleaseLayers();
                ReleaseMaterials();
                RefreshTilemaps();
            }

            if (!UseIllustratedRendering || _terrainShader == null || _camera == null || !_camera.orthographic)
            {
                return;
            }

            BindLayer(ref _foreground, world.FgGrid);
            BindLayer(ref _background, world.BgGrid);

            RectInt visibleChunks = GetVisibleChunkRange(world.FgGrid.Width, world.FgGrid.Height);

            RenderLayer(_background, visibleChunks, "Background");
            RenderLayer(_foreground, visibleChunks, "Foreground");
        }

        private void BindLayer(ref Layer layer, TileGrid grid)
        {
            if (layer != null && layer.Grid == grid)
                return;

            ReleaseLayer(layer);

            var root = new GameObject(grid == WorldManager.Instance.FgGrid ? "Illustrated foreground" : "Illustrated background");
            root.transform.SetParent(transform, true);
            layer = new Layer
            {
                Grid = grid,
                Root = root,
                Sorting = root.AddComponent<SortingGroup>()
            };

            Layer boundLayer = layer;
            layer.Changed = position => MarkDirty(boundLayer, position);

            grid.OnTileDataChanged += layer.Changed;
        }

        private RectInt GetVisibleChunkRange(int width, int height)
        {
            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;

            // All four corners also cover a camera rotated around Z.
            for (int y = 0; y <= 1; y++)
            {
                for (int x = 0; x <= 1; x++)
                {
                    Vector3 point = _camera.ViewportToWorldPoint(
                        new Vector3(x, y, _camera.nearClipPlane));

                    minX = Mathf.Min(minX, point.x);
                    minY = Mathf.Min(minY, point.y);
                    maxX = Mathf.Max(maxX, point.x);
                    maxY = Mathf.Max(maxY, point.y);
                }
            }

            int firstX = Mathf.FloorToInt((minX - _paddingCells) / ChunkSize) - _prefetchChunks;

            int firstY = Mathf.FloorToInt((minY - _paddingCells) / ChunkSize) - _prefetchChunks;

            int lastX = Mathf.FloorToInt((maxX + _paddingCells) / ChunkSize) + _prefetchChunks;

            int lastY = Mathf.FloorToInt((maxY + _paddingCells) / ChunkSize) + _prefetchChunks;

            firstX = Mathf.Max(0, firstX);
            firstY = Mathf.Max(0, firstY);

            lastX = Mathf.Min((width - 1) / ChunkSize, lastX);
            lastY = Mathf.Min((height - 1) / ChunkSize, lastY);

            return new RectInt(firstX, firstY, Mathf.Max(0, lastX - firstX + 1), Mathf.Max(0, lastY - firstY + 1));
        }

        private void RenderLayer(Layer layer, RectInt visibleChunks, string layerName)
        {
            _removeKeys.Clear();

            foreach (var entry in layer.Chunks)
            {
                if (!visibleChunks.Contains(entry.Key))
                    _removeKeys.Add(entry.Key);
            }

            foreach (Vector2Int key in _removeKeys)
            {
                DestroyChunk(layer.Chunks[key]);
                layer.Chunks.Remove(key);
            }

            Tilemap map = layer.Grid.Tilemap;
            TilemapRenderer source = map.GetComponent<TilemapRenderer>();
            
            // All fills in this world layer draw before any outlines, including
            // outlines which extend across a rendering chunk boundary.
            layer.Sorting.sortingLayerID = source.sortingLayerID;
            layer.Sorting.sortingOrder = source.sortingOrder;

            _properties.Clear();
            _properties.SetColor("_LayerTint", map.color);

            for (int cy = visibleChunks.yMin; cy < visibleChunks.yMax; cy++)
            {
                for (int cx = visibleChunks.xMin; cx < visibleChunks.xMax; cx++)
                {
                    Vector2Int key = new Vector2Int(cx, cy);

                    int startX = cx * ChunkSize;
                    int startY = cy * ChunkSize;

                    RectInt region = new RectInt(startX, startY, Mathf.Min(ChunkSize, layer.Grid.Width - startX), Mathf.Min(ChunkSize, layer.Grid.Height - startY));

                    if (!layer.Chunks.TryGetValue(key, out Chunk chunk))
                    {
                        chunk = CreateChunk(layer, layerName, key);
                        layer.Chunks.Add(key, chunk);
                    }

                    chunk.Root.layer = map.gameObject.layer;
                    chunk.InkRenderer.gameObject.layer = map.gameObject.layer;
                    chunk.Root.transform.position = new Vector3(startX, startY, map.transform.position.z);
                    chunk.Renderer.sortingLayerID = source.sortingLayerID;
                    chunk.Renderer.sortingOrder = 0;
                    chunk.InkRenderer.sortingLayerID = source.sortingLayerID;
                    chunk.InkRenderer.sortingOrder = 1;
                    chunk.InkRenderer.SetPropertyBlock(_properties);
                    chunk.Renderer.SetPropertyBlock(_properties);

                    if (!chunk.Dirty)
                        continue;

                    chunk.Renderer.sharedMaterials = _builder.Build(layer.Grid, region, chunk.Mesh, chunk.InkMesh, GetFillMaterial);
                    chunk.Renderer.enabled = chunk.Mesh.subMeshCount > 0;
                    chunk.InkRenderer.enabled = chunk.InkMesh.vertexCount > 0;
                    chunk.Dirty = false;
                }
            }
        }

        private Chunk CreateChunk(Layer layer, string layerName, Vector2Int key)
        {
            var root = new GameObject($"{layerName} terrain [{key.x}, {key.y}]");

            root.transform.SetParent(layer.Root.transform, true);

            var mesh = new Mesh
            {
                name = $"{layerName} terrain mesh [{key.x}, {key.y}]"
            };

            mesh.MarkDynamic();

            root.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = root.AddComponent<MeshRenderer>();

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var inkRoot = new GameObject("Outline");
            inkRoot.transform.SetParent(root.transform, false);
            
            var inkMesh = new Mesh { name = mesh.name + " ink" };
            inkMesh.MarkDynamic();
            inkRoot.AddComponent<MeshFilter>().sharedMesh = inkMesh;
            
            var inkRenderer = inkRoot.AddComponent<MeshRenderer>();
            inkRenderer.sharedMaterial = GetInkMaterial();
            inkRenderer.shadowCastingMode = ShadowCastingMode.Off;
            inkRenderer.receiveShadows = false;

            return new Chunk
            {
                Root = root,
                Mesh = mesh,
                Renderer = renderer,
                InkMesh = inkMesh,
                InkRenderer = inkRenderer
            };
        }

        private Material GetFillMaterial(TerrainTileConfigSO config)
        {
            if (_fillMaterials.TryGetValue(config, out Material material))
                return material;

            material = new Material(_terrainShader)
            {
                name = config.name + " illustrated fill"
            };

            material.SetTexture("_MainTex", config.FillTexture != null ? config.FillTexture : Texture2D.whiteTexture);

            _fillMaterials.Add(config, material);

            return material;
        }

        private Material GetInkMaterial()
        {
            if (_inkMaterial != null)
                return _inkMaterial;

            _inkMaterial = new Material(_terrainShader)
            {
                name = "Terrain outline ink"
            };

            _inkMaterial.SetTexture("_MainTex", Texture2D.whiteTexture);

            return _inkMaterial;
        }

        private void MarkDirty(Layer layer, Vector2Int position)
        {
            // Corner rules inspect diagonals too, hence the 3x3 area.
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    Vector2Int key = new Vector2Int(Mathf.FloorToInt((position.x + dx) / (float)ChunkSize), Mathf.FloorToInt((position.y + dy) / (float)ChunkSize));

                    if (layer.Chunks.TryGetValue(key, out Chunk chunk))
                        chunk.Dirty = true;
                }
            }
        }

        private static void RefreshTilemaps()
        {
            var world = WorldManager.Instance;

            if (world == null ||
                !world.IsWorldReady ||
                GameDataRegistry.Instance == null ||
                WorldRenderer.Instance == null ||
                PlayerCamera.Instance == null ||
                world.FgGrid.Tilemap == null ||
                world.BgGrid.Tilemap == null)
            {
                return;
            }

            PlayerCamera.Instance.InvokeCurrentBoundsRefresh();
        }

        private void OnDisable()
        {
            TileConfigSO.VisualsChanged -= RequestRefresh;

            ReleaseLayers();
            RefreshTilemaps();
        }

        private void OnDestroy()
        {
            ReleaseLayers();
            ReleaseMaterials();

            if (Instance == this)
                Instance = null;
        }

        private void ReleaseLayers()
        {
            ReleaseLayer(_foreground);
            ReleaseLayer(_background);

            _foreground = null;
            _background = null;
        }

        private void ReleaseLayer(Layer layer)
        {
            if (layer == null)
                return;

            layer.Grid.OnTileDataChanged -= layer.Changed;

            foreach (Chunk chunk in layer.Chunks.Values)
                DestroyChunk(chunk);

            layer.Chunks.Clear();
            if (layer.Root != null)
            {
                layer.Root.SetActive(false);
                Destroy(layer.Root);
            }
        }

        private void DestroyChunk(Chunk chunk)
        {
            if (chunk.Root != null)
            {
                chunk.Root.SetActive(false);
                Destroy(chunk.Root);
            }

            if (chunk.Mesh != null)
                Destroy(chunk.Mesh);
            if (chunk.InkMesh != null)
                Destroy(chunk.InkMesh);
        }

        private void ReleaseMaterials()
        {
            foreach (Material material in _fillMaterials.Values)
            {
                if (material != null)
                    Destroy(material);
            }

            _fillMaterials.Clear();

            if (_inkMaterial != null)
                Destroy(_inkMaterial);

            _inkMaterial = null;
        }
    }
}