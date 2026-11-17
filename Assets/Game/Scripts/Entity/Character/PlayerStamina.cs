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
        [SerializeField, Min(0f)] private float _maxRegenPerSecond = 35f;
        [SerializeField, Min(0f)] private float _regenRampSeconds = 3f;
        [SerializeField, Min(0f)] private float _swingCost = 5f;
        [Header("Movement Regen Percentages")]
        [Tooltip("Percentage of normal regeneration applied while standing still, including minimum and ramped rates.")]
        [SerializeField, Range(0f, 100f)] private float _standingRegenPercent = 100f;
        [SerializeField, Range(0f, 100f)] private float _walkingRegenPercent = 75f;
        [SerializeField, Range(0f, 100f)] private float _sprintingRegenPercent = 50f;

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

        public float SwingCost { get => _swingCost; set => _swingCost = Mathf.Max(0f, value); }
        public float Current => Mathf.Min(_current, Max);

        private float _current;
        private float _lastMax;

        private float _regenElapsed;

        public void Refill()
        {
            _regenElapsed = 0f;
            SetCurrent(Max);
        }

        public bool TrySpendSwing() => TrySpend(SwingCost);

        public bool TrySpend(float amount)
        {
            if (amount < 0f || Current < amount) return false;

            if (amount > 0f) _regenElapsed = 0f;
            SetCurrent(Current - amount);
            return true;
        }

        public void Tick(float deltaTime, bool moving, bool sprinting)
        {
            float regenPercent = sprinting ? _sprintingRegenPercent
                : moving ? _walkingRegenPercent : _standingRegenPercent;
            if (Current >= Max) return;

            float maxRate = Mathf.Max(_baseRegenPerSecond, _maxRegenPerSecond);
            float rampTime = Mathf.Clamp(_regenRampSeconds - _regenElapsed, 0f, deltaTime);
            float recovered = maxRate * (deltaTime - rampTime);
            if (rampTime > 0f)
            {
                // The midpoint rate gives the exact gain over this part of the linear ramp.
                float midpoint = (_regenElapsed + rampTime * 0.5f) / _regenRampSeconds;
                recovered += Mathf.Lerp(_baseRegenPerSecond, maxRate, midpoint) * rampTime;
                _regenElapsed += rampTime;
            }
            SetCurrent(Current + recovered * (regenPercent * 0.01f));
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
