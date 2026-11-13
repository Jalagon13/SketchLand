using UnityEngine;

namespace SketchLand
{
    public class PlayerStatsDisplayUI : MonoBehaviour
    {
        [SerializeField] private StatBarUI _healthBar;

        [SerializeField] private StatBarUI _staminaBar;

        private ServerCharacter _character;

        private void Start()
        {
            var player = Player.Instance;
        
            if (player == null) return;

            player.Stamina.OnStaminaChanged += _staminaBar.UpdateBar;
            player.PlayerReady += OnPlayerReady;

            if (player.Character != null)
                OnPlayerReady(player.Character);

        }

        private void OnDestroy()
        {
            var player = Player.Instance;

            UnbindCharacter();

            if (player != null)
            {
                player.Stamina.OnStaminaChanged -= _staminaBar.UpdateBar;
                player.PlayerReady -= OnPlayerReady;
            }
        }

        private void UnbindCharacter()
        {
            if (_character == null) return;

            _character.Health.CurrentHealth.OnValueChanged -= OnHealthChanged;
            _character.Stats.OnBuffStarted -= UpdateStatBar;
            _character.Stats.OnBuffStopped -= UpdateStatBar;
            _character = null;
        }

        private void OnPlayerReady(ServerCharacter player)
        {
            UnbindCharacter();
            _character = player;
            var stamina = Player.Instance.Stamina;
            _staminaBar.UpdateBar(stamina.Current, stamina.Max);
            player.Health.CurrentHealth.OnValueChanged += OnHealthChanged;
            player.Stats.OnBuffStarted += UpdateStatBar;
            player.Stats.OnBuffStopped += UpdateStatBar;

            _healthBar.UpdateBar(player.Health.CurrentHealth.Value, player.Stats.MaxHealth.GetValue());
        }

        private void UpdateStatBar(Buff buff)
        {
            _healthBar.UpdateBar(_character.Health.CurrentHealth.Value, _character.Stats.MaxHealth.GetValue());
        }

        private void OnHealthChanged(int previousHp, int currentHp)
        {
            _healthBar.UpdateBar(currentHp, _character.Stats.MaxHealth.GetValue());
        }
    }
}