using UnityEngine;

namespace SketchLand
{
    [DefaultExecutionOrder(0)]
    public class HeldItemMount : MonoBehaviour
    {
        [SerializeField] private Transform _forearm;

        [SerializeField, Range(0f, 1f)]
        private float _rotationFollow = 1f;

        [SerializeField] private float _idleForearmAngle;

        private Quaternion _originalLocalRotation;

        private void Awake()
        {
            _originalLocalRotation = transform.localRotation;
        }

        private void LateUpdate()
        {
            if (_forearm == null || transform.parent == null)
                return;

            float currentAngle = GetForearmAngle();
            float angleChange = Mathf.DeltaAngle(_idleForearmAngle, currentAngle);

            transform.localRotation = Quaternion.AngleAxis(angleChange * _rotationFollow, Vector3.forward) * _originalLocalRotation;
        }

        private float GetForearmAngle()
        {
            // Express the forearm direction in the hand's space.
            // This also accounts for the character facing left.
            Vector3 worldDirection = _forearm.TransformVector(Vector3.right);

            Vector3 localDirection = transform.parent.InverseTransformVector(worldDirection);

            return Mathf.Atan2(localDirection.y, localDirection.x) * Mathf.Rad2Deg;
        }

        [ContextMenu("Calibrate Idle Forearm Angle")]
        private void CalibrateIdleForearmAngle()
        {
            if (_forearm == null || transform.parent == null)
                return;

            _idleForearmAngle = GetForearmAngle();
        }
    }
}