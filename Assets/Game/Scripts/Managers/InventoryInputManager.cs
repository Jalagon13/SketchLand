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

        private ItemSO _primaryActionItem;
        private InventorySlot _primaryActionSlot;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            GameInput.Instance.OnScrollWheel += OnScrollWheel;
            GameInput.Instance.OnSelectSlot += OnSelectSlot;
            GameInput.Instance.OnToggleInventory += OnToggleInventory;
            GameInput.Instance.OnPrimaryActionPressed += OnPrimaryActionPressed;
            GameInput.Instance.OnSecondaryActionPressed += OnSecondaryActionPressed;
        }

        private void OnDestroy()
        {
            GameInput.Instance.OnScrollWheel -= OnScrollWheel;
            GameInput.Instance.OnSelectSlot -= OnSelectSlot;
            GameInput.Instance.OnToggleInventory -= OnToggleInventory;
            GameInput.Instance.OnPrimaryActionPressed -= OnPrimaryActionPressed;
            GameInput.Instance.OnSecondaryActionPressed -= OnSecondaryActionPressed;
        }

        private void Update()
        {
            ActiveSlot = GetActiveInvSlot();
            // Consuming the cursor stack can reveal the selected hotbar tool.
            // That tool must not inherit the placement press.
            if (_primaryActionItem != null &&
                (ActiveSlot != _primaryActionSlot || ActiveSlot.IsEmpty || ActiveSlot.GetItemSO() != _primaryActionItem))
            {
                CancelPrimaryAction();
            }
            if (_primaryActionItem != null && (IsInventoryOpen || Player.Instance.Character.Health.CurrentLifeState.Value == LifeState.Dead))
                CancelPrimaryAction();
            if(ActiveSlot.IsEmpty) return;
            
            if(Player.Instance.Character.Health.CurrentLifeState.Value == LifeState.Dead) return;
            if (WorldManager.Instance.MouseOverUI) return;
            
            var player = Player.Instance;

            if (GameInput.Instance.PrimaryActionHeld)
            {
                _primaryActionItem?.OnPrimaryActionHeld(player);
            }
            else if(GameInput.Instance.SecondaryActionHeld)
            {
                ActiveSlot.GetItemSO().OnSecondaryActionHeld(player);
            }
        }

        private void OnPrimaryActionPressed(InputAction.CallbackContext context)
        {
            // Release the item that received the press, even if selection changed
            // or the player died before releasing the button.
            if (context.phase == InputActionPhase.Canceled)
            {
                if (IsInventoryOpen || WorldManager.Instance.MouseOverUI
                    || Player.Instance.Character.Health.CurrentLifeState.Value == LifeState.Dead
                    || GetActiveInvSlot() != _primaryActionSlot)
                    CancelPrimaryAction();
                else
                    ReleasePrimaryAction();
                return;
            }

            if (context.phase != InputActionPhase.Started) return;
            CancelPrimaryAction();
            if (IsInventoryOpen) return;
            if (Player.Instance.Character.Health.CurrentLifeState.Value == LifeState.Dead) return;

            var activeSlot = GetActiveInvSlot();
            if (activeSlot.IsEmpty || WorldManager.Instance.MouseOverUI) return;

            _primaryActionSlot = activeSlot;
            _primaryActionItem = activeSlot.GetItemSO();
            _primaryActionItem.OnPrimaryActionStarted(Player.Instance);
        }

        private void CancelPrimaryAction()
        {
            var item = _primaryActionItem;
            _primaryActionItem = null;
            _primaryActionSlot = null;
            item?.OnPrimaryActionCancelled(Player.Instance);
        }

        private void OnDisable()
        {
            if (Player.Instance != null)
                CancelPrimaryAction();
        }

        private void ReleasePrimaryAction()
        {
            var item = _primaryActionItem;
            _primaryActionItem = null;
            _primaryActionSlot = null;
            item?.OnPrimaryActionRelease(Player.Instance);
        }

        private void OnSecondaryActionPressed(InputAction.CallbackContext context)
        {
            if (Player.Instance.Character.Health.CurrentLifeState.Value == LifeState.Dead) return;

            var activeSlot = GetActiveInvSlot();
            
            if (!activeSlot.IsEmpty)
            {
                var player = Player.Instance;

                if (context.phase == InputActionPhase.Started)
                {
                    if (WorldManager.Instance.MouseOverUI) return;

                    activeSlot.GetItemSO().OnSecondaryActionStarted(player);
                }
                else if (context.phase == InputActionPhase.Canceled)
                {
                    activeSlot.GetItemSO().OnSecondaryActionRelease(player);
                }

            }
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

            CancelPrimaryAction();
            IsInventoryOpen = !IsInventoryOpen;

            OnInventoryOpenChanged?.Invoke(IsInventoryOpen);
        }
        
        // Used for chest manager
        public void OnToggleInventory(bool showDefaultCraftingMenu = true)
        {
            if (Player.Instance.Character.Health.CurrentLifeState.Value == LifeState.Dead) return;

            CancelPrimaryAction();
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

            CancelPrimaryAction();
            ActiveHotbarIndex = newIndex;
            OnActiveHotbarIndexChanged?.Invoke(ActiveHotbarIndex);
            // Debug.Log($"Active Hotbar Index is: {newIndex}");
        }

        
    }
}
