using UnityEngine;
using UnityEngine.Tilemaps;

namespace SketchLand
{
    [CreateAssetMenu(fileName = "New TerrainTileConfigSO", menuName = "OceanGame/TileConfig/TerrainTileConfigSO")]
    public class TerrainTileConfigSO : TileConfigSO
    {
        [Header("Terrain Fill")]
        public Texture2D FillTexture;
        public Color FillTint = Color.white;

        [Tooltip("World units covered by one complete texture repeat.")]
        public Vector2 TextureWorldSize = new(4f, 4f);

        public Vector2 TextureOffset = Vector2.zero;

        [Header("Terrain Outline")]
        public Color OutlineColor = new(0.06f, 0.05f, 0.04f, 1f);

        [Range(0.005f, 0.12f)]
        public float OutlineWidth = 0.04f;

        [Range(0f, 0.08f)]
        public float OutlineWobble = 0.025f;

        [Range(0.1f, 3f)]
        public float OutlineWavelength = 0.65f;

        [Range(0f, 0.2f)]
        public float CornerSoftness = 0.1f;

        [field: Header("Legacy Tilemap Fallback")]
        [field: Tooltip("Used when the terrain mesh renderer is disabled or unavailable.")]
        [field: SerializeField]
        public TileBase DrawTile { get; private set; }

        public override TileBase GetStateInterpretedTileForRendering(byte state)
        {
            return DrawTile;
        }
    }
}