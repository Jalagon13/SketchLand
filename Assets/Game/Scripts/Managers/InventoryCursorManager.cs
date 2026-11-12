using System;
using UnityEngine;

namespace SketchLand
{
    public class InventoryCursorManager : MonoBehaviour
    {
        public static InventoryCursorManager Instance { get; private set; }
        
        public event Action OnCursorSlotChanged;
        
        [SerializeField] private float _throwItemForce = 15;

        public InventorySlot CursorSlot { get; private set; } = new(); 

        private void Awake()
        {
            Instance = this;
        }
        
        public void RefreshCursorSlot()
        {
            OnCursorSlotChanged?.Invoke();
        }

        public void AssignCursorSlot(ushort itemId, int amount)
        {
            CursorSlot.AssignItem(itemId, amount);
            InventoryManager.Instance.RefreshInventory();
        }

        public void ThrowCursorSlotItem()
        {
            if (CursorSlot.IsEmpty) return;

            Vector2 aimDirection = WorldManager.MouseWorldPosition - (Vector2)Player.Instance.Character.transform.position;
            aimDirection.Normalize();
            aimDirection *= _throwItemForce;
            
            GameManager.Instance.SpawnItem(GameDataRegistry.Instance.GetItemSOFromItemId(CursorSlot.ItemId), CursorSlot.Amount, Player.Instance.Character.transform.position, aimDirection);
            CursorSlot.Clear();

            InventoryManager.Instance.RefreshInventory();
        }

        // Armor (Helmet, Chest, Pants)
        public void HandleArmorSlotLeftClick(ArmorType armorType)
        {
            var equip = Player.Instance.Equipment;
            
            HandleGenericEquipmentSlotClick(
                equip.GetArmorSlot(armorType),
                item => item is ArmorItemSO armor && armor.ArmorPieceType == armorType,
                itemId => equip.EquipArmor(armorType, itemId),
                () => equip.UnequipArmor(armorType)
            );
        }

        public void HandleArmorSlotRightClick(ArmorType armorType)
        {
            // Right click in Terraria or slot can quickly unequip to cursor or inventory
            HandleArmorSlotLeftClick(armorType);
        }

        // Accessories
        public void HandleAccessorySlotLeftClick(int slotIndex)
        {
            var equip = Player.Instance.Equipment;
            
            HandleGenericEquipmentSlotClick(
                equip.GetAccessorySlot(slotIndex),
                item => item is AccessoryItemSO && !equip.IsAccessoryEquipped(item.GetId(), ignoreIndex: slotIndex),
                itemId => equip.EquipAccessory(slotIndex, itemId),
                () => equip.UnequipAccessory(slotIndex)
            );
        }

        public void HandleAccessorySlotRightClick(int slotIndex)
        {
            HandleAccessorySlotLeftClick(slotIndex);
        }

        // Generic slot handlers that work for Player, Chest, or any container
        public void HandleSlotLeftClick(InventorySlot slot, Action onSlotModified = null)
        {
            if (slot == null) return;
            if (CursorSlot.IsEmpty && slot.IsEmpty) return;
            
            if (CursorSlot.IsEmpty)
            {
                CursorSlot = slot.Clone();
                slot.Clear();
            }
            else if (slot.IsEmpty)
            {
                slot.AssignItem(CursorSlot.ItemId, CursorSlot.Amount);
                CursorSlot.Clear();
            }
            else if (CanStacksMerge(slot, CursorSlot))
            {
                int maxStack = GetMaxStackSize(GameDataRegistry.Instance.GetItemSOFromItemId(slot.ItemId));
                MoveAmount(CursorSlot, slot, maxStack);
            }
            else
            {
                // Swap slot contents safely
                ushort tempId = slot.ItemId;
                int tempAmount = slot.Amount;
                slot.AssignItem(CursorSlot.ItemId, CursorSlot.Amount);
                CursorSlot.AssignItem(tempId, tempAmount);
            }
            
            onSlotModified?.Invoke();
            AudioManager.Instance.PlayOneShot(FMODEvents.Instance.SlotClickedSFX, default);
            InventoryManager.Instance.RefreshInventory();
        }
        
