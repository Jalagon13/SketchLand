using UnityEngine;

namespace SketchLand
{
    [CreateAssetMenu(fileName = "New WearableItem", menuName = "OceanGame/Item/WearableItemSO")]
    public class WearableItemSO : ItemSO
    {
        [field: Header("Wearable Visuals")]
        [field: SerializeField] public WearableVisualData Visuals { get; private set; } = new();
    }
}
