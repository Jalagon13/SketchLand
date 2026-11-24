using UnityEngine;

namespace SketchLand
{
    /// <summary>Shows gameplay's eating progress. Never consumes food or advances an action clock.</summary>
    [System.Serializable]
    public sealed class EatingAnimationPose
    {
        [SerializeField] private Transform _head;
        [SerializeField] private PlayerHandGripIK _hand;
        private Transform _rig, _target;
        private PlayerHeldItemDisplay _display;
        private PlayerAnimationProfile _profile;
        private Quaternion _headBase;
        private bool _headApplied;
        public bool IsActive { get; private set; }

        public void Initialize(PlayerHeldItemDisplay display, Transform rig)
        {
            _display = display;
            _rig = rig;
        }

        public void Begin(PlayerAnimationProfile profile)
        {
            _profile = profile;
            if (_head == null || _hand == null) return;
            if (_target == null)
            {
                _target = new GameObject("EatingHandTarget").transform;
                _target.SetParent(_rig, false);
            }
            _target.SetPositionAndRotation(_hand.AnimatedTarget.position, _hand.AnimatedTarget.rotation);
            IsActive = true;
            _display.SetEatingPose(true);
            _hand.BeginGrip(_target, true);
        }

        public void RestorePose()
        {
            if (!_headApplied) return;
            _head.localRotation = _headBase;
            _headApplied = false;
        }

        public void Sample(PlayerActionController state)
        {
            if (!IsActive) return;
            var settings = _profile.Eating;
            float elapsed = Mathf.Max(0f, Time.time - state.StartedAt);
            float raise = Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(0.01f, settings.RaiseDuration));
            float biteTime = Mathf.Max(0f, elapsed - settings.RaiseDuration);
            float bite = Mathf.Sin(biteTime * settings.BiteFrequency * Mathf.PI * 2f);
            float intensity = _profile.Intensity * raise;
            _headBase = _head.localRotation;
            _headApplied = true;
            _head.localRotation *= Quaternion.Euler(0f, 0f, -settings.HeadNod * (0.5f + 0.5f * bite) * intensity);
            Vector3 mouth = _head.position + _rig.TransformVector(
                settings.MouthOffset + new Vector2(-settings.HandBob * bite, settings.HandBob * Mathf.Abs(bite)) * intensity);
            _target.position = Vector3.Lerp(_hand.AnimatedTarget.position, mouth, raise);
            _target.rotation = _hand.AnimatedTarget.rotation;
            _display.SetEatingPose(true, bite * 3f * intensity);
        }

        public void ResetPose()
        {
            RestorePose();
            if (!IsActive) return;
            IsActive = false;
            _hand.ResetGrip();
            _display.SetEatingPose(false);
        }
    }
}
