using UnityEngine;

namespace SketchLand
{
    [CreateAssetMenu(fileName = "New Arrow", menuName = "OceanGame/Item/ArrowItemSO")]
    public class ArrowItemSO : ItemSO
    {
        [field: Header("Arrow Settings")]
        [field: SerializeField] public ProjectileSO Projectile { get; private set; }
    }
}
