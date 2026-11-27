using UnityEngine;
using UnityEngine.U2D.IK;

namespace SketchLand
{
    // Additive offsets sampled after the Animator and before hand IK.
    [System.Serializable]
    public sealed class SwingBodyPose
    {
        [SerializeField] private Transform _hip;
        [SerializeField] private Transform _spine;
        [SerializeField] private Transform _head;
        [SerializeField] private Transform _freeHand;
        [SerializeField] private Transform _swingShoulder;

        private PlayerAnimationProfile _profile;
        private float Exaggeration => _profile.Swing.Exaggeration * _profile.Intensity;
        private float BodySway => Exaggeration * 0.85f;
        private Transform _arm;
        private Vector3 _armRestPosition, _hipBase, _handBase;
        private Quaternion _hipRotationBase, _spineBase, _headBase;
        private bool _poseApplied;
        private LimbSolver2D _freeHandSolver;

        public void Initialize(Transform arm)
        {
            _arm = arm;
            _armRestPosition = arm.localPosition;
            _freeHandSolver = _freeHand.GetComponentInParent<LimbSolver2D>();
        }

        public void Begin(PlayerAnimationProfile profile)
        {
            _profile = profile;
            _arm.position = _swingShoulder.position;
        }

        public void End()
        {
            RestorePose();
            _arm.localPosition = _armRestPosition;
        }

        // Each part reverses at a different moment. Hips drive the blow,
        // then the shoulders and head catch up.
        private float PoseWeight(float time, float delay) => _profile.Swing.BodyCurve.Evaluate(Mathf.Clamp01(time - delay));

        public void Apply(float time)
        {
            float hips = BodySway * PoseWeight(time, 0f);
            float shoulders = BodySway * PoseWeight(time, 0.025f);
            float head = BodySway * PoseWeight(time, 0.06f);

            float hand = Exaggeration * PoseWeight(time, 0.045f);
            _handBase = _freeHand.localPosition;
            _hipBase = _hip.localPosition;
            _hipRotationBase = _hip.localRotation;
            _spineBase = _spine.localRotation;
            _headBase = _head.localRotation;
            _poseApplied = true;

            // Shift weight between planted feet and sink deeper on the landing.
            if (!_profile.UpperBodyOnly)
                _hip.localPosition += new Vector3(-0.16f * hips, -Mathf.Abs(hips) * (hips > 0 ? 0.08f : 0.15f));
            // Share the backward lean across both torso bones during the wind-up.
            float hipLean = _profile.UpperBodyOnly ? 0f : 22f * Mathf.Max(0f, shoulders);
            if (!_profile.UpperBodyOnly) _hip.localRotation *= Quaternion.Euler(0f, 0f, 4f * hips + hipLean);
            _spine.localRotation *= Quaternion.Euler(0f, 0f, _profile.Swing.TorsoRotation * shoulders - hipLean);
            _head.localRotation *= Quaternion.Euler(0f, 0f, -_profile.Swing.HeadCounterRotation * shoulders + 12f * head);

            _freeHand.position += _hip.parent.TransformVector((Vector3)(_profile.Swing.FreeHandTravel * hand));
            if (_freeHandSolver != null)
            {
                var effector = _freeHandSolver.GetChain(0).effector;
                var elbow = effector.parent;
                var shoulder = elbow.parent;
                float reach = (Vector3.Distance(shoulder.position, elbow.position)
                    + Vector3.Distance(elbow.position, effector.position)) * 0.985f;
                var offset = _freeHand.position - shoulder.position;
                if (offset.sqrMagnitude > reach * reach)
                    _freeHand.position = shoulder.position + offset.normalized * reach;
            }

            // Let the grip arc travel with the posed shoulder at every aim angle. The
            // gameplay pivot stays fixed so the strike zone keeps its origin.
            _arm.position = _swingShoulder.position;
        }

        public void RestorePose()
        {
            if (!_poseApplied) return;
            _hip.SetLocalPositionAndRotation(_hipBase, _hipRotationBase);
            _spine.localRotation = _spineBase;
            _head.localRotation = _headBase;
            _freeHand.localPosition = _handBase;
            _poseApplied = false;
        }
    }
}
