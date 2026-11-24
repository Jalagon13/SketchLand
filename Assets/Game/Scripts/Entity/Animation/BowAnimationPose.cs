using UnityEngine;

namespace SketchLand
{
    // Update aim and targets after the Animator, before hand blending (-20) and IK (-10).
    [System.Serializable]
    public sealed class BowAnimationPose
    {
        [Header("Rig")]
        [SerializeField] private Transform _aimRoot;
        [SerializeField] private Transform _shoulder;
        [SerializeField] private Transform _bowGrip;
        [SerializeField] private Transform _drawHand;
        [SerializeField] private Transform _nock;
        [SerializeField] private PlayerHandGripIK _leftHand;
        [SerializeField] private PlayerHandGripIK _rightHand;
        [SerializeField] private SpriteRenderer _bowSprite;
        [SerializeField] private Transform _upperStringTip;
        [SerializeField] private Transform _lowerStringTip;
        [SerializeField] private LineRenderer _string;
        [SerializeField] private SpriteRenderer _arrowSprite;
        [SerializeField] private Transform _arrowRoot;

        [Header("Body Pose")]
        [SerializeField] private Transform _hip;
        [SerializeField] private Transform _spine;
        [SerializeField] private Transform _head;

        private PlayerAnimationProfile _profile;
        private float _bodyExaggeration => _profile.Intensity;
        private Vector2 _bowPosition => _profile.Bow.BowPosition;
        private Vector2 _readyHandPosition => _profile.Bow.ReadyHandPosition;
        private Vector2 _drawHandControlPosition => _profile.Bow.DrawHandControlPosition;
        private Vector2 _drawnHandPosition => _profile.Bow.DrawnHandPosition;
        private AnimationCurve _drawCurve => _profile.Bow.DrawCurve;
        private float _raiseDuration => Mathf.Max(0.01f, _profile.Bow.RaiseDuration);
        private float _releaseDuration => Mathf.Max(0.01f, _profile.Bow.ReleaseDuration);
        private float _returnDuration => Mathf.Max(0.01f, _profile.Bow.ReturnDuration);

        private PlayerHeldItemDisplay _heldItemDisplay;
        private float _charge;
        private float _minimumCharge;
        private float _poseTime;
        private float _releaseTime;
        private float _releaseCharge;
        private bool _isReleasing;
        private bool _isReturning;
        private Vector3 _arrowTailOffset;
        private Vector3 _hipBase;
        private Quaternion _hipRotationBase, _spineBase, _headBase;
        private bool _bodyPoseApplied;
        public bool IsActive { get; private set; }
        public Vector2 AimDirection { get; private set; } = Vector2.right;
        public void Initialize(PlayerHeldItemDisplay display)
        {
            _heldItemDisplay = display;
            _aimRoot.gameObject.SetActive(false);
        }

        public void RestorePose() => RestoreBodyPose();

        public void Sample(PlayerActionController state)
        {
            if (!IsActive) return;
            _poseTime = Time.time - state.StartedAt;
            _charge = state.Charge;
            AimDirection = state.AimDirection;
            if (state.Kind == PlayerActionKind.BowReleased)
            {
                _isReleasing = true;
                _releaseTime = Time.time - state.ReleasedAt;
                _releaseCharge = state.Charge;
                if (!_isReturning && _releaseTime >= _releaseDuration)
                {
                    _isReturning = true;
                    _leftHand.ReleaseGrip();
                    _rightHand.ReleaseGrip();
                }
                if (_releaseTime >= _releaseDuration + Mathf.Max(_returnDuration, _leftHand.BlendDuration, _rightHand.BlendDuration))
                {
                    ResetPose();
                    return;
                }
            }
            UpdateAim();
        }

