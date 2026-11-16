using UnityEngine;

namespace SketchLand
{
    // Additive offsets sampled after the Animator and before hand IK.
    [System.Serializable]
    internal sealed class SwingPose
    {
        [SerializeField] private Transform _hip;
        [SerializeField] private Transform _spine;
        [SerializeField] private Transform _head;
        [SerializeField] private Transform _freeHand;
        [SerializeField] private Transform _swingShoulder;

        internal const float Exaggeration = 0.765f;
        internal const float WindUp = 0.42f;
        private const float BodySway = Exaggeration * 0.85f;
        private Transform _arm, _armPivot;
        private Vector3 _armRestPosition, _hipBase, _handBase;
        private Quaternion _hipRotationBase, _spineBase, _headBase;
        private bool _poseApplied;
        private float _overheadWeight;

        public void Initialize(Transform arm, Transform pivot)
        {
            _arm = arm;
            _armPivot = pivot;
            _armRestPosition = arm.localPosition;
        }

        public void Begin(Vector2 localDirection)
        {
            _overheadWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.95f, localDirection.y));
        }

        public void End()
        {
            RestorePose();
            _arm.localPosition = _armRestPosition;
        }

        // Each part reverses at a different moment. Hips drive the blow,
        // then the shoulders and head catch up.
        private static float PoseWeight(float time, float delay)
        {
            time -= delay;
            if (time < 0.34f) return Mathf.SmoothStep(0f, 1f, time / 0.34f);
            if (time < 0.39f) return 1f;
            if (time < 0.56f) return Mathf.SmoothStep(1f, -1f, (time - 0.39f) / 0.17f);
            return Mathf.SmoothStep(-1f, 0f, (time - 0.62f) / (0.38f - delay));
        }

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
            _hip.localPosition += new Vector3(-0.16f * hips, -Mathf.Abs(hips) * (hips > 0 ? 0.08f : 0.15f));
            // Share the backward lean across both torso bones during the wind-up.
            float hipLean = 22f * Mathf.Max(0f, shoulders);
            _hip.localRotation *= Quaternion.Euler(0f, 0f, 4f * hips + hipLean);
            _spine.localRotation *= Quaternion.Euler(0f, 0f, 43f * shoulders - hipLean);
            _head.localRotation *= Quaternion.Euler(0f, 0f, -25f * shoulders + 12f * head);

            _freeHand.position += _hip.parent.TransformVector(new Vector3(0.44f * hand, 0.24f * hand));

            // Let the overhead grip arc travel with the posed shoulder. The
            // gameplay pivot stays fixed so the strike zone keeps its origin.
            if (_overheadWeight > 0f)
            {
                float blend = _overheadWeight * Mathf.SmoothStep(0f, 1f, time / WindUp);
                _arm.localPosition = Vector3.Lerp(_armRestPosition,
                    _armPivot.InverseTransformPoint(_swingShoulder.position), blend);
            }
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
