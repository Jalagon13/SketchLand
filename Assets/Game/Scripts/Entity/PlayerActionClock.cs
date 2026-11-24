using UnityEngine;

namespace SketchLand
{
    /// <summary>A gameplay clock, independent of animation playback or rendering.</summary>
    public sealed class PlayerActionClock
    {
        public float Progress { get; private set; }
        public bool IsComplete => Progress >= 1f;
        private float _duration, _impact;
        private bool _active, _hit;
        public void Begin(float duration, float impact)
        {
            _duration = Mathf.Max(0.001f, duration);
            _impact = Mathf.Clamp01(impact);
            Progress = 0f;
            _active = true;
            _hit = false;
        }
        public bool Advance(float deltaTime)
        {
            if (!_active) return false;
            Progress = Mathf.Clamp01(Progress + Mathf.Max(0f, deltaTime) / _duration);
            bool impact = !_hit && Progress >= _impact;
            _hit |= impact;
            if (IsComplete) _active = false;
            return impact;
        }
        public void Cancel() { _active = false; Progress = 0f; }
    }
}
