using UnityEngine;
using UnityEngine.Tilemaps;

namespace SketchLand
{
    [CreateAssetMenu(fileName = "New SpriteTileConfigSO", menuName = "OceanGame/TileConfig/SpriteTileConfigSO")]
    public class SpriteTileConfigSO : TileConfigSO
    {
        [field: Header("Sprite Rendering")]
        [field: SerializeField] public TileBase DrawTile { get; private set; }

        public override TileBase GetStateInterpretedTileForRendering(byte state)
        {
            return DrawTile;
        }
    }
}