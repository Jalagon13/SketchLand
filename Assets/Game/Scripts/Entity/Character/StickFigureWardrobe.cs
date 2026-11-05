using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SketchLand
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class StickFigureWardrobe : MonoBehaviour
    {
        [Header("Attachment Points")]
        [Tooltip("The Head visual attachment point on this rig.")]
        [SerializeField] private VisualAttachmentPoint _headPoint;
        [SerializeField] private WearableVisualSO _headVisual;
        public WearableVisualSO Visual => _headVisual;

        private VisualAttachmentPoint _appliedHeadPoint;
        private Sprite _appliedHeadPiece;
        private volatile bool _visualDirty;

        private void OnEnable()
        {
            ApplyVisual();
#if UNITY_EDITOR
            EditorApplication.update += ApplyPendingVisual;
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= ApplyPendingVisual;
#endif
        }

        private void OnValidate()
        {
            _visualDirty = true;
        }

        private void Update()
        {
            ApplyPendingVisual();
        }

        private Sprite GetHeadPiece()
        {
            if (_headPoint == null || _headPoint.Id != VisualAttachmentPointId.Head)
                return null;

            return _headVisual != null ? _headVisual.HeadPiece : null;
        }

        private void ApplyPendingVisual()
        {
            // Also refresh when the sprite on the assigned asset changes.
            if (_visualDirty || _headPoint != _appliedHeadPoint || GetHeadPiece() != _appliedHeadPiece)
                ApplyVisual();
        }

        public void EquipVisual(WearableVisualSO visual)
        {
            _headVisual = visual;
            ApplyVisual();
        }

        public void ClearVisual()
        {
            EquipVisual(null);
        }

        public void ApplyVisual()
        {
            if (_appliedHeadPoint != null && _appliedHeadPoint != _headPoint)
                _appliedHeadPoint.SetSprite(null);

            Sprite headPiece = GetHeadPiece();
            if (_headPoint != null)
                _headPoint.SetSprite(headPiece);

            _appliedHeadPoint = _headPoint;
            _appliedHeadPiece = headPiece;
            _visualDirty = false;
        }
    }
}
