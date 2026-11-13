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

        private bool _cursorPlacementHeld;
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
            if (!_cursorPlacementHeld && !ActiveSlot.IsEmpty && ActiveSlot.GetItemSO() is ToolItemSO tool)
                tool.OnPrimaryActionHeld(player);
            else
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
            if (InventoryCursorManager.Instance.CursorSlot.IsEmpty)
                Player.Instance.TryInteractWithObject();
        }

        private void CancelItemUse()
        {
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
            var item = _activeItem;
            _activeItem = null;
            _activeSlot = null;
            item?.OnPrimaryActionRelease(Player.Instance);
        }

        private void OnPrimaryActionPressed(InputAction.CallbackContext context)
        {
            if (context.canceled)
            {
                _cursorPlacementHeld = false;
                if (_activeItem != null && CanAct() && !WorldManager.Instance.MouseOverUI && HasActiveItem())
                    ReleaseItemUse();
                else
                    CancelItemUse();
                return;
            }

            if (!context.started || !CanAct() || WorldManager.Instance.MouseOverUI) return;

            var slot = GetActiveInvSlot();
            if (slot.IsEmpty) return;
            _activeSlot = slot;
            _activeItem = slot.GetItemSO();
            // Placement can empty the cursor immediately. Keep this press with the cursor item.
            _cursorPlacementHeld = slot == InventoryCursorManager.Instance.CursorSlot && _activeItem is TileItemSO;
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
        
            OnCraftTableInteract?.Invoke(recipes, x, y);
        }

        public void OnToggleInventory()
        {
            if (Player.Instance.Character.Health.CurrentLifeState.Value == LifeState.Dead) return;

            CancelItemUse();
            IsInventoryOpen = !IsInventoryOpen;

            OnInventoryOpenChanged?.Invoke(IsInventoryOpen);
        }
        
        // Used for chest manager
        public void OnToggleInventory(bool showDefaultCraftingMenu = true)
        {
            if (Player.Instance.Character.Health.CurrentLifeState.Value == LifeState.Dead) return;

            CancelItemUse();
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
            ActiveHotbarIndex = newIndex;
            OnActiveHotbarIndexChanged?.Invoke(ActiveHotbarIndex);
            // Debug.Log($"Active Hotbar Index is: {newIndex}");
        }

        
    }
}
