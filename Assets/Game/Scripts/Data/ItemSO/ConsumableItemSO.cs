using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

namespace SketchLand
{
    [CreateAssetMenu(fileName = "Food", menuName = "OceanGame/Item/Food")]
    public class ConsumableItemSO : ItemSO
    {
        [field: Header("Food contributions (zero leaves a stat unchanged)")]
        [field: SerializeField, Min(0.01f)] public float Duration { get; private set; } = 200;
        [field: SerializeField, Min(0)] public int Health { get; private set; }
        [field: SerializeField, Min(0)] public float HealthRegen { get; private set; }
        [field: SerializeField, Range(0, 100)] public float BonusDamagePercentage { get; private set; }
        [field: SerializeField, Range(0, 100)] public float BonusSwingSpeedPercentage { get; private set; }
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
            if (BonusDamagePercentage > 0) lines.Add($"+{BonusDamagePercentage:0.##}% damage");
            if (BonusSwingSpeedPercentage > 0) lines.Add($"+{BonusSwingSpeedPercentage:0.##}% swing speed");
            lines.Add($"Duration: {Duration:0.##} seconds");
            lines.Add("Use primary action to eat");
            return string.Join("<br>", lines);
        }
    }
}
