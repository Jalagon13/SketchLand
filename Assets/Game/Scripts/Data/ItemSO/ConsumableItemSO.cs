using System.Collections.Generic;
using FMODUnity;
using UnityEngine;
using UnityEngine.Serialization;

namespace SketchLand
{
    [CreateAssetMenu(fileName = "Food", menuName = "OceanGame/Item/Food")]
    public class ConsumableItemSO : ItemSO
    {
        [field: Header("Food contributions (zero leaves a stat unchanged)")]
        [field: SerializeField, Min(0.01f)] public float Duration { get; private set; } = 200;
        [field: SerializeField, Min(0)] public int Health { get; private set; }
        [field: SerializeField, Min(0)] public float HealthRegen { get; private set; }
        [field: Header("ToolItemSO Bonuses (%)")]
        [field: FormerlySerializedAs("<BonusSwingSpeedPercentage>k__BackingField")]
        [field: SerializeField, Min(0)] public float BonusMiningSpeedPercentage { get; private set; }

        [field: Header("MeleeWeaponItemSO Bonuses (%)")]
        [field: FormerlySerializedAs("<BonusDamagePercentage>k__BackingField")]
        [field: SerializeField, Min(0)] public float BonusMeleeDamagePercentage { get; private set; }
        [field: SerializeField, Min(0)] public float BonusMeleeSpeedPercentage { get; private set; }
        [field: SerializeField, Min(0)] public float BonusMeleeRangePercentage { get; private set; }

        [field: Header("Audio")]
        [field: SerializeField] public EventReference ConsumeSFX { get; private set; }

        public override void OnPrimaryActionStarted(Player player)
        {
            if (player == null || player.Food == null) return;
            var slot = InventoryInputManager.Instance.GetActiveInvSlot();
            player.Food.Eat(slot, this);
        }

        public override string GetDescription()
        {
            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(Description)) lines.Add(Description);
            if (Health > 0) lines.Add($"+{Health} max HP");
            if (HealthRegen > 0) lines.Add($"+{HealthRegen:0.##} HP/sec");
            if (BonusMiningSpeedPercentage > 0)
            {
                lines.Add($"+{BonusMiningSpeedPercentage:0.##}% mining speed");
            }
            if (BonusMeleeDamagePercentage > 0 || BonusMeleeSpeedPercentage > 0 || BonusMeleeRangePercentage > 0)
            {
                if (BonusMeleeDamagePercentage > 0) lines.Add($"+{BonusMeleeDamagePercentage:0.##}% melee damage");
                if (BonusMeleeSpeedPercentage > 0) lines.Add($"+{BonusMeleeSpeedPercentage:0.##}% melee speed");
                if (BonusMeleeRangePercentage > 0) lines.Add($"+{BonusMeleeRangePercentage:0.##}% melee range");
            }
            int durationSeconds = Mathf.CeilToInt(Duration);
            lines.Add($"Duration: {durationSeconds / 60}:{durationSeconds % 60:00}");
            lines.Add("Use primary action to eat");
            return string.Join("<br>", lines);
        }
    }
}
