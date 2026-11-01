using UnityEngine;
using UnityEngine.Tilemaps;

namespace SketchLand
{
    [CreateAssetMenu(fileName = "New SpriteTileConfigSO", menuName = "OceanGame/TileConfig/SpriteTileConfigSO")]
    public class SpriteTileConfigSO : TileConfigSO
    {
        [field: Header("Sprite Rendering")]
        [field: SerializeField] public TileBase DrawTile { get; private set; }

        public virtual TileBase GetTileForRendering(TileGrid grid, TileGrid background, Vector2Int position, byte state)
        {
            return GetStateInterpretedTileForRendering(state);
        }

        public virtual TileBase GetStateInterpretedTileForRendering(byte state)
        {
            return DrawTile;
        }
    }
}