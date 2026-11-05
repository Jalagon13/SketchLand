// Assets/_Game/Scripts/Data/ItemSO/ArmorItemSO.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SketchLand
{
    [CreateAssetMenu(fileName = "New ArmorItem", menuName = "OceanGame/Item/ArmorItemSO")]
    public class ArmorItemSO : ItemSO
    {
        [field: Header("Armor Configuration")]
        [field: SerializeField] public ArmorType ArmorPieceType { get; private set; }
        [field: SerializeField] public int Defense { get; private set; } = 2;

        [field: Header("Wearable Visual")]
        [field: SerializeField] public WearableVisualSO WearableVisual { get; private set; }

        [Header("Future Expansion: Secondary Stats")]
        [SerializeField] private List<ArmorStatModifier> _secondaryStats = new();
        public IReadOnlyList<ArmorStatModifier> SecondaryStats => _secondaryStats;

        // Hook for future custom armor effects (e.g., jump height, lighting, thorns).
        public virtual void OnEquip(ServerCharacter character)
        {
            // Base behavior: apply secondary stats (if any)
            foreach (var bonus in _secondaryStats)
            {
                var stat = character.Stats.GetStat(bonus.TargetStat);
                stat.AddModifier(new StatModifier(bonus.ModifierType, bonus.Value));
            }
        }

        public virtual void OnUnequip(ServerCharacter character)
        {
            // Base behavior: remove secondary stats (if any)
            foreach (var bonus in _secondaryStats)
            {
                var stat = character.Stats.GetStat(bonus.TargetStat);
                stat.RemoveModifier(new StatModifier(bonus.ModifierType, bonus.Value));
            }
        }
    }
}
