using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SketchLand
{
    [DisallowMultipleComponent]
    public sealed class TerrainChunkRenderer : MonoBehaviour
    {
        public const int ChunkSize = 16;

        [Header("Rendering")]
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

        [Serializable]
        private sealed class LayerSettings
        {
            public string SortingLayerName = "Default";
            public int SortingOrder;

            public Color Tint = Color.white;

            [Range(0, 31)]
            public int GameObjectLayer;

            public float Z;
        }

        [SerializeField]
        private LayerSettings _foregroundSettings = new();

        [SerializeField]
        private LayerSettings _backgroundSettings = new()
        {
            SortingOrder = -30
        };

        private void Awake()
        {
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

        private void OnDisable()
        {
            TileConfigSO.VisualsChanged -= RequestRefresh;

            Clear();
        }

        private void OnDestroy()
        {
            Clear();
        }

        [ContextMenu("Rebuild Visible Terrain")]
        public void RequestRefresh()
        {
            _refreshRequested = true;
        }

        public void Render(WorldManager world)
        {
            if (world == null || !world.IsWorldReady)
                return;

            if (_refreshRequested)
            {
                _refreshRequested = false;

                ReleaseLayers();
                ReleaseMaterials();
            }

            if (_terrainShader == null || _camera == null || !_camera.orthographic)
            {
                return;
            }

            BindLayer(ref _foreground, world.FgGrid);
            BindLayer(ref _background, world.BgGrid);

            RectInt visibleChunks = GetVisibleChunkRange(world.FgGrid.Width,world.FgGrid.Height);

            RenderLayer(_background, visibleChunks, "Background", _backgroundSettings);
            RenderLayer(_foreground, visibleChunks, "Foreground", _foregroundSettings);
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

        private void RenderLayer(Layer layer, RectInt visibleChunks, string layerName, LayerSettings settings)
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

            int sortingLayerId = SortingLayer.NameToID(settings.SortingLayerName);

            layer.Root.layer = settings.GameObjectLayer;

            layer.Sorting.sortingLayerID = sortingLayerId;
            layer.Sorting.sortingOrder = settings.SortingOrder;

            _properties.Clear();
            _properties.SetColor("_LayerTint", settings.Tint);

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

                    chunk.Root.layer = settings.GameObjectLayer;
                    chunk.InkRenderer.gameObject.layer = settings.GameObjectLayer;

                    chunk.Root.transform.position = new Vector3(startX, startY, settings.Z);

                    chunk.Renderer.sortingLayerID = sortingLayerId;
                    chunk.Renderer.sortingOrder = 0;

                    chunk.InkRenderer.sortingLayerID = sortingLayerId;
                    chunk.InkRenderer.sortingOrder = 1;

                    chunk.Renderer.SetPropertyBlock(_properties);
                    chunk.InkRenderer.SetPropertyBlock(_properties);

                    if (!chunk.Dirty)
                        continue;

                    chunk.Renderer.sharedMaterials = _builder.Build(layer.Grid, region, chunk.Mesh, chunk.InkMesh, GetFillMaterial, layer == _background);
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
            // Background coverage extends half a cell; its outline also samples neighbours.
            int radius = layer == _background ? 2 : 1;
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    Vector2Int key = new Vector2Int(Mathf.FloorToInt((position.x + dx) / (float)ChunkSize), Mathf.FloorToInt((position.y + dy) / (float)ChunkSize));

                    if (layer.Chunks.TryGetValue(key, out Chunk chunk))
                        chunk.Dirty = true;
                }
            }
        }

        public void Clear()
        {
            ReleaseLayers();
            ReleaseMaterials();

            _refreshRequested = true;
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
