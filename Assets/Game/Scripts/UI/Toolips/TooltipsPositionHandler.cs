using UnityEngine;
using UnityEngine.InputSystem;

namespace SketchLand
{
    public class TooltipsPositionHandler : MonoBehaviour
    {
        public RectTransform Canvas;
        public RectTransform Layout;

        private const float CursorGap = 6f;

        private void LateUpdate()
        {
            if (Mouse.current == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Canvas,
                Mouse.current.position.ReadValue(), null, out var cursor)) return;

            Rect bounds = Canvas.rect;
            Vector2 size = Layout.rect.size;
            float left = cursor.x + CursorGap;
            float top = cursor.y - CursorGap;
            if (left + size.x > bounds.xMax) left = cursor.x - CursorGap - size.x;
            if (top - size.y < bounds.yMin) top = cursor.y + CursorGap + size.y;

            left = Mathf.Clamp(left, bounds.xMin, Mathf.Max(bounds.xMin, bounds.xMax - size.x));
            top = Mathf.Clamp(top, Mathf.Min(bounds.yMin + size.y, bounds.yMax), bounds.yMax);
            Vector2 pivot = new Vector2(left + size.x * Layout.pivot.x, top - size.y * (1f - Layout.pivot.y));
            Layout.position = Canvas.TransformPoint(pivot);
        }
    }
}
