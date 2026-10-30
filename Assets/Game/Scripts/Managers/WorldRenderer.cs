using UnityEngine;

namespace SketchLand
{
    [DefaultExecutionOrder(10000)]
    public sealed class WorldRenderer : MonoBehaviour
    {
        [SerializeField] private TerrainChunkRenderer _terrain;

        [Header("Foreground")]
        [SerializeField] private SpriteTileRenderer _foregroundObjects;
        [SerializeField] private DamageOverlayRenderer _foregroundDamage;

        [Header("Background")]
        [SerializeField] private SpriteTileRenderer _backgroundObjects;
        [SerializeField] private DamageOverlayRenderer _backgroundDamage;

        private TileGrid _foreground;
        private TileGrid _background;
        private PlayerCamera _view;

        private bool _refreshRequested = true;

        private void OnEnable()
        {
            TileConfigSO.VisualsChanged += RequestRefresh;
            _refreshRequested = true;
        }

        private void OnDisable()
        {
            TileConfigSO.VisualsChanged -= RequestRefresh;

            Disconnect();
            ClearViews();

            _refreshRequested = true;
        }

        private void LateUpdate()
        {
            WorldManager world = WorldManager.Instance;
            PlayerCamera view = PlayerCamera.Instance;

            if (world == null || !world.IsWorldReady || view == null)
            {
                if (_foreground != null)
                {
                    Disconnect();
                    ClearViews();
                }

                return;
            }

            if (_foreground != world.FgGrid || _background != world.BgGrid || _view != view)
            {
                Disconnect();
                ClearViews();

                _foreground = world.FgGrid;
                _background = world.BgGrid;
                _view = view;

                _foreground.OnTileDataChanged += OnTileChanged;
                _background.OnTileDataChanged += OnTileChanged;

                _foreground.OnDamageChanged += RequestRefresh;
                _background.OnDamageChanged += RequestRefresh;

                _view.OnVisibleTileBoundsChanged += OnBoundsChanged;

                RequestRefresh();
            }

            // Terrain checks its visible chunks each frame.
            // Only dirty chunks rebuild their meshes.
            _terrain.Render(world);

            if (!_refreshRequested)
                return;

            _refreshRequested = false;

            RectInt bounds = _view.CurrentVisibleTileBounds;

            _backgroundObjects.Render(_background, bounds);
            _foregroundObjects.Render(_foreground, bounds);

            _backgroundDamage.Render(_background, bounds, _backgroundObjects.Tilemap);
            _foregroundDamage.Render(_foreground, bounds, _foregroundObjects.Tilemap);
        }

        public void RequestRefresh()
        {
            _refreshRequested = true;
        }

        private void OnTileChanged(Vector2Int position)
        {
            RequestRefresh();
        }

        private void OnBoundsChanged(RectInt oldBounds, RectInt newBounds)
        {
            RequestRefresh();
        }

        private void Disconnect()
        {
            if (_foreground != null)
            {
                _foreground.OnTileDataChanged -= OnTileChanged;
                _foreground.OnDamageChanged -= RequestRefresh;
            }

            if (_background != null)
            {
                _background.OnTileDataChanged -= OnTileChanged;
                _background.OnDamageChanged -= RequestRefresh;
            }

            if (_view != null)
            {
                _view.OnVisibleTileBoundsChanged -= OnBoundsChanged;
            }

            _foreground = null;
            _background = null;
            _view = null;
        }

        private void ClearViews()
        {
            if (_terrain != null)
                _terrain.Clear();

            if (_foregroundObjects != null)
                _foregroundObjects.Clear();

            if (_backgroundObjects != null)
                _backgroundObjects.Clear();

            if (_foregroundDamage != null)
                _foregroundDamage.Clear();

            if (_backgroundDamage != null)
                _backgroundDamage.Clear();
        }

        
    }
}
