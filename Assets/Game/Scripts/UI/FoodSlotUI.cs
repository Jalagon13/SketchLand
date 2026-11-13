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

        private void Awake() => _remove.onClick.AddListener(RemoveFood);

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
            int seconds = food != null ? Mathf.CeilToInt(foods.Remaining(_index)) : 0;
            if (seconds == _seconds) return;
            _seconds = seconds;
            _durationText.text = $"{seconds / 60}:{seconds % 60:00}";
        }
    }
}
