using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SketchLand
{
    public class InventoryInputManager : MonoBehaviour
    {
        public static InventoryInputManager Instance { get; private set; }
        
        public event Action<bool> OnInventoryOpenChanged;
        public event Action<List<RecipeSO>, int, int> OnCraftTableInteract;
        public event Action<int> OnActiveHotbarIndexChanged;
        
        public int ActiveHotbarIndex { get; private set; }
        public bool IsInventoryOpen { get; private set; }
        
        public InventorySlot ActiveSlot { get; private set; }

        private bool _cursorItemUseHeld;
        private bool _startAfterSwing;
        private ItemSO _activeItem;
        private InventorySlot _activeSlot;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            GameInput.Instance.OnScrollWheel += OnScrollWheel;
            GameInput.Instance.OnSelectSlot += OnSelectSlot;
            GameInput.Instance.OnThrowItemPressed += OnThrowItemPressed;
            GameInput.Instance.OnToggleInventory += OnToggleInventory;
            GameInput.Instance.OnPrimaryActionPressed += OnPrimaryActionPressed;
            GameInput.Instance.OnSecondaryActionPressed += OnSecondaryActionPressed;
        }

        private void OnDestroy()
        {
            GameInput.Instance.OnScrollWheel -= OnScrollWheel;
            GameInput.Instance.OnSelectSlot -= OnSelectSlot;
            GameInput.Instance.OnThrowItemPressed -= OnThrowItemPressed;
            GameInput.Instance.OnToggleInventory -= OnToggleInventory;
            GameInput.Instance.OnPrimaryActionPressed -= OnPrimaryActionPressed;
            GameInput.Instance.OnSecondaryActionPressed -= OnSecondaryActionPressed;
        }

        private void Update()
        {
            ActiveSlot = GetActiveInvSlot();
            var player = Player.Instance;
            if (!CanAct())
            {
                CancelItemUse();
                return;
            }

            if (_activeItem != null && !HasActiveItem())
                CancelItemUse();

            if (WorldManager.Instance.MouseOverUI) return;
            if (!GameInput.Instance.PrimaryActionHeld) return;
            if (player.ArmHandler.IsSwinging) return;
            if (_startAfterSwing)
            {
                _startAfterSwing = false;
                StartSelectedItem(ActiveSlot);
                return;
            }
            if (_activeItem == null && !_cursorItemUseHeld && !ActiveSlot.IsEmpty &&
                ActiveSlot.GetItemSO().CanStartWhilePrimaryHeld)
            {
                StartSelectedItem(ActiveSlot);
                return;
            }
            _activeItem?.OnPrimaryActionHeld(player);
        }

        private bool CanAct()
        {
            return WorldManager.Instance.IsWorldReady && Player.Instance.Character != null
                && Player.Instance.Character.Health.CurrentLifeState.Value != LifeState.Dead;
        }

        private bool HasActiveItem()
        {
            var slot = GetActiveInvSlot();
            return slot == _activeSlot && !slot.IsEmpty && slot.GetItemSO() == _activeItem;
        }

        private void OnThrowItemPressed()
        {
            if (!CanAct()) return;

            var slot = GetActiveInvSlot();
            if (slot.IsEmpty) return;

            CancelItemUse();
            InventoryCursorManager.Instance.ThrowItem(slot);
        }

        private void OnSecondaryActionPressed(InputAction.CallbackContext context)
        {
            if (!context.started || !CanAct() || WorldManager.Instance.MouseOverUI) return;
            var slot = GetActiveInvSlot();
            if (!slot.IsEmpty && slot.GetItemSO().OnSecondaryActionStarted(Player.Instance)) return;
            if (InventoryCursorManager.Instance.CursorSlot.IsEmpty)
                Player.Instance.TryInteractWithObject();
        }

        private void CancelItemUse()
        {
            _startAfterSwing = false;
            var item = _activeItem;
            _activeItem = null;
            _activeSlot = null;
            item?.OnPrimaryActionCancelled(Player.Instance);
        }

        private void OnDisable()
        {
            if (Player.Instance != null)
                CancelItemUse();
        }

        private void ReleaseItemUse()
        {
            _startAfterSwing = false;
            var item = _activeItem;
            _activeItem = null;
            _activeSlot = null;
            item?.OnPrimaryActionRelease(Player.Instance);
        }

        private void OnPrimaryActionPressed(InputAction.CallbackContext context)
        {
            if (context.canceled)
            {
                _cursorItemUseHeld = false;
                if (_activeItem != null && CanAct() && !WorldManager.Instance.MouseOverUI && HasActiveItem())
                    ReleaseItemUse();
                else
                    CancelItemUse();
                return;
            }

            if (!context.started || !CanAct() || WorldManager.Instance.MouseOverUI) return;

            if (Player.Instance.ArmHandler.IsSwinging)
            {
                _startAfterSwing = true;
                return;
            }
            StartSelectedItem(GetActiveInvSlot());
        }

        private void StartSelectedItem(InventorySlot slot)
        {
            if (slot.IsEmpty) return;
            _activeSlot = slot;
            _activeItem = slot.GetItemSO();
            // Consuming or placing the last cursor item must not pass this press to the hotbar.
            _cursorItemUseHeld = slot == InventoryCursorManager.Instance.CursorSlot;
            _activeItem.OnPrimaryActionStarted(Player.Instance);
        }

        public InventorySlot GetActiveInvSlot()
        {
            var cursorSlot = InventoryCursorManager.Instance.CursorSlot;
            
            if(cursorSlot != null && !cursorSlot.IsEmpty)
            {
                return cursorSlot;
            }
            
            return InventoryManager.Instance.PlayerInventory[ActiveHotbarIndex];
        }

        public void OnInteractWithCraftingTable(List<RecipeSO> recipes, int x, int y)
        {
            if (Player.Instance.Character.Health.CurrentLifeState.Value == LifeState.Dead) return;

            if (!IsInventoryOpen)
            {
                OnToggleInventory();
            }
            else
            {
                AudioManager.Instance.PlayOneShot(FMODEvents.Instance.InventoryOpenSFX, default);
            }
        
            OnCraftTableInteract?.Invoke(recipes, x, y);
        }

        public void OnToggleInventory()
        {
            if (Player.Instance.Character.Health.CurrentLifeState.Value == LifeState.Dead) return;

            IsInventoryOpen = !IsInventoryOpen;

            OnInventoryOpenChanged?.Invoke(IsInventoryOpen);
        }
        
        // Used for chest manager
        public void OnToggleInventory(bool showDefaultCraftingMenu = true)
        {
            if (Player.Instance.Character.Health.CurrentLifeState.Value == LifeState.Dead) return;

            IsInventoryOpen = !IsInventoryOpen;

            OnInventoryOpenChanged?.Invoke(IsInventoryOpen);
        }

        private void OnScrollWheel(InputAction.CallbackContext context)
        {
            if (Player.Instance.Character.Health.CurrentLifeState.Value == LifeState.Dead) return;

            Vector2 scrollDelta = context.ReadValue<Vector2>();
            // Down/right advance; up/left go back. Shift may redirect the wheel to X.
            float scroll = scrollDelta.y != 0f ? scrollDelta.y : -scrollDelta.x;
            int itemCount = InventoryManager.Instance.HotbarSize;
            if (itemCount == 0)
            {
                return;
            }

            int selectedSlotIndex = ActiveHotbarIndex;

            if (scroll > 0f)
            {
                int upcomingIndex = selectedSlotIndex - 1;
                selectedSlotIndex = upcomingIndex < 0 ? itemCount - 1 : selectedSlotIndex - 1;
                SelectHotbarSlot(selectedSlotIndex);
            }
            else if (scroll < 0f)
            {
                int upcomingIndex = selectedSlotIndex + 1;
                selectedSlotIndex = upcomingIndex >= itemCount ? 0 : selectedSlotIndex + 1;
                SelectHotbarSlot(selectedSlotIndex);
            }
        }

        private void OnSelectSlot(InputAction.CallbackContext context)
        {
            if (Player.Instance.Character.Health.CurrentLifeState.Value == LifeState.Dead) return;

            var control = context.control;

            if (control is KeyControl key)
            {
                int slotIndex = key.keyCode - Key.Digit1;
                if (slotIndex >= 0 && slotIndex < InventoryManager.Instance.HotbarSize)
                {
                    SelectHotbarSlot(slotIndex);
                }
            }
        }

        private void SelectHotbarSlot(int hotbarSlotIndex)
        {
            if (Player.Instance.Character.Health.CurrentLifeState.Value == LifeState.Dead) return;

            int newIndex = Mathf.Clamp(hotbarSlotIndex, 0, InventoryManager.Instance.HotbarSize - 1);

            if (newIndex == ActiveHotbarIndex) // Ignore same calls
            {
                return;
            }

            AudioManager.Instance.PlayOneShot(FMODEvents.Instance.HotbarSlotChangedSFX, default);

            CancelItemUse();
            // Keep only the held press. Resolve the latest slot when recovery ends.
            _startAfterSwing = Player.Instance.ArmHandler.IsSwinging && GameInput.Instance.PrimaryActionHeld;
            ActiveHotbarIndex = newIndex;
            OnActiveHotbarIndexChanged?.Invoke(ActiveHotbarIndex);
            // Debug.Log($"Active Hotbar Index is: {newIndex}");
        }

        
    }
}
