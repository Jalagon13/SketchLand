using UnityEngine;

namespace SketchLand
{
    /// <summary>Samples a visual performance from gameplay progress. Never schedules an impact.</summary>
    [System.Serializable]
    public sealed class SwingAnimationPose
    {
        [SerializeField] private Transform _armPivot;
        [SerializeField] private Transform _arm;
        [SerializeField] private PlayerHandGripIK _handGripIK;
        [SerializeField] private SwingBodyPose _pose = new();
        private PlayerHeldItemDisplay _display;
        private SwingSlashEffect _slash;
        private PlayerAnimationProfile _profile;
        private bool _raised, _returning;
        private AnimationCurve _slashCurve;
        public bool IsActive { get; private set; }

        public void Initialize(PlayerHeldItemDisplay display)
        {
            _display = display;
            _pose.Initialize(_arm);
            _slash = new SwingSlashEffect(_armPivot, display.HeldItemSprite);
            _armPivot.gameObject.SetActive(false);
        }
        public void RestorePose() => _pose.RestorePose();
        public void Begin(PlayerActionController state, PlayerAnimationProfile profile)
        {
            _profile = profile;
            _raised = _returning = false;
            IsActive = true;
            Vector2 localAim = _armPivot.parent.InverseTransformVector(state.AimDirection);
            _arm.localScale = new Vector3(-1f, 1f, 1f);
            _arm.localRotation = Quaternion.Euler(0f, 0f, 180f);
            _armPivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(localAim.y, localAim.x) * Mathf.Rad2Deg);
            _pose.Begin(profile);
            _armPivot.gameObject.SetActive(true);
            _display.BeginSwing(state.Item);
            _handGripIK.BeginGrip(state.SwingRate);
            float wind = Mathf.Clamp(profile.Swing.WindUpEnd, 0.01f, 0.8f);
            float strike = Mathf.Clamp(profile.Swing.StrikeEnd, wind + 0.01f, 0.95f);
            float back = Mathf.Clamp(profile.Swing.ReturnStart, strike + 0.01f, 0.99f);
            float half = state.SwingHalfArc;
            float anticipation = profile.Swing.Exaggeration * Mathf.Min(32f, half * 0.4f);
            float follow = profile.Swing.Exaggeration * half * 0.56f;
            float slope = (3f * follow / (back - strike)) * (strike - wind) / (2f * (half + anticipation));
            _slashCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f),
                new Keyframe(0.5f, 0.5f, 1.65f, 1.65f), new Keyframe(1f, 1f, slope, slope));
        }
        public void Sample(PlayerActionController state)
        {
            if (!IsActive) return;
            var settings = _profile.Swing;
            float t = Mathf.Clamp01(settings.TimeCurve.Evaluate(state.SwingProgress));
            float wind = Mathf.Clamp(settings.WindUpEnd, 0.01f, 0.8f);
            float strike = Mathf.Clamp(settings.StrikeEnd, wind + 0.01f, 0.95f);
            float back = Mathf.Clamp(settings.ReturnStart, strike + 0.01f, 0.99f);
            Vector2 localAim = _armPivot.parent.InverseTransformVector(state.AimDirection);
            float center = Mathf.Atan2(localAim.y, localAim.x) * Mathf.Rad2Deg;
            float half = state.SwingHalfArc;
            float anticipation = settings.Exaggeration * Mathf.Min(32f, half * 0.4f);
            float start = center + half + anticipation, end = center - half - anticipation;
            float follow = settings.Exaggeration * half * 0.56f;
            float angle;
            if (t < wind)
            {
                float u = t / wind;
                float ease = u < 0.5f ? 4f * u * u * u : 1f - Mathf.Pow(-2f * u + 2f, 3f) * 0.5f;
                angle = Mathf.Lerp(center, start, ease);
                _display.SetTransitionProgress(u);
            }
            else
            {
                if (!_raised) { _display.FinishRaise(); _raised = true; }
                if (t < strike)
                {
                    angle = Mathf.Lerp(start, end, _slashCurve.Evaluate((t - wind) / (strike - wind)));
                }
                else angle = Mathf.Lerp(end, end - follow, 1f - Mathf.Pow(1f - Mathf.Clamp01((t - strike) / (back - strike)), 3f));
                if (t >= back)
                {
                    if (!_returning) { _display.BeginReturn(); _handGripIK.ReleaseGrip(); _returning = true; }
                    _display.SetTransitionProgress((t - back) / (1f - back));
                }
            }
            _armPivot.localRotation = Quaternion.Euler(0f, 0f, angle);
            float travel = t < wind ? 0f : t < strike ? Mathf.Clamp01((center + half - angle) / (2f * half))
                : Mathf.Lerp(1f, 1.82f, 1f - Mathf.Pow(1f - Mathf.Clamp01((t - strike) / 0.075f), 2f));
            _slash.Draw(travel, center, half, state.SwingReach);
            _pose.Apply(t);
            _display.UpdateSwingTarget();
        }
        public void ResetPose()
        {
            if (!IsActive) return;
            IsActive = false;
            _pose.End();
            _slash.Hide();
            _handGripIK.ResetGrip();
            _display.EndSwing();
            _armPivot.gameObject.SetActive(false);
        }
    }
}
