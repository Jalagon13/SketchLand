using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

namespace SketchLand
{
    public abstract class TileConfigSO : ScriptableObject
    {
        [field: Header("Base Tile Data")]
        [field: SerializeField] public HarvestType RequiredHarvestType { get; private set; } = HarvestType.None;
        [field: SerializeField] public int MaxHP { get; private set; } = 50;
        [field: SerializeField, Range(0, 1f)] public float LightLevel { get; private set; } = 0;
        [field: SerializeField] public bool Indestructible { get; private set; } = false;
        [field: SerializeField] public bool IsSolid { get; private set; } = true;
        [field: SerializeField] public Vector2Int Size { get; private set; } = new(1, 1);

        public static event System.Action VisualsChanged; // Lets the renderer refresh when an asset is edited in the Inspector.

        protected virtual void OnValidate()
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