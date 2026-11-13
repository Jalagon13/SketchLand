using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace SketchLand
{
    [Serializable]
    public class PlayerStamina
    {
        public event Action<float, float> OnStaminaChanged;

        [SerializeField, Min(0f)] private float _max = 100f;
        [FormerlySerializedAs("_regenPerSecond")]
        [SerializeField, Min(0f)] private float _baseRegenPerSecond = 10f;
        [SerializeField, Min(0f)] private float _maxRegenPerSecond = 25f;
        [SerializeField, Min(0f)] private float _regenRampSeconds = 3f;
        [SerializeField, Min(0f)] private float _regenDelaySeconds = 1f;
        [SerializeField, Min(0f)] private float _sprintCostPerSecond = 12f;
        [SerializeField, Min(0f)] private float _swingCost = 15f;

        public float BaseMax => _max;
        private float _foodMax;

        public void SetFoodMaxBonus(float max)
        {
            _foodMax = max;
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

        public float RegenDelaySeconds { get => _regenDelaySeconds; set => _regenDelaySeconds = Mathf.Max(0f, value); }
        public float SprintCostPerSecond { get => _sprintCostPerSecond; set => _sprintCostPerSecond = Mathf.Max(0f, value); }
        public float SwingCost { get => _swingCost; set => _swingCost = Mathf.Max(0f, value); }
        public float Current => Mathf.Min(_current, Max);

        private float _current;
        private float _lastMax;

        private float _regenDelayRemaining;

        private float _regenElapsed;

        private void InterruptRecovery()
        {
            _regenElapsed = 0f;
            _regenDelayRemaining = RegenDelaySeconds;
        }

        public void Refill()
        {
            _regenElapsed = 0f;
            _regenDelayRemaining = 0f;
            SetCurrent(Max);
        }

        public bool TrySpendSwing() => TrySpend(SwingCost);

        public bool TrySpend(float amount)
        {
            if (amount < 0f || Current < amount) return false;

            if (amount > 0f) InterruptRecovery();
            SetCurrent(Current - amount);
            return true;
        }

        public void Tick(float deltaTime, bool sprinting)
        {
            if (sprinting)
            {
                InterruptRecovery();
                SetCurrent(Current - SprintCostPerSecond * deltaTime);
                return;
            }

            float waitingTime = Mathf.Min(deltaTime, _regenDelayRemaining);
            _regenDelayRemaining -= waitingTime;
            float recoveryTime = deltaTime - waitingTime;
            if (recoveryTime <= 0f || Current >= Max) return;

            float maxRate = Mathf.Max(_baseRegenPerSecond, _maxRegenPerSecond);
            float rampTime = Mathf.Clamp(_regenRampSeconds - _regenElapsed, 0f, recoveryTime);
            float recovered = maxRate * (recoveryTime - rampTime);
            if (rampTime > 0f)
            {
                // The midpoint rate gives the exact gain over this part of the linear ramp.
                float midpoint = (_regenElapsed + rampTime * 0.5f) / _regenRampSeconds;
                recovered += Mathf.Lerp(_baseRegenPerSecond, maxRate, midpoint) * rampTime;
                _regenElapsed += rampTime;
            }
            SetCurrent(Current + recovered);
        }

        private void SetCurrent(float value)
        {
            float max = Max;
            float current = Mathf.Clamp(value, 0f, max);
            if (current >= max) _regenElapsed = 0f;
            if (_current == current && _lastMax == max) return;

            _current = current;
            _lastMax = max;
            OnStaminaChanged?.Invoke(current, max);
        }
    }
}