        public void HandleSlotRightClick(InventorySlot slot, Action onSlotModified = null)
        {
            if (slot == null) return;
            if (CursorSlot.IsEmpty && slot.IsEmpty) return;
            
            if (CursorSlot.IsEmpty)
            {
                int cursorAmount = Mathf.CeilToInt(slot.Amount * 0.5f);
                CursorSlot.AssignItem(slot.ItemId, cursorAmount);
                slot.RemoveFromCurrentAmount(cursorAmount);
            }
            else if (slot.IsEmpty)
            {
                slot.AssignItem(CursorSlot.ItemId, 1);
                CursorSlot.RemoveFromCurrentAmount(1);
            }
            else if (CanStacksMerge(slot, CursorSlot))
            {
                slot.AddToCurrentAmount(1);
                CursorSlot.RemoveFromCurrentAmount(1);
            }
            
            onSlotModified?.Invoke();
            AudioManager.Instance.PlayOneShot(FMODEvents.Instance.SlotClickedSFX, default);
            InventoryManager.Instance.RefreshInventory();
        }

        private void HandleGenericEquipmentSlotClick(InventorySlot equipSlot, Func<ItemSO, bool> canEquipValidator, Action<ushort> onEquip, Action onUnequip)
        {
            if (Player.Instance == null || Player.Instance.Equipment == null || equipSlot == null) return;
            if (CursorSlot.IsEmpty && equipSlot.IsEmpty) return;

            if (CursorSlot.IsEmpty)
            {
                // Unequip to cursor
                ushort unequippedItemId = equipSlot.ItemId;
                CursorSlot.AssignItem(unequippedItemId, 1);
                onUnequip?.Invoke();

                AudioManager.Instance.PlayOneShot(FMODEvents.Instance.SlotClickedSFX, default);
            }
            else if (equipSlot.IsEmpty)
            {
                // Equip from cursor
                ItemSO cursorItem = GameDataRegistry.Instance.GetItemSOFromItemId(CursorSlot.ItemId);
                if (canEquipValidator != null && !canEquipValidator(cursorItem)) return;

                ushort itemIdToEquip = CursorSlot.ItemId;
                CursorSlot.RemoveFromCurrentAmount(1);
                onEquip?.Invoke(itemIdToEquip);

                AudioManager.Instance.PlayOneShot(FMODEvents.Instance.SlotClickedSFX, default);
            }
            else
            {
                // Swap cursor item with equipped item
                ItemSO cursorItem = GameDataRegistry.Instance.GetItemSOFromItemId(CursorSlot.ItemId);
                if (canEquipValidator != null && !canEquipValidator(cursorItem)) return;

                if (CursorSlot.Amount == 1)
                {
                    ushort oldEquippedId = equipSlot.ItemId;
                    ushort newEquippedId = CursorSlot.ItemId;

                    CursorSlot.AssignItem(oldEquippedId, 1);
                    onEquip?.Invoke(newEquippedId);

                    AudioManager.Instance.PlayOneShot(FMODEvents.Instance.SlotClickedSFX, default);
                }
            }
        }

        private int MoveAmount(InventorySlot source, InventorySlot target, int maxTargetAmount, int requestedAmount = int.MaxValue)
        {
            if (source == null || target == null || source.IsEmpty || target.IsEmpty)
            {
                return 0;
            }

            int amountToMove = Mathf.Min(requestedAmount, source.Amount);
            amountToMove = Mathf.Min(amountToMove, maxTargetAmount - target.Amount);
            
            if (amountToMove <= 0)
            {
                return 0;
            }

            target.AddToCurrentAmount(amountToMove);
            source.RemoveFromCurrentAmount(amountToMove);
            return amountToMove;
        }

        private bool CanStacksMerge(InventorySlot target, InventorySlot source)
        {
            return target != null && source != null && !target.IsEmpty && !source.IsEmpty && target.ItemId == source.ItemId && target.Amount < GetMaxStackSize(GameDataRegistry.Instance.GetItemSOFromItemId(target.ItemId));
        }

        private int GetMaxStackSize(ItemSO item)
        {
            if (item == null)
            {
                return 1;
            }

            return item.IsStackable ? InventoryManager.Instance.MaxStackSize : 1;
        }

        
    }
}