        public void BeginDraw(BowItemSO bow, ArrowItemSO arrow, PlayerAnimationProfile profile, Vector2 direction)
        {
            _profile = profile;
            AimDirection = direction;
            IsActive = true;
            _isReleasing = false;
            _isReturning = false;
            _charge = 0f;
            _minimumCharge = bow.MinimumCharge;
            _poseTime = 0f;
            _releaseTime = 0f;
            _arrowSprite.enabled = false;
            _aimRoot.gameObject.SetActive(true);
            
            // Keep the authored sprite transform; only the selected bow supplies the artwork.
            _bowSprite.sprite = bow.DrawnSprite;
            var source = arrow.Projectile.ProjectilePrefab.GetComponentInChildren<SpriteRenderer>();
            _arrowSprite.sprite = source != null ? source.sprite : arrow.DisplayIcon;
            _arrowSprite.transform.localRotation = source != null ? source.transform.localRotation : Quaternion.identity;
            _arrowSprite.transform.localScale = source != null ? source.transform.localScale : Vector3.one;
            _arrowSprite.transform.localPosition = source != null ? source.transform.localPosition : Vector3.zero;
            _arrowRoot.localScale = source != null && source.transform.parent != null ? source.transform.parent.localScale : Vector3.one;
            _arrowTailOffset = Vector3.zero;
            
            if (_arrowSprite.sprite != null)
            {
                Vector3 min = Vector3.positiveInfinity;
                Vector3 max = Vector3.negativeInfinity;
                
                foreach (Vector2 vertex in _arrowSprite.sprite.vertices)
                {
                    Vector3 point = Vector3.Scale(_arrowRoot.localScale, _arrowSprite.transform.localPosition + _arrowSprite.transform.localRotation * Vector3.Scale(_arrowSprite.transform.localScale, vertex));
                    min = Vector3.Min(min, point);
                    max = Vector3.Max(max, point);
                }
                _arrowTailOffset = new Vector3(min.x, (min.y + max.y) * 0.5f, 0f);
            }
            
            _heldItemDisplay.SetBowPoseActive(true);
            
            UpdateAim();
            ApplyPose();
            
            _leftHand.BeginGrip(_bowGrip);
            
            // This bend lets the elbow travel behind the shoulder as the hand reaches the cheek.
            _rightHand.BeginGrip(_drawHand, false);
        }

        private void UpdateAim()
        {
            RestoreBodyPose();
            ApplyBodyPose();
            _aimRoot.position = _shoulder.position;
            
            Vector3 localAim = _aimRoot.parent.InverseTransformVector(AimDirection);
            _aimRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(localAim.y, localAim.x) * Mathf.Rad2Deg);
            
            ApplyPose();
        }

        private float BodyWeight(float draw, float delay)
        {
            float drawTime = _isReleasing ? _poseTime - _releaseTime : _poseTime;
            float raised = Mathf.SmoothStep(0f, 1f, drawTime / _raiseDuration);
            // Dip toward the bow as it rises, then pull back toward upright.
            // Negative weight leans forward; full draw stays close to neutral.
            float weight = -raised * Mathf.Lerp(0.65f, 0.025f, draw);
            if (!_isReleasing) return weight;

            // Shoulders spring forward first; hips and head follow a little later.
            float time = Mathf.Max(0f, _releaseTime - delay);
            float kickDuration = _releaseDuration * 0.65f;
            float settleDuration = Mathf.Max(0.01f, _releaseDuration + _returnDuration - delay - kickDuration);
            if (time < kickDuration)
                return Mathf.SmoothStep(weight, -weight * 0.3f, time / kickDuration);
            return Mathf.SmoothStep(-weight * 0.3f, 0f, (time - kickDuration) / settleDuration);
        }

