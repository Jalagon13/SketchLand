using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SketchLand
{
    public class InventorySlotUI : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private Image _itemIcon;
        [SerializeField] private TextMeshProUGUI _stackText;

        private InventorySlot _slot;
        private Action _onSlotModified;
        private bool _isPlayerSlot;
        private bool _isHotbarSlot;
        private bool CanEatFromSlot => _isPlayerSlot && (_isHotbarSlot || InventoryInputManager.Instance.IsInventoryOpen);
        private TooltipHover _hover;

        private ConsumableItemSO _heldFood;
        private Image _progress;
        private float _elapsed;
        private bool _suppressLeftClick;

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            CancelHold();
            _suppressLeftClick = false;
            bool foodPress = CanEatFromSlot &&
                !_slot.IsEmpty && _slot.GetItemSO() is ConsumableItemSO && InventoryCursorManager.Instance.CursorSlot.IsEmpty;
            if (!foodPress) return;
            _heldFood = (ConsumableItemSO)_slot.GetItemSO();
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) CancelHold();
        }

        public void OnPointerExit(PointerEventData e) => CancelHold();
        private void OnDisable() => CancelHold();
        private void OnApplicationFocus(bool focused) { if (!focused) CancelHold(); }

        private void CancelHold()
        {
            _heldFood = null;
            _elapsed = 0;
            if (_progress != null) _progress.enabled = false;
        }

        private void Update()
        {
            if (_heldFood == null) return;
            if (!CanEatFromSlot || _slot.IsEmpty || _slot.GetItemSO() != _heldFood ||
                !InventoryCursorManager.Instance.CursorSlot.IsEmpty)
            {
                _suppressLeftClick = true;
                CancelHold();
                return;
            }
            _elapsed += Time.deltaTime;
            var settings = InventoryInputManager.Instance;
            float fillTime = _elapsed - settings.ConsumeDelay;
            if (fillTime < 0f) return;
            // Unity sends a click after pointer-up, even when the press was a hold.
            _suppressLeftClick = true;
            var food = Player.Instance.Food;
            if (food == null || !food.CanEat(_heldFood))
            {
                CancelHold();
                return;
            }
            if (_progress == null)
            {
                _progress = new GameObject("Eating progress", typeof(Image)).GetComponent<Image>();
                var rect = _progress.rectTransform;
                rect.SetParent(transform, false);
                rect.SetSiblingIndex(_itemIcon.transform.GetSiblingIndex());
                rect.anchorMin = Vector2.zero;
                rect.offsetMin = new Vector2(3, 3);
                rect.offsetMax = new Vector2(-3, 3);
                _progress.color = new Color(1, 0.78f, 0.08f, 0.85f);
                _progress.raycastTarget = false;
            }
            float amount = settings.ConsumeDuration > 0f ? Mathf.Clamp01(fillTime / settings.ConsumeDuration) : 1f;
            _progress.enabled = true;
            _progress.rectTransform.anchorMax = new Vector2(1, amount);
            _progress.rectTransform.offsetMax = new Vector2(-3, 3 - 6 * amount);
            if (amount < 1) return;
            food.Eat(_slot, _heldFood);
            CancelHold();
        }

        private void OnDestroy()
        {
            if (_isPlayerSlot && InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnPlayerInventoryChanged -= RefreshUI;
            }
        }

        // Called for Player Hotbar / Main Inventory
        public void Initialize(int index)
        {
            _slot = InventoryManager.Instance.PlayerInventory[index];
            _isPlayerSlot = true;
            _isHotbarSlot = index < InventoryManager.Instance.HotbarSize;
            _onSlotModified = null;
            ConfigureTooltipHover();

            InventoryManager.Instance.OnPlayerInventoryChanged += RefreshUI;
            RefreshUI();
        }

        // Called for Chests or external containers
        public void Initialize(InventorySlot slot, Action onSlotModified)
        {
            _slot = slot;
            _isPlayerSlot = false;
            _onSlotModified = onSlotModified;
            ConfigureTooltipHover();

            RefreshUI();
        }

        private void ConfigureTooltipHover()
        {
            _hover = GetComponent<TooltipHover>();
            if (_hover == null)
            {
                _hover = gameObject.AddComponent<TooltipHover>();
            }

            _hover.OnHoverEnter = () =>
            {
                if (_slot != null && !_slot.IsEmpty)
                {
                    Tooltip.ItemDescriptionDisplay(_slot.GetItemSO(), _slot.Amount);
                }
                else
                    Tooltip.HideUI();
            };
        }

        public void RefreshUI()
        {
            if (_slot == null || _slot.IsEmpty)
            {
                _itemIcon.enabled = false;
                _stackText.text = string.Empty;
            }
            else
            {
                _itemIcon.enabled = true;
                _itemIcon.sprite = GameDataRegistry.Instance.GetItemSOFromItemId(_slot.ItemId).DisplayIcon;
                _stackText.text = _slot.Amount > 1 ? _slot.Amount.ToString() : string.Empty;
            }
            _hover?.RefreshTooltip();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!InventoryInputManager.Instance.IsInventoryOpen || _slot == null) return;
            if (Player.Instance.Character.Health.CurrentLifeState.Value == LifeState.Dead) return;

            if (eventData.button == PointerEventData.InputButton.Left)
            {
                if (_suppressLeftClick) return;
                InventoryCursorManager.Instance.HandleSlotLeftClick(_slot, _onSlotModified);
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                InventoryCursorManager.Instance.HandleSlotRightClick(_slot, _onSlotModified);
            }
        }
    }
}