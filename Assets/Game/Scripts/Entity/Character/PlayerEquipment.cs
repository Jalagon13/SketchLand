using System;
using System.Collections.Generic;
using UnityEngine;

namespace SketchLand
{
    public class PlayerEquipment : MonoBehaviour
    {
        public event Action OnEquipmentChanged;

        // Armor
        public InventorySlot EquippedHelmet { get; private set; } = new();
        public InventorySlot EquippedChestplate { get; private set; } = new();
        public InventorySlot EquippedPants { get; private set; } = new();
        private readonly Dictionary<ArmorType, StatModifier> _appliedDefenseModifiers = new(); // Cache applied defense modifiers so they can be removed cleanly

        // Accessories
        public List<InventorySlot> EquippedAccessories { get; private set; } = new(); // Dynamic list of accessory slots that automatically expands
        private readonly Dictionary<int, List<(StatType stat, StatModifier mod)>> _appliedAccessoryModifiers = new(); // Tracks active stat modifiers per slot index so they can be cleanly removed

        public InventorySlot GetAccessorySlot(int index)
        {
            EnsureAccessorySlotCount(index + 1);
            return EquippedAccessories[index];
        }

        public void EnsureAccessorySlotCount(int count)
        {
            while (EquippedAccessories.Count < count)
            {
                EquippedAccessories.Add(new InventorySlot());
            }
        }
        
        public AccessoryItemSO GetEquippedAccessorySO(int index)
        {
            var slot = GetAccessorySlot(index);
            if (slot == null || slot.IsEmpty) return null;
            return GameDataRegistry.Instance.GetItemSOFromItemId(slot.ItemId) as AccessoryItemSO;
        }
        
        public bool IsAccessoryEquipped(ushort itemId, int ignoreIndex = -1)
        {
            for (int i = 0; i < EquippedAccessories.Count; i++)
            {
                if (i == ignoreIndex) continue;
                if (!EquippedAccessories[i].IsEmpty && EquippedAccessories[i].ItemId == itemId)
                {
                    return true;
                }
            }
            return false;
        }
        
        public void EquipAccessory(int index, ushort itemId)
        {
            EnsureAccessorySlotCount(index + 1);
            UnequipAccessory(index, refresh: false);
            
            var slot = EquippedAccessories[index];
            slot.AssignItem(itemId, 1);
            var accessorySO = GetEquippedAccessorySO(index);
            
            if (accessorySO != null && Player.Instance.Character != null)
            {
                // Apply configured stat boosts to CharacterStats
                var appliedList = new List<(StatType, StatModifier)>();
                
                foreach (var bonus in accessorySO.StatModifiers)
                {
                    var stat = Player.Instance.Character.Stats.GetStat(bonus.TargetStat);
                    var mod = new StatModifier(bonus.ModifierType, bonus.Value);
                    stat.AddModifier(mod);
                    appliedList.Add((bonus.TargetStat, mod));
                }
                
                _appliedAccessoryModifiers[index] = appliedList;
                
                // Trigger special ability lifecycle hook
                accessorySO.OnEquip(Player.Instance.Character);
            }
            RefreshEquipment();
        }
        
        public void UnequipAccessory(int index, bool refresh = true)
        {
            if (index < 0 || index >= EquippedAccessories.Count) return;
            
            var slot = EquippedAccessories[index];
            if (slot.IsEmpty) return;
            
            var accessorySO = GetEquippedAccessorySO(index);
            
            if (accessorySO != null && Player.Instance.Character != null)
            {
                // Remove tracked stat boosts
                if (_appliedAccessoryModifiers.TryGetValue(index, out var appliedList))
                {
                    foreach (var (targetStat, mod) in appliedList)
                    {
                        Player.Instance.Character.Stats.GetStat(targetStat).RemoveModifier(mod);
                    }
                    _appliedAccessoryModifiers.Remove(index);
                }
                
                // Trigger special ability cleanup hook
                accessorySO.OnUnequip(Player.Instance.Character);
            }
            
            slot.Clear();
            
            if (refresh)
            {
                RefreshEquipment();
            }
        }

        public ArmorItemSO GetEquippedArmorSO(ArmorType type)
        {
            var slot = GetArmorSlot(type);
            if (slot == null || slot.IsEmpty) return null;
            return GameDataRegistry.Instance.GetItemSOFromItemId(slot.ItemId) as ArmorItemSO;
        }

        public InventorySlot GetArmorSlot(ArmorType type) => type switch
        {
            ArmorType.Helmet => EquippedHelmet,
            ArmorType.Chestplate => EquippedChestplate,
            ArmorType.Pants => EquippedPants,
            _ => null
        };

        public void EquipArmor(ArmorType type, ushort itemId)
        {
            UnequipArmor(type, refresh: false); // If something was already equipped in this slot, unequip it first

            var slot = GetArmorSlot(type);
            slot.AssignItem(itemId, 1);
            var armorSO = GetEquippedArmorSO(type);
            
            if (armorSO != null && Player.Instance.Character != null)
            {
                // Apply Defense modifier to CharacterStats
                var defMod = new StatModifier(StatModifierType.Flat, armorSO.Defense);
                _appliedDefenseModifiers[type] = defMod;
                Player.Instance.Character.Stats.Defense.AddModifier(defMod);
                
                // Trigger future/unique effects lifecycle
                armorSO.OnEquip(Player.Instance.Character);
            }
            
            CheckSetBonus();
            RefreshEquipment();
        }
        public void UnequipArmor(ArmorType type, bool refresh = true)
        {
            var slot = GetArmorSlot(type);
            if (slot == null || slot.IsEmpty) return;
            var armorSO = GetEquippedArmorSO(type);
            
            if (armorSO != null && Player.Instance.Character != null)
            {
                // Remove defense modifier
                if (_appliedDefenseModifiers.TryGetValue(type, out var defMod))
                {
                    Player.Instance.Character.Stats.Defense.RemoveModifier(defMod);
                    _appliedDefenseModifiers.Remove(type);
                }
                
                // Cleanup future/unique effects
                armorSO.OnUnequip(Player.Instance.Character);
            }
            
            slot.Clear();
            CheckSetBonus();
            
            if (refresh)
            {
                RefreshEquipment();
            }
        }
        
        private void CheckSetBonus()
        {
            // Foundation for Terraria set bonuses:
            // Check if Helm, Chest, and Pants are equipped and belong to the same set.
        }

        public void RefreshEquipment()
        {
            OnEquipmentChanged?.Invoke();
            InventoryCursorManager.Instance.RefreshCursorSlot();
        }
    }
}
