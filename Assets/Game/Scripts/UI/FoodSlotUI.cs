using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SketchLand
{
    public class FoodSlotUI : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _durationText;
        [SerializeField, Range(0, PlayerFood.Capacity - 1)] private int _index;
        [SerializeField] private Button _remove;
        private int _seconds = -1;
        private TooltipHover _hover;
        private Graphic _hoverTarget;
        private ConsumableItemSO _currentFood;

        private void Awake()
        {
            _remove.onClick.AddListener(RemoveFood);
            _hoverTarget = GetComponent<Graphic>();
            _hover = GetComponent<TooltipHover>();
            if (_hover == null) _hover = gameObject.AddComponent<TooltipHover>();
            _hover.OnHoverEnter = ShowFoodTooltip;
        }

        private void ShowFoodTooltip()
        {
            var food = Player.Instance?.Food?.At(_index);
            if (food != null) Tooltip.ItemDescriptionDisplay(food, 1);
            else Tooltip.HideUI();
        }

        private void RemoveFood()
        {
            if (InventoryInputManager.Instance.IsInventoryOpen) Player.Instance.Food?.Remove(_index);
        }

        private void LateUpdate()
        {
            var foods = Player.Instance?.Food;
            var food = foods != null ? foods.At(_index) : null;
            _icon.enabled = food != null;
            _icon.sprite = food != null ? food.DisplayIcon : null;
            _durationText.enabled = food != null;
            _remove.gameObject.SetActive(food != null && InventoryInputManager.Instance.IsInventoryOpen);
            if (_hoverTarget != null) _hoverTarget.raycastTarget = food != null;
            if (_currentFood != food)
            {
                _currentFood = food;
                _hover.RefreshTooltip();
            }
            int seconds = food != null ? Mathf.CeilToInt(foods.Remaining(_index)) : 0;
            if (seconds == _seconds) return;
            _seconds = seconds;
            _durationText.text = $"{seconds / 60}:{seconds % 60:00}";
        }
    }
}
