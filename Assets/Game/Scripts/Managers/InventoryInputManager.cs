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
        
        [field: Header("Food consumption")]
        [field: SerializeField, Tooltip("Seconds to hold before the yellow progress fill appears.")]
        public float ConsumeDelay { get; private set; } = 0.25f;

        [field: SerializeField, Tooltip("Seconds to fill the slot after the delay. Zero consumes immediately after the delay.")]
        public float ConsumeDuration { get; private set; } = 1f;

        public InventorySlot ActiveSlot { get; private set; }

        private bool _swingHeld;
        private ItemSO _secondaryActionItem;
        private InventorySlot _secondaryActionSlot;

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
                _swingHeld = false;
                CancelSecondaryAction();
                return;
            }

            if (_secondaryActionItem != null && !HasSecondaryActionItem())
                CancelSecondaryAction();

            if (WorldManager.Instance.MouseOverUI) return;
            if (_swingHeld)
                player.ArmHandler.Swing(ActiveSlot.IsEmpty ? null : ActiveSlot.GetItemSO());
            else
                _secondaryActionItem?.OnSecondaryActionHeld(player);
        }

        private bool CanAct()
        {
            return WorldManager.Instance.IsWorldReady && Player.Instance.Character != null
                && Player.Instance.Character.Health.CurrentLifeState.Value != LifeState.Dead;
        }

        private bool HasSecondaryActionItem()
        {
            var slot = GetActiveInvSlot();
            return slot == _secondaryActionSlot && !slot.IsEmpty && slot.GetItemSO() == _secondaryActionItem;
        }

        private void OnThrowItemPressed()
        {
            if (!CanAct()) return;

            var slot = GetActiveInvSlot();
            if (slot.IsEmpty) return;

            CancelSecondaryAction();
            InventoryCursorManager.Instance.ThrowItem(slot);
        }

        private void OnPrimaryActionPressed(InputAction.CallbackContext context)
        {
            _swingHeld = context.started && CanAct() && !WorldManager.Instance.MouseOverUI;
            if (!_swingHeld) return;

            CancelSecondaryAction();
            var slot = GetActiveInvSlot();
            Player.Instance.ArmHandler.Swing(slot.IsEmpty ? null : slot.GetItemSO());
        }

        private void CancelSecondaryAction()
        {
            var item = _secondaryActionItem;
            _secondaryActionItem = null;
            _secondaryActionSlot = null;
            item?.OnSecondaryActionCancelled(Player.Instance);
        }

        private void OnDisable()
        {
            _swingHeld = false;
            if (Player.Instance != null)
                CancelSecondaryAction();
        }

        private void ReleaseSecondaryAction()
        {
            var item = _secondaryActionItem;
            _secondaryActionItem = null;
            _secondaryActionSlot = null;
            item?.OnSecondaryActionRelease(Player.Instance);
        }

        private void OnSecondaryActionPressed(InputAction.CallbackContext context)
        {
            if (context.canceled)
            {
                if (_secondaryActionItem != null && CanAct() && !WorldManager.Instance.MouseOverUI && HasSecondaryActionItem())
                    ReleaseSecondaryAction();
                else
                    CancelSecondaryAction();
                return;
            }

            if (!context.started || !CanAct() || WorldManager.Instance.MouseOverUI || _swingHeld) return;
            var cursor = InventoryCursorManager.Instance.CursorSlot;
            if (cursor.IsEmpty && Player.Instance.TryInteractWithObject()) return;

            var slot = GetActiveInvSlot();
            if (slot.IsEmpty) return;
            _secondaryActionSlot = slot;
            _secondaryActionItem = slot.GetItemSO();
            _secondaryActionItem.OnSecondaryActionStarted(Player.Instance);
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
        
            OnCraftTableInteract?.Invoke(recipes, x, y);
        }

        public void OnToggleInventory()
        {
            if (Player.Instance.Character.Health.CurrentLifeState.Value == LifeState.Dead) return;

            CancelSecondaryAction();
            _swingHeld = false;
            IsInventoryOpen = !IsInventoryOpen;

            OnInventoryOpenChanged?.Invoke(IsInventoryOpen);
        }
        
        // Used for chest manager
        public void OnToggleInventory(bool showDefaultCraftingMenu = true)
        {
            if (Player.Instance.Character.Health.CurrentLifeState.Value == LifeState.Dead) return;

            CancelSecondaryAction();
            _swingHeld = false;
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

            CancelSecondaryAction();
            ActiveHotbarIndex = newIndex;
            OnActiveHotbarIndexChanged?.Invoke(ActiveHotbarIndex);
            // Debug.Log($"Active Hotbar Index is: {newIndex}");
        }

        
    }
}
