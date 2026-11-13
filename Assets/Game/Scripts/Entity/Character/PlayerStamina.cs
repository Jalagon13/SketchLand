using System;
using UnityEngine;

namespace SketchLand
{
    [Serializable]
    public class PlayerStamina
    {
        public event Action<float, float> OnStaminaChanged;

        [SerializeField, Min(0f)] private float _max = 100f;
        [SerializeField, Min(0f)] private float _regenPerSecond = 10f;
        [SerializeField, Min(0f)] private float _regenDelaySeconds = 1f;
        [SerializeField, Min(0f)] private float _sprintCostPerSecond = 12f;
        [SerializeField, Min(0f)] private float _swingCost = 15f;

        public float BaseMax => _max;
        private float _foodMax;
        private float _foodRegen;

        public void SetFoodBonuses(float max, float regen)
        {
            _foodMax = max;
            _foodRegen = regen;
            SetCurrent(Current);
        }

        public float Max
        {
            get => _max + _foodMax;
            set
            {
                _max = Mathf.Max(0f, value);
                SetCurrent(Current);
            }
        }

        public float RegenPerSecond { get => _regenPerSecond + _foodRegen; set => _regenPerSecond = Mathf.Max(0f, value); }
        public float RegenDelaySeconds { get => _regenDelaySeconds; set => _regenDelaySeconds = Mathf.Max(0f, value); }
        public float SprintCostPerSecond { get => _sprintCostPerSecond; set => _sprintCostPerSecond = Mathf.Max(0f, value); }
        public float SwingCost { get => _swingCost; set => _swingCost = Mathf.Max(0f, value); }
        public float Current => Mathf.Min(_current, Max);

        private float _current;
        private float _lastMax;

        private float _regenDelayRemaining;

        public void Refill()
        {
            _regenDelayRemaining = 0f;
            SetCurrent(Max);
        }

        public bool TrySpendSwing() => TrySpend(SwingCost);

        public bool TrySpend(float amount)
        {
            if (amount < 0f || Current < amount) return false;

            if (amount > 0f) _regenDelayRemaining = RegenDelaySeconds;
            SetCurrent(Current - amount);
            return true;
        }

        public void Tick(float deltaTime, bool sprinting)
        {
            if (sprinting)
            {
                _regenDelayRemaining = RegenDelaySeconds;
                SetCurrent(Current - SprintCostPerSecond * deltaTime);
                return;
            }

            float waitingTime = Mathf.Min(deltaTime, _regenDelayRemaining);
            _regenDelayRemaining -= waitingTime;
            SetCurrent(Current + RegenPerSecond * (deltaTime - waitingTime));
        }

        private void SetCurrent(float value)
        {
            float max = Max;
            float current = Mathf.Clamp(value, 0f, max);
            if (_current == current && _lastMax == max) return;

            _current = current;
            _lastMax = max;
            OnStaminaChanged?.Invoke(current, max);
        }
    }
}
