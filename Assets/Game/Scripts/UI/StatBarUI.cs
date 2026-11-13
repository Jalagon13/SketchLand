using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace SketchLand
{
    public class StatBarUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image _barFg;
        [SerializeField] private TextMeshProUGUI _barText;
        [SerializeField] private string _statName;
        
        private int _lastCurrent = -1;
        private int _lastMax = -1;

        [SerializeField] private RectTransform _outline;
        private float _baseBorderWidth;

        private bool _hovered;
        private TMP_Text _tooltipText;

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            if (_lastCurrent >= 0) ShowTooltip();
        }

        public void OnPointerExit(PointerEventData eventData) => CloseTooltip();
        private void OnDisable() => CloseTooltip();

        private void CloseTooltip()
        {
            if (_hovered && _tooltipText != null && Tooltip.ReferenceHolder != null)
                Tooltip.HideUI();
            _hovered = false;
            _tooltipText = null;
        }

        private string HoverText => $"{_statName}: {_lastCurrent}/{_lastMax}";

        private void ShowTooltip()
        {
            Tooltip.ShowNew();
            _tooltipText = Tooltip.JustText(HoverText, Color.white, fontSize: 12);
        }

        private void Awake()
        {
            _baseBorderWidth = _outline.rect.width;
        }

        public void UpdateBar(float currentValue, float maxValue, float baseMax)
        {
            float width = baseMax > 0f ? _baseBorderWidth * maxValue / baseMax : _baseBorderWidth;
            if (!Mathf.Approximately(_outline.rect.width, width))
                _outline.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);

            float fill = maxValue > 0f ? Mathf.Clamp01(currentValue / maxValue) : 0f;
            var rect = _barFg.rectTransform;
            rect.anchorMax = new Vector2(fill, rect.anchorMax.y);

            int current = Mathf.RoundToInt(currentValue);
            int max = Mathf.RoundToInt(maxValue);
            if (_lastCurrent == current && _lastMax == max) return;

            _lastCurrent = current;
            _lastMax = max;
            _barText.text = current.ToString();
            if (_hovered)
            {
                if (_tooltipText == null) ShowTooltip();
                else _tooltipText.text = HoverText;
            }
        }
    }
}
