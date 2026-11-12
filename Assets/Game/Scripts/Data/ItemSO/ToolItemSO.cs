using UnityEngine;

namespace SketchLand
{
    [CreateAssetMenu(fileName = "New ToolItemSO", menuName = "OceanGame/Item/ToolItemSO")]
    public class ToolItemSO : ItemSO
    {
        [field: Header("Tool Settings")]
        [field: SerializeField] public HarvestType HarvestType { get; private set; } = HarvestType.None;
        [field: SerializeField] public int MiningDamage { get; private set; } = 15; 

        [field: Header("Melee & Hitbox Settings")]
        [field: SerializeField] public int MeleeDamage { get; private set; } = 10;
        [field: SerializeField, Range(0, ServerCharacter.MaxKnockbackForce)] public int KnockbackForce { get; private set; } = 4;
        [field: SerializeField] public float HitboxOffset { get; private set; } = 1.0f; // Distance from pivot to blade center
        [field: SerializeField] public Vector2 HitboxSize { get; private set; } = new Vector2(1.2f, 1.2f);

        [field: Header("Swing Settings")]
        [field: SerializeField] public int SwingArcAngle { get; private set; } = 120;
        [field: SerializeField] public float SwingDuration { get; private set; } = 0.3f;
        [field: SerializeField, Min(0f)] public float SwingCooldown { get; private set; } = 0.25f;
    }
}
