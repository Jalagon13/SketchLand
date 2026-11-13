using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SketchLand
{
    public class StatBarUI : MonoBehaviour
    {
        [SerializeField] private Image _barFg;
        [SerializeField] private TextMeshProUGUI _barText;
        [SerializeField] private string _statName;
        
        private int _lastCurrent = -1;
        private int _lastMax = -1;

        public void UpdateBar(float currentValue, float maxValue)
        {
            float fill = maxValue > 0f ? Mathf.Clamp01(currentValue / maxValue) : 0f;
            var rect = _barFg.rectTransform;
            rect.anchorMax = new Vector2(fill, rect.anchorMax.y);

            if(_barText != null)
            {
                int current = Mathf.RoundToInt(currentValue);
                int max = Mathf.RoundToInt(maxValue);
            
                if (_lastCurrent == current && _lastMax == max) return;
                _lastCurrent = current;
                _lastMax = max;
                _barText.text = $"{_statName} {current}/{max}";
            }
        }
    }
}
