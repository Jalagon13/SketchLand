using UnityEngine;

namespace SketchLand
{
    [CreateAssetMenu(fileName = "New Wearable Visual", menuName = "OceanGame/WearableVisualSO")]
    public class WearableVisualSO : ScriptableObject
    {
        [field: SerializeField] public Sprite HeadPiece { get; private set; }
    }
}
