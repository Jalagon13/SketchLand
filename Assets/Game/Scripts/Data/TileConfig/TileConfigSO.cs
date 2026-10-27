using System.Collections.Generic;
using FMODUnity;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SketchLand
{
    [CreateAssetMenu(fileName = "New TileConfigSO", menuName = "OceanGame/TileConfig/TileConfigSO")]
    public class TileConfigSO : ScriptableObject
    {
        [field: Header("Base Tile Data")]
        [field: SerializeField] public HarvestType RequiredHarvestType { get; private set; } = HarvestType.None;
        [field: SerializeField] public int MaxHP { get; private set; } = 50;
        [field: SerializeField, Range(0, 1f)] public float LightLevel { get; private set; } = 0;
        [field: SerializeField] public bool Indestructible { get; private set; } = false;
        [field: SerializeField] public bool IsSolid { get; private set; } = true;
        [field: SerializeField] public Vector2Int Size { get; private set; } = new(1, 1);
        [field: SerializeField] public TileBase DrawTile { get; private set; }

        [Header("Illustrated Rendering — 1x1 terrain")]
        public bool UseIllustratedRendering = false;

        public Texture2D FillTexture;
        public Color FillTint = Color.white;

        [Tooltip("World units covered by one complete texture repeat.")]
        public Vector2 TextureWorldSize = new(4f, 4f);

        public Vector2 TextureOffset = Vector2.zero;

        public Color OutlineColor = new(0.06f, 0.05f, 0.04f, 1f);

        [Range(0.005f, 0.12f)]
        public float OutlineWidth = 0.04f;

        [Range(0f, 0.08f)]
        public float OutlineWobble = 0.025f;

        [Range(0.1f, 3f)]
        public float OutlineWavelength = 0.65f;

        [Range(0f, 0.2f)]
        public float CornerSoftness = 0.1f;

        public static event System.Action VisualsChanged; // Lets the renderer refresh when an asset is edited in the Inspector.

        private void OnValidate()
        {
            VisualsChanged?.Invoke();
        }

        [field: Header("Game Feel")]
        [field: SerializeField] public EventReference TileHitSFX { get; private set; }
        [field: SerializeField] public EventReference TileDestroyedSFX { get; private set; }

        [field: Header("Tile Behavior")]
        [field: SerializeReference]
        [field: SerializeField] public InteractBehavior InteractBehavior { get; private set; }
        [field: SerializeField] public List<LootDropEntry> LootDrops = new();

        public bool IsMultiTile => Size != new Vector2Int(1, 1);

        public ushort GetId()
        {
            return GameDataRegistry.Instance.GetTileIdFromTileDataSO(this);
        }

        public virtual TileBase GetStateInterpretedTileForRendering(byte state)
        {
            return DrawTile;
        }

        public virtual void OnTileDestroyed(TileGrid grid, int x, int y, TileData tileData, bool refreshCurrentBounds = false)
        {
            Vector2 dropPos;

            if (IsMultiTile)
            {
                int rootX = x - tileData.OffsetX;
                int rootY = y - tileData.OffsetY;

                for (int ox = 0; ox < Size.x; ox++)
                {
                    for (int oy = 0; oy < Size.y; oy++)
                    {
                        int targetX = rootX + ox;
                        int targetY = rootY + oy;

                        if (grid.IsInBounds(targetX, targetY))
                        {
                            grid.SetTileDataDirect(targetX, targetY, TileData.Air);
                            grid.ClearDamage(targetX, targetY);
                        }
                    }
                }

                dropPos = new Vector2(rootX + (Size.x * 0.5f), rootY + (Size.y * 0.5f));
            }
            else
            {
                grid.SetTileDataDirect(x, y, TileData.Air);
                grid.ClearDamage(x, y);

                dropPos = new Vector2(x + 0.5f, y + 0.5f);
            }
            
            // Roll and drop each entry in the loot table
            DropLoot(dropPos);
        }

        public void DropLoot(Vector2 position)
        {
            if (LootDrops == null) return;

            for (int i = 0; i < LootDrops.Count; i++)
            {
                LootDrops[i].TryDrop(position);
            }
        }
    }
}