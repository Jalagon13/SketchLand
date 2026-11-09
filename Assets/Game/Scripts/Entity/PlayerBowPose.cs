using UnityEngine;

namespace SketchLand
{
    // Update aim and targets after the Animator, before hand blending (-20) and IK (-10).
    [DefaultExecutionOrder(-30)]
    public class PlayerBowPose : MonoBehaviour
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

        [Header("Draw Pose")]
        [SerializeField] private Vector2 _bowPosition = new(0.55f, 0.03f);
        [SerializeField] private Vector2 _readyHandPosition = new(0.39f, 0.03f);
        [SerializeField] private Vector2 _drawHandControlPosition = new(0.14f, 0.16f);
        [SerializeField] private Vector2 _drawnHandPosition = new(-0.12f, 0.13f);
        [SerializeField] private AnimationCurve _drawCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField, Min(0.01f)] private float _raiseDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float _releaseDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float _returnDuration = 0.12f;

        private ServerCharacter _character;
        private PlayerHeldItemDisplay _heldItemDisplay;
        private float _charge;
        private float _minimumCharge;
        private float _poseTime;
        private float _releaseTime;
        private float _releaseCharge;
        private bool _isReleasing;
        private bool _isReturning;
        private Vector3 _arrowTailOffset;
        public bool IsActive { get; private set; }
        public Vector2 AimDirection { get; private set; } = Vector2.right;
        public Vector2 LaunchPosition => _arrowRoot.position;

        private void Awake()
        {
            _character = GetComponent<ServerCharacter>();
            _heldItemDisplay = GetComponent<PlayerHeldItemDisplay>();
            _aimRoot.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            ResetPose();
        }

        private void LateUpdate()
        {
            if (!IsActive) return;

            _poseTime += Time.deltaTime;

            if (_isReleasing)
            {
                _releaseTime += Time.deltaTime;

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

        public void BeginDraw(BowItemSO bow, ArrowItemSO arrow)
        {
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
            _arrowRoot.localScale = source != null ? source.transform.parent.localScale : Vector3.one;
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

        public void SetCharge(float charge) => _charge = Mathf.Clamp01(charge);

        public void UpdateAim()
        {
            Vector2 worldAim = WorldManager.MouseWorldPosition - (Vector2)_shoulder.position;
            
            if (worldAim.sqrMagnitude < 0.0001f)
                worldAim = AimDirection;
                
            AimDirection = worldAim.normalized;
            _character.SetFacingOverride(AimDirection.x);
            _aimRoot.position = _shoulder.position;
            
            Vector3 localAim = _aimRoot.parent.InverseTransformVector(AimDirection);
            _aimRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(localAim.y, localAim.x) * Mathf.Rad2Deg);
            
            ApplyPose();
        }

        public void ReleaseDraw()
        {
            _isReleasing = true;
            _releaseTime = 0f;
            _releaseCharge = _charge;
            _arrowSprite.enabled = false;
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
            if (!IsActive) return;
            
            IsActive = false;
            _arrowSprite.enabled = false;
            _leftHand.ResetGrip();
            _rightHand.ResetGrip();
            _aimRoot.gameObject.SetActive(false);
            _heldItemDisplay.SetBowPoseActive(false);
            _character.ClearFacingOverride();
        }
    }
}
