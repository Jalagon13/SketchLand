using System;
using System.Collections.Generic;
using UnityEngine;

namespace SketchLand
{
    [CreateAssetMenu(fileName = "New AccessoryItem", menuName = "OceanGame/Item/AccessoryItemSO")]
    public class AccessoryItemSO : WearableItemSO
    {
        [Header("Stat Boosts")]
        [Tooltip("Configure any number of stat boosts granted while worn (e.g., +MoveSpeed, +Defense, +MaxHealth).")]
        [SerializeField] private List<AccessoryStatModifier> _statModifiers = new();
        public IReadOnlyList<AccessoryStatModifier> StatModifiers => _statModifiers;

        // Hook for custom accessory abilities (e.g., double jump, knockback immunity, water walking).
        // Called when the accessory is equipped.
        public virtual void OnEquip(ServerCharacter character)
        {
            // Consider putting the equip and dequip stuff on a pure c# interfaced class if i decide to have many types of one kidn of accessory maybe
        }

        // Cleanup hook for custom accessory abilities.
        // Called when the accessory is unequipped.
        public virtual void OnUnequip(ServerCharacter character)
        {
            
        }
    }
}
