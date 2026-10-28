using System;
using System.Collections.Generic;
using FMODUnity;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SketchLand
{
    [CreateAssetMenu(fileName = "New DoorTileConfigSO", menuName = "OceanGame/TileConfig/DoorTileConfigSO")]
    public class DoorTileConfigSO : SpriteTileConfigSO
    {
        [field: Header("Door Tile Settings")]
        [field: SerializeField] public Sprite ClosedDoorSprite { get; private set; }
        [field: SerializeField] public Sprite OpenDoorSprite { get; private set; }

        public override TileBase GetStateInterpretedTileForRendering(byte state)
        {
            AutoTile drawTile = (AutoTile)Instantiate(DrawTile);
        
            switch(state)
            {
                case 0:
                    drawTile.m_DefaultSprite = ClosedDoorSprite;
                    return drawTile;
                case 1:
                    drawTile.m_DefaultSprite = OpenDoorSprite;
                    return drawTile;
                default:
                    return DrawTile;
            }
        }
    }
}
