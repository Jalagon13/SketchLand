using UnityEngine;

namespace SketchLand
{
    public class PlayerStatsDisplayUI : MonoBehaviour
    {
        [SerializeField] private StatBarUI _healthBar;
        [SerializeField] private StatBarUI _staminaBar;

        public StatBarUI HealthBar => _healthBar;

        private void LateUpdate()
        {
            var player = Player.Instance;
            if (player == null || player.Character == null || player.Food == null) return;
            var hp = player.Character.Health;
            _healthBar.UpdateBar(hp.CurrentHealth.Value, hp.Maximum, player.Food.BaseHealth);
            _staminaBar.UpdateBar(player.Stamina.Current, player.Stamina.Max, player.Stamina.BaseMax);
        }
    }
}
