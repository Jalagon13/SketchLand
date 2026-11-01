using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

namespace SketchLand
{
    [CreateAssetMenu(fileName = "TC_Torch", menuName = "OceanGame/TileConfig/TorchTileConfigSO")]
    public sealed class TorchTileConfigSO : SpriteTileConfigSO
    {
        public enum Attachment { None, Floor, LeftWall, RightWall, Background }

        [field: Header("Torch Sprites")]
        [field: SerializeField] public Sprite FloorSprite { get; private set; }
        [field: SerializeField, FormerlySerializedAs("<LeftWallSprite>k__BackingField")]
        [field: Tooltip("Draw attached to a solid tile LEFT of the torch. Mirrored horizontally for a tile on the right.")]
        public Sprite WallSprite { get; private set; }
        [field: SerializeField, Tooltip("Attached to the background tile in the torch's own cell.")]
        public Sprite BackgroundSprite { get; private set; }

        // Reused for every torch of this config; never allocate per cell or render refresh.
        private readonly Tile[] _drawTiles = new Tile[4];

        public Attachment GetAttachment(TileGrid foreground, TileGrid background, Vector2Int position)
        {
            if (!foreground.IsInBounds(position.x, position.y)) return Attachment.None;

            // Stable priority when several supports exist. A ceiling alone never supports a torch.
            if (HasSolidTile(foreground, position + Vector2Int.down)) return Attachment.Floor;
            if (HasSolidTile(foreground, position + Vector2Int.left)) return Attachment.LeftWall;
            if (HasSolidTile(foreground, position + Vector2Int.right)) return Attachment.RightWall;
            if (background != null && background.GetTileData(position.x, position.y).HasTile)
                return Attachment.Background;

            return Attachment.None;
        }

        public override bool CanPlace(TileGrid foreground, TileGrid background, Vector2Int position, WorldManager.LayerType layer)
        {
            return layer == WorldManager.LayerType.Foreground && !IsMultiTile &&
                   foreground.IsInBounds(position.x, position.y) &&
                   !foreground.GetTileData(position.x, position.y).HasTile &&
                   GetAttachment(foreground, background, position) != Attachment.None;
        }

        public override void OnSupportChanged(TileGrid foreground, TileGrid background, Vector2Int position)
        {
            if (GetAttachment(foreground, background, position) == Attachment.None)
                foreground.DestroyTile(position.x, position.y);
        }

        public override TileBase GetTileForRendering(TileGrid grid, TileGrid background, Vector2Int position, byte state)
        {
            Attachment attachment = GetAttachment(grid, background, position);
            Sprite sprite = attachment switch
            {
                Attachment.Floor => FloorSprite,
                Attachment.LeftWall or Attachment.RightWall => WallSprite,
                Attachment.Background => BackgroundSprite,
                _ => null
            };

            // Keep the existing art visible until directional sprites have been authored.
            if (sprite == null) return DrawTile;

            int index = (int)attachment - 1;
            Tile tile = _drawTiles[index];
            if (tile == null)
            {
                tile = CreateInstance<Tile>();
                tile.hideFlags = HideFlags.HideAndDontSave;
                tile.colliderType = Tile.ColliderType.None;
                _drawTiles[index] = tile;
            }

            tile.sprite = sprite;
            tile.transform = attachment == Attachment.RightWall
                ? Matrix4x4.Scale(new Vector3(-1f, 1f, 1f))
                : Matrix4x4.identity;
            return tile;
        }

        private static bool HasSolidTile(TileGrid grid, Vector2Int position)
        {
            TileData tile = grid.GetTileData(position.x, position.y);
            return tile.HasTile && tile.IsSolid;
        }

        private void OnDisable()
        {
            for (int i = 0; i < _drawTiles.Length; i++)
            {
                if (_drawTiles[i] == null) continue;
                if (Application.isPlaying) Destroy(_drawTiles[i]);
                else DestroyImmediate(_drawTiles[i]);
                _drawTiles[i] = null;
            }
        }
    }
}
