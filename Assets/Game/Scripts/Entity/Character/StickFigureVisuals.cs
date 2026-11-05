using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SketchLand
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class StickFigureVisuals : MonoBehaviour
    {
        [Header("Renderers")]
        [SerializeField] private SpriteRenderer _bodyRenderer;
        [SerializeField] private SpriteRenderer _headFillRenderer;

        [Header("Colors")]
        [Tooltip("Tints the stick figure body and head outline.")]
        [SerializeField, ColorUsage(true)] private Color _bodyColor = Color.white;
        [Tooltip("Tints the head interior. Set alpha to zero for a hollow head.")]
        [SerializeField, ColorUsage(true)] private Color _headFillColor = Color.white;

        public Color BodyColor => _bodyColor;
        public Color HeadFillColor => _headFillColor;

        private volatile bool _colorsDirty;

        private void OnEnable()
        {
            ApplyColors();
#if UNITY_EDITOR
            EditorApplication.update += ApplyPendingColors;
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= ApplyPendingColors;
#endif
        }

        private void OnValidate()
        {
            // Validation can run off the main thread. Apply renderer changes
            // on the next editor or game update instead.
            _colorsDirty = true;
        }

        private void Update()
        {
            ApplyPendingColors();
        }

        private void ApplyPendingColors()
        {
            if (_colorsDirty)
                ApplyColors();
        }

        public void SetBodyColor(Color color)
        {
            _bodyColor = color;
            ApplyColors();
        }

        public void SetHeadFillColor(Color color)
        {
            _headFillColor = color;
            ApplyColors();
        }

        public void SetColors(Color bodyColor, Color headFillColor)
        {
            _bodyColor = bodyColor;
            _headFillColor = headFillColor;
            ApplyColors();
        }

        public void ApplyColors()
        {
            if (_bodyRenderer != null)
                _bodyRenderer.color = _bodyColor;

            if (_headFillRenderer != null)
                _headFillRenderer.color = _headFillColor;

            _colorsDirty = false;
        }
    }
}
