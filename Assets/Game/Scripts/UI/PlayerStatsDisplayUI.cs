using UnityEngine;

namespace SketchLand
{
    public class PlayerStatsDisplayUI : MonoBehaviour
    {
        [SerializeField] private StatBarUI _healthBar;
        [SerializeField] private StatBarUI _staminaBar;

        [SerializeField, Min(0f)] private float _staminaHideDelay = 1f;
        private PlayerStamina _stamina;
        private float _lastStaminaChange;
        private int _lastChangeFrame;

        private void Awake() => _staminaBar.gameObject.SetActive(false);

        private void OnDisable()
        {
            if (_stamina != null) _stamina.OnStaminaChanged -= ShowStamina;
            _stamina = null;
            _staminaBar.gameObject.SetActive(false);
        }

        private void ShowStamina(float current, float max)
        {
            _lastStaminaChange = Time.time;
            _lastChangeFrame = Time.frameCount;
            _staminaBar.gameObject.SetActive(true);
            _staminaBar.UpdateBar(current, max, _stamina.BaseMax);
        }

        public StatBarUI HealthBar => _healthBar;

        private void LateUpdate()
        {
            var player = Player.Instance;
            if (player == null || player.Character == null || player.Food == null) return;
            var hp = player.Character.Health;
            _healthBar.UpdateBar(hp.CurrentHealth.Value, hp.Maximum, player.Food.BaseHealth);
            if (_stamina != player.Stamina)
            {
                if (_stamina != null) _stamina.OnStaminaChanged -= ShowStamina;
                _stamina = player.Stamina;
                _stamina.OnStaminaChanged += ShowStamina;
                if (_stamina.Current < _stamina.Max || _stamina.Max != _stamina.BaseMax)
                    ShowStamina(_stamina.Current, _stamina.Max);
            }

            if (_staminaBar.gameObject.activeSelf && Time.frameCount != _lastChangeFrame &&
                Time.time - _lastStaminaChange >= _staminaHideDelay)
                _staminaBar.gameObject.SetActive(false);
        }
    }
}