        private void ApplyBodyPose()
        {
            if (!IsActive || _hip == null || _spine == null || _head == null) return;

            _hipBase = _hip.localPosition;
            _hipRotationBase = _hip.localRotation;
            _spineBase = _spine.localRotation;
            _headBase = _head.localRotation;
            _bodyPoseApplied = true;

            float draw = Mathf.Clamp01(_drawCurve.Evaluate(_isReleasing ? _releaseCharge : _charge));
            float hips = BodyWeight(draw, 0.015f) * _bodyExaggeration;
            float chest = BodyWeight(draw, 0f) * _bodyExaggeration;
            float head = BodyWeight(Mathf.SmoothStep(0f, 1f, draw), 0.035f) * _bodyExaggeration;
            // A small tension/breathing motion keeps a held draw alive.
            float breathTime = _isReleasing ? _poseTime - _releaseTime : _poseTime;
            float breathFade = _isReleasing ? Mathf.SmoothStep(1f, 0f, _releaseTime / (_releaseDuration + _returnDuration)) : 1f;
            float breath = Mathf.Sin(breathTime * 3.2f) * 0.2f * draw * draw * _bodyExaggeration * breathFade;

            if (!_profile.UpperBodyOnly)
            {
                _hip.localPosition += new Vector3(-0.075f * hips, -0.035f * Mathf.Abs(hips), 0f);
                _hip.localRotation *= Quaternion.Euler(0f, 0f, 5f * hips);
            }
            else hips = 0f;
            _spine.localRotation *= Quaternion.Euler(0f, 0f, 14f * chest + breath);
            // Counter the torso lean so the face stays focused along the shot.
            _head.localRotation *= Quaternion.Euler(0f, 0f, -5f * hips - 14f * chest + 5f * head - breath * 0.7f);
        }

        private void RestoreBodyPose()
        {
            if (!_bodyPoseApplied) return;
            _hip.SetLocalPositionAndRotation(_hipBase, _hipRotationBase);
            _spine.localRotation = _spineBase;
            _head.localRotation = _headBase;
            _bodyPoseApplied = false;
        }

        private Vector2 GetDrawHandPosition(float charge)
        {
            float draw = Mathf.Clamp01(_drawCurve.Evaluate(charge));
            Vector2 start = Vector2.Lerp(_readyHandPosition, _drawHandControlPosition, draw);
            Vector2 end = Vector2.Lerp(_drawHandControlPosition, _drawnHandPosition, draw);
            return Vector2.Lerp(start, end, draw);
        }

        private void ApplyPose()
        {
            Vector2 hand = GetDrawHandPosition(_charge);
            Vector2 nock = hand;
            
            if (_isReleasing)
            {
                float release = Mathf.Clamp01(_releaseTime / _releaseDuration);
                hand = Vector2.Lerp(GetDrawHandPosition(_releaseCharge), _drawnHandPosition + new Vector2(-0.07f, 0.02f), Mathf.Sin(release * Mathf.PI * 0.5f));
                nock = Vector2.Lerp(GetDrawHandPosition(_releaseCharge), _readyHandPosition, Mathf.Clamp01(release * 3f));
            }

            // The bow itself rises from the animated left hand, independently of solved bones.
            float raise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_poseTime / _raiseDuration));
            Vector3 restingGrip = _aimRoot.InverseTransformPoint(_leftHand.AnimatedTarget.position);
            
            if (_isReturning)
                raise = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_releaseTime - _releaseDuration) / _returnDuration));
                
            _bowGrip.localPosition = Vector3.Lerp(restingGrip, _bowPosition, raise);
            _drawHand.localPosition = hand;
            _nock.localPosition = nock;

            // Attachment transforms live beneath the sprite so Inspector pose edits carry the string tips.
            _string.positionCount = 3;
            _string.SetPosition(0, _string.transform.InverseTransformPoint(_upperStringTip.position));
            _string.SetPosition(1, _string.transform.InverseTransformPoint(_nock.position));
            _string.SetPosition(2, _string.transform.InverseTransformPoint(_lowerStringTip.position));

            _arrowRoot.localPosition = (Vector3)nock - _arrowTailOffset;
            
            // Reveal only after raising and the minimum hold; quick taps never flash an arrow.
            _arrowSprite.enabled = IsActive && !_isReleasing && _poseTime >= _raiseDuration
                && _charge >= _minimumCharge && _arrowSprite.sprite != null;
        }

        public void ResetPose()
        {
            RestoreBodyPose();
            if (!IsActive) return;
            
            IsActive = false;
            _arrowSprite.enabled = false;
            _leftHand.ResetGrip();
            _rightHand.ResetGrip();
            _aimRoot.gameObject.SetActive(false);
            _heldItemDisplay.SetBowPoseActive(false);
        }
    }
}
