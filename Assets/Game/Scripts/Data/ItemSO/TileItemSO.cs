using UnityEngine;
using UnityEngine.Tilemaps;

namespace SketchLand
{
    [CreateAssetMenu(fileName = "New TileitemSO", menuName = "OceanGame/Item/TileItemSO")]
    public class TileItemSO : ItemSO
    {
        [field: Header("Tile Item Settings")]
        [field: SerializeField] public TileConfigSO PlaceTileDataSO { get; private set; }

        public override void OnPrimaryActionStarted(Player player)
        {
            PlacingManager.Instance.StartPlacing(this);
        }

        public override void OnPrimaryActionHeld(Player player)
        {
            PlacingManager.Instance.TickPlacing(this);
        }

        public override void OnPrimaryActionRelease(Player player)
        {
            PlacingManager.Instance.StopPlacing();
        }
        
    }
}