using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace SketchLand
{
    public class LightManager : MonoBehaviour
    {
        public static LightManager Instance { get; private set; }

        [Header("Light Settings")]
        [SerializeField] private bool _showLight = true;
        [SerializeField] private Material _multiplyMaterial;
        [SerializeField, Min(1f)] private float _fullBrightness;
        [SerializeField] private int _extraLightmapPadding;
        [SerializeField, Min(1)] private int _lightCellsPerTile = 1;
        [Tooltip("Pushes light values away from the midpoint toward dark or full brightness. Set to 1 for no adjustment.")]
        [SerializeField, Range(1f, 3f)] private float _lightContrast = 1.2f;

        [Header("Light Attenuation")]
        [Tooltip("Distance in game tiles that light travels before it starts dimming.")]
        [SerializeField, Min(0)] private int _tileAmountBeforeAttenuationBegins = 2;
        [FormerlySerializedAs("_solidFgTransmission")]
        [SerializeField, Min(0f)] private float _solidForegroundAttenuation = 2f;
        [FormerlySerializedAs("_bgTransmission")]
        [SerializeField, Min(0f)] private float _backgroundOnlyAttenuation = 0.5f;

        [Header("Foreground Smoothing")]
        [SerializeField] private bool _enableForegroundBlur = true;
        [Tooltip("Blur radius in world tiles. Only solid foreground samples are blended.")]
        [SerializeField, Range(0.25f, 2f)] private float _foregroundBlurRadius = 0.5f;
        [SerializeField, Range(1, 4)] private int _foregroundBlurPasses = 1;

        [Header("Background Settings")]
        [SerializeField] private RawImage _backgroundOverlay;
        [SerializeField] private GameObject _backgroundLightOverlay;
        [SerializeField] private FilterMode _backgroundLightmapFilterMode;

        [Header("Foreground Settings")]
        [SerializeField] private RawImage _foregroundOverlay;
        [SerializeField] private GameObject _foregroundLightOverlay;
        [SerializeField] private FilterMode _foregroundLightmapFilterMode;
        
        private RectInt _lmBounds;
        private Material _lightmapMaterial;
        private static readonly int LightmapTexelSizeId = Shader.PropertyToID("_LightmapTexelSize");
        private Texture2D _foregroundLightmapTexture;
        private Texture2D _backgroundLightmapTexture;
        private Color32[] _foregroundColorBuffer;
        private Color32[] _backgroundColorBuffer;
        private float[,] _lightGrid;
        private float[,] _foregroundLightGrid;
        private float[,] _blurGrid;
        private bool[,] _foregroundMask;
        private int[,] _distanceGrid;

        private Queue<Vector2Int> _lightQueue;
        private static readonly Vector2Int[] _directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        
        public float FullBrightness => _fullBrightness;

        private void Awake()
        {
            Instance = this;
            // Keep per-lightmap uniforms off the shared material asset.
            _lightmapMaterial = new Material(_multiplyMaterial);

            _foregroundLightOverlay.SetActive(_showLight);
            _backgroundLightOverlay.SetActive(_showLight);

            _lightQueue = new Queue<Vector2Int>(100 * 100 * _lightCellsPerTile * _lightCellsPerTile);
        }

        private void Start()
        {
            PlayerCamera.Instance.OnVisibleTileBoundsChanged += OnBoundsChanged;
        }

        private void OnDestroy()
        {
            if (PlayerCamera.Instance != null) PlayerCamera.Instance.OnVisibleTileBoundsChanged -= OnBoundsChanged;

            // Prevent memory leaks when changing scenes
            if (_foregroundLightmapTexture != null) Destroy(_foregroundLightmapTexture);
            if (_backgroundLightmapTexture != null) Destroy(_backgroundLightmapTexture);
            if (_lightmapMaterial != null) Destroy(_lightmapMaterial);
        }

        private void OnBoundsChanged(RectInt oldBounds, RectInt newBounds)
        {
            var world = WorldManager.Instance;

            if (world == null || !world.IsWorldReady) return;

            // Initializations
            int xMin = newBounds.xMin - _extraLightmapPadding;
            int yMin = newBounds.yMin - _extraLightmapPadding;
            int xMax = newBounds.xMax + _extraLightmapPadding;
            int yMax = newBounds.yMax + _extraLightmapPadding;

            _lmBounds = new(xMin, yMin, xMax - xMin, yMax - yMin);

            int tileWidth = _lmBounds.width;
            int tileHeight = _lmBounds.height;
            int lightmapWidth = tileWidth * _lightCellsPerTile;
            int lightmapHeight = tileHeight * _lightCellsPerTile;

            // CanvasRenderer can retain stale automatic _MainTex_TexelSize values after
            // Texture2D.Reinitialize. Keep the mask's sampling grid explicitly in sync.
            _lightmapMaterial.SetVector(LightmapTexelSizeId,
                new Vector4(1f / lightmapWidth, 1f / lightmapHeight, lightmapWidth, lightmapHeight));

            if (_foregroundLightmapTexture == null) // Initialize if new
            {
                _foregroundLightmapTexture = new(lightmapWidth, lightmapHeight, TextureFormat.RGBA32, false)
                {
                    filterMode = _foregroundLightmapFilterMode,
                    wrapMode = TextureWrapMode.Clamp
                };
            }
            else if (_foregroundLightmapTexture.width != lightmapWidth || _foregroundLightmapTexture.height != lightmapHeight) // dynamically set it when camera changes dimensions
            {
                _foregroundLightmapTexture.Reinitialize(lightmapWidth, lightmapHeight);
                _foregroundLightmapTexture.filterMode = _foregroundLightmapFilterMode;
                _foregroundLightmapTexture.wrapMode = TextureWrapMode.Clamp;
            }

            if (_backgroundLightmapTexture == null) // Initialize if new
            {
                _backgroundLightmapTexture = new(lightmapWidth, lightmapHeight, TextureFormat.RGBA32, false)
                {
                    filterMode = _backgroundLightmapFilterMode,
                    wrapMode = TextureWrapMode.Clamp
                };
            }
            else if (_backgroundLightmapTexture.width != lightmapWidth || _backgroundLightmapTexture.height != lightmapHeight) // dynamically set it when camera changes dimensions
            {
                _backgroundLightmapTexture.Reinitialize(lightmapWidth, lightmapHeight);
                _backgroundLightmapTexture.filterMode = _backgroundLightmapFilterMode;
                _backgroundLightmapTexture.wrapMode = TextureWrapMode.Clamp;
            }

            // Manage light grid and color buffer sizes safely
            if (_lightGrid == null || _lightGrid.GetLength(0) != lightmapWidth || _lightGrid.GetLength(1) != lightmapHeight)
            {
                _lightGrid = new float[lightmapWidth, lightmapHeight];
                _foregroundLightGrid = new float[lightmapWidth, lightmapHeight];
                _blurGrid = new float[lightmapWidth, lightmapHeight];
                _foregroundMask = new bool[lightmapWidth, lightmapHeight];
                _distanceGrid = new int[lightmapWidth, lightmapHeight];
                _foregroundColorBuffer = new Color32[lightmapWidth * lightmapHeight];
                _backgroundColorBuffer = new Color32[lightmapWidth * lightmapHeight];
            }
            else
            {
                Array.Clear(_lightGrid, 0, _lightGrid.Length); // Clear existing frames rather than instantiating clean arrays
            }

            _lightQueue.Clear();
            for (int x = 0; x < lightmapWidth; x++)
            {
                for (int y = 0; y < lightmapHeight; y++)
                {
                    _distanceGrid[x, y] = int.MaxValue;
                }
            }

            // Seed light sources
            for (int localX = 0; localX < tileWidth; localX++)
            {
                for (int localY = 0; localY < tileHeight; localY++)
                {
                    int worldPosX = xMin + localX;
                    int worldPosY = yMin + localY;

                    if (!world.FgGrid.IsInBounds(worldPosX, worldPosY)) continue;

                    var fgTd = world.FgGrid.GetTileData(worldPosX, worldPosY);
                    var bgTd = world.BgGrid.GetTileData(worldPosX, worldPosY);

                    if (world.GetZoneAtY(worldPosY).AllowsNaturalDaylight &&
                        (!fgTd.HasTile || !fgTd.IsSolid) && !bgTd.HasTile)
                    {
                        SeedTile(localX, localY, _fullBrightness);
                    }
                    else if (fgTd.LightLevel > 0 || bgTd.LightLevel > 0)
                    {
                        float sourceStrength = Mathf.Max(fgTd.LightLevel, bgTd.LightLevel) / 100f;
                        SeedTile(localX, localY, _fullBrightness * Mathf.Clamp01(sourceStrength));
                    }
                }
            }

            PropagateLight(world, lightmapWidth, lightmapHeight);
            PrepareForegroundLight(world, lightmapWidth, lightmapHeight);

            float fullBrightnessInv = 1f / _fullBrightness; // Multiplies are faster than division loops

            // Foreground alpha is a binary tile mask. RGB is premultiplied by that mask
            // so the shader can interpolate light without blending in empty cells.
            for (int localY = 0; localY < lightmapHeight; localY++)
            {
                for (int localX = 0; localX < lightmapWidth; localX++)
                {
                    byte backgroundGrey = ToGrayscale(_lightGrid[localX, localY] * fullBrightnessInv);
                    int bufferIndex = localY * lightmapWidth + localX;
                    byte foregroundGrey = ToGrayscale(_foregroundLightGrid[localX, localY] * fullBrightnessInv);
                    _foregroundColorBuffer[bufferIndex] = _foregroundMask[localX, localY]
                        ? new Color32(foregroundGrey, foregroundGrey, foregroundGrey, 255)
                        : new Color32(0, 0, 0, 0);
                    _backgroundColorBuffer[bufferIndex] = new Color32(backgroundGrey, backgroundGrey, backgroundGrey, 255);
                }
            }

            // Apply foreground lighting with a sharp occupancy mask, independent of filtering.
            _foregroundLightmapTexture.filterMode = _foregroundLightmapFilterMode;
            _foregroundLightmapTexture.SetPixels32(_foregroundColorBuffer);
            _foregroundLightmapTexture.Apply();
            _foregroundOverlay.texture = _foregroundLightmapTexture;
            _foregroundOverlay.material = _lightmapMaterial;

            // Apply bilinear background lighting only where foreground tiles do not cover it.
            _backgroundLightmapTexture.SetPixels32(_backgroundColorBuffer);
            _backgroundLightmapTexture.Apply();
            _backgroundOverlay.texture = _backgroundLightmapTexture;
            _backgroundOverlay.material = _lightmapMaterial;

            // Update Overlay positioning
            Vector2 center = new Vector2(_lmBounds.xMin + _lmBounds.xMax, _lmBounds.yMin + _lmBounds.yMax) * 0.5f;
            Vector2 size = new(tileWidth, tileHeight);

            _foregroundOverlay.rectTransform.position = center;
            _foregroundOverlay.rectTransform.sizeDelta = size;
            _foregroundOverlay.rectTransform.localScale = Vector3.one;

            _backgroundOverlay.rectTransform.position = center;
            _backgroundOverlay.rectTransform.sizeDelta = size;
            _backgroundOverlay.rectTransform.localScale = Vector3.one;
        }

        private byte ToGrayscale(float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            if (_lightContrast > 1f)
            {
                float bright = Mathf.Pow(fraction, _lightContrast);
                float dark = Mathf.Pow(1f - fraction, _lightContrast);
                fraction = bright / (bright + dark);
            }
            return (byte)(fraction * 255f);
        }

        private void PrepareForegroundLight(WorldManager world, int width, int height)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var tile = world.FgGrid.GetTileData(_lmBounds.xMin + x / _lightCellsPerTile,
                        _lmBounds.yMin + y / _lightCellsPerTile);
                    bool solid = tile.HasTile && tile.IsSolid;
                    _foregroundMask[x, y] = solid;
                    _foregroundLightGrid[x, y] = solid ? _lightGrid[x, y] : 0f;
                }
            }

            if (!_enableForegroundBlur) return;
            int radius = Mathf.Max(1, Mathf.RoundToInt(_foregroundBlurRadius * _lightCellsPerTile));
            for (int pass = 0; pass < _foregroundBlurPasses; pass++)
            {
                BlurForegroundAxis(_foregroundLightGrid, _blurGrid, width, height, radius, true);
                BlurForegroundAxis(_blurGrid, _foregroundLightGrid, width, height, radius, false);
            }
        }

        private void BlurForegroundAxis(float[,] source, float[,] destination, int width, int height,
            int radius, bool horizontal)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    destination[x, y] = 0f;
                    if (!_foregroundMask[x, y]) continue;
                    float sum = source[x, y];
                    int count = 1;
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        for (int step = 1; step <= radius; step++)
                        {
                            int nx = x + (horizontal ? sign * step : 0);
                            int ny = y + (horizontal ? 0 : sign * step);
                            if (nx < 0 || nx >= width || ny < 0 || ny >= height || !_foregroundMask[nx, ny]) break;
                            sum += source[nx, ny];
                            count++;
                        }
                    }
                    // Renormalize at edges; empty sky must neither brighten nor darken the ground.
                    destination[x, y] = sum / count;
                }
            }
        }

        private void SeedTile(int localTileX, int localTileY, float lightValue)
        {
            int lightX = localTileX * _lightCellsPerTile;
            int lightY = localTileY * _lightCellsPerTile;

            for (int x = 0; x < _lightCellsPerTile; x++)
            {
                for (int y = 0; y < _lightCellsPerTile; y++)
                {
                    Vector2Int lightPosition = new(lightX + x, lightY + y);
                    _lightGrid[lightPosition.x, lightPosition.y] = lightValue;
                    _distanceGrid[lightPosition.x, lightPosition.y] = 0;
                    _lightQueue.Enqueue(lightPosition);
                }
            }
        }

        private void PropagateLight(WorldManager world, int width, int height)
        {
            int unattenuatedDistance = _tileAmountBeforeAttenuationBegins * _lightCellsPerTile;
            float cellSize = 1f / _lightCellsPerTile;

            while (_lightQueue.Count > 0)
            {
                Vector2Int current = _lightQueue.Dequeue();
                float currentLight = _lightGrid[current.x, current.y];
                int nextDistance = _distanceGrid[current.x, current.y] + 1;

                foreach (Vector2Int direction in _directions)
                {
                    int x = current.x + direction.x;
                    int y = current.y + direction.y;
                    if (x < 0 || x >= width || y < 0 || y >= height) continue;

                    int worldX = _lmBounds.xMin + x / _lightCellsPerTile;
                    int worldY = _lmBounds.yMin + y / _lightCellsPerTile;
                    if (!world.FgGrid.IsInBounds(worldX, worldY)) continue;

                    float attenuation = 0f;
                    if (nextDistance > unattenuatedDistance)
                    {
                        var foreground = world.FgGrid.GetTileData(worldX, worldY);
                        // SketchLand's non-solid foreground tiles transmit like open/background cells.
                        attenuation = (foreground.HasTile && foreground.IsSolid
                            ? _solidForegroundAttenuation
                            : _backgroundOnlyAttenuation) * cellSize;
                    }

                    float nextLight = currentLight - attenuation;
                    if (nextLight <= 0f) continue;

                    // Equal light arriving by a shorter path retains more of the undimmed range.
                    if (nextLight > _lightGrid[x, y] ||
                        (nextLight == _lightGrid[x, y] && nextDistance < _distanceGrid[x, y]))
                    {
                        _lightGrid[x, y] = nextLight;
                        _distanceGrid[x, y] = nextDistance;
                        _lightQueue.Enqueue(new Vector2Int(x, y));
                    }
                }
            }
        }

    }
}
