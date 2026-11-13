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

        [SerializeField, Min(1)] private float _baseWidth = 300;
        [SerializeField] private RectTransform _outline;
        private RectTransform _track;
        private float _outlinePadding;
        public float BaseWidth => _baseWidth;

        private void Awake()
        {
            _track = (RectTransform)_barFg.transform.parent;
            _outlinePadding = _outline.rect.width - _track.rect.width;
        }

        public void UpdateBar(float currentValue, float maxValue, float baseMax)
        {
            float width = _baseWidth * maxValue / Mathf.Max(1, baseMax);
            if (!Mathf.Approximately(_track.rect.width, width))
            {
                _track.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                _outline.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width + _outlinePadding);
            }
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
