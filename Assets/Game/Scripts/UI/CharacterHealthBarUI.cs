using UnityEngine;

namespace SketchLand
{
    public class CharacterHealthBarUI : MonoBehaviour
    {
        [SerializeField, Tooltip("The character whose current and maximum health this bar displays.")]
        private ServerCharacter _character;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private RectTransform _fill;

        public ServerCharacter Character
        {
            get => _character;
            set => _character = value;
        }

        private void OnEnable()
        {
            // Keep the bar visible for positioning in Prefab Mode, but hide it
            // at runtime until the character has initialized its health.
            if (Application.isPlaying && _canvas != null) _canvas.enabled = false;
        }

        private void LateUpdate()
        {
            if (_canvas == null || _fill == null) return;

            if (_character == null || _character.Health == null || _character.Stats == null)
            {
                _canvas.enabled = false;
                return;
            }

            int current = _character.Health.CurrentHealth.Value;
            int maximum = _character.Health.Maximum;
            _canvas.enabled = maximum > 0 && current > 0 && current < maximum;
            _fill.anchorMax = new Vector2(maximum > 0 ? Mathf.Clamp01((float)current / maximum) : 0f, 1f);
        }

        private void OnDisable()
        {
            if (Application.isPlaying && _canvas != null) _canvas.enabled = false;
        }
    }
}
