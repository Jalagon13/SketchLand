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
        public const byte Closed = 0;
        public const byte OpenRight = 1;
        public const byte OpenLeft = 2;

        [field: Header("Door Tile Settings")]
        [field: SerializeField] public Sprite ClosedDoorSprite { get; private set; }
        [field: SerializeField] public Sprite OpenDoorSprite { get; private set; }

        public override TileBase GetStateInterpretedTileForRendering(byte state)
        {
            if (state > OpenLeft) return DrawTile;

            var source = (AutoTile)DrawTile;
            var drawTile = CreateInstance<Tile>();
            drawTile.sprite = state == Closed ? ClosedDoorSprite : OpenDoorSprite;
            drawTile.gameObject = source.m_DefaultGameObject;
            drawTile.colliderType = source.m_DefaultColliderType;
            drawTile.flags = TileFlags.LockAll;
            // Mirror around the sprite's pivot at the tile anchor, keeping the doorway fixed.
            drawTile.transform = Matrix4x4.Scale(new Vector3(state == OpenLeft ? -1f : 1f, 1f, 1f));
            return drawTile;
        }
    }
}
