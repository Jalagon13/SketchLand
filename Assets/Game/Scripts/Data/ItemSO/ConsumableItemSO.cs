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
        [field: SerializeField, Min(0)] public float Stamina { get; private set; }
        [field: SerializeField, Min(0)] public float StaminaRegen { get; private set; }
        [field: SerializeField] public EventReference ConsumeSFX { get; private set; }

        public override string GetDescription()
        {
            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(Description)) lines.Add(Description);
            if (Health > 0) lines.Add($"+{Health} max HP");
            if (HealthRegen > 0) lines.Add($"+{HealthRegen:0.##} HP/sec");
            if (Stamina > 0) lines.Add($"+{Stamina:0.##} max stamina");
            if (StaminaRegen > 0) lines.Add($"+{StaminaRegen:0.##} stamina/sec");
            lines.Add($"Hold right click to eat · {Duration:0.##} seconds");
            return string.Join("<br>", lines);
        }
    }
}
