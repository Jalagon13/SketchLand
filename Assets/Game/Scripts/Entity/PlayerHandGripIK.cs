using UnityEngine;
using UnityEngine.U2D.IK;

namespace SketchLand
{
    // Animator evaluates first. This runs before IKManager2D,
    // whose LateUpdate execution order is -10.
    [DefaultExecutionOrder(-20)]
    public class PlayerHandGripIK : MonoBehaviour
    {
        [SerializeField] private LimbSolver2D _solver;
        [SerializeField] private Transform _gripPoint;
        [SerializeField, Min(0f)] private float _blendDuration = 0.08f;

        public float BlendDuration => _blendDuration;
        public Transform AnimatedTarget => _animatedTarget;

        private Transform _animatedTarget;
        private Transform _blendTarget;
        private float _gripWeight;
        private bool _wantsGrip;
        private bool _isDriving;

        private void Awake()
        {
            _animatedTarget = _solver.GetChain(0).target;
        }

        public void BeginGrip()
        {
            _wantsGrip = true;

            if (_isDriving)
                return;

            _gripWeight = 0f;
            _isDriving = true;

            // Only transitions need an intermediate target. Create it once at
            // runtime so there is no extra target to configure in the prefab.
            if (_blendTarget == null)
            {
                _blendTarget = new GameObject("R_Hand_BlendTarget").transform;
                _blendTarget.SetParent(transform, false);
            }

            _blendTarget.SetPositionAndRotation(_animatedTarget.position, _animatedTarget.rotation);
            _solver.GetChain(0).target = _blendTarget;
        }

        public void ReleaseGrip()
        {
            _wantsGrip = false;
        }

        public void ResetGrip()
        {
            _wantsGrip = false;
            _isDriving = false;
            _gripWeight = 0f;

            if (_solver != null && _animatedTarget != null)
                _solver.GetChain(0).target = _animatedTarget;
        }

        private void LateUpdate()
        {
            if (!_isDriving)
                return;

            float destination = _wantsGrip ? 1f : 0f;

            _gripWeight = _blendDuration <= 0f ? destination : Mathf.MoveTowards(_gripWeight, destination, Time.deltaTime / _blendDuration);

            // Ease the transition without changing its duration.
            float blend = Mathf.SmoothStep(0f, 1f, _gripWeight);

            Vector3 position = Vector3.Lerp(_animatedTarget.position, _gripPoint.position, blend);
            Quaternion quaternion = Quaternion.Slerp(_animatedTarget.rotation, _gripPoint.rotation, blend);
            _blendTarget.SetPositionAndRotation(position, quaternion);

            // Once the transition finishes, follow the actual grip directly.
            _solver.GetChain(0).target = _wantsGrip && _gripWeight >= 1f
                ? _gripPoint
                : _blendTarget;

            if (!_wantsGrip && _gripWeight <= 0f)
                ResetGrip();
        }

        private void OnDisable()
        {
            ResetGrip();
        }

        private void OnDestroy()
        {
            ResetGrip();

            if (_blendTarget != null)
                Destroy(_blendTarget.gameObject);
        }
    }
}
