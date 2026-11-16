using UnityEngine;

namespace SketchLand
{
    internal sealed class SwingSlashEffect
    {
        private readonly Transform _armPivot;
        private readonly SpriteRenderer _sprite;
        private LineRenderer _slashInk, _slashFill;

        public SwingSlashEffect(Transform pivot, SpriteRenderer sprite)
        {
            _armPivot = pivot;
            _sprite = sprite;
        }

        public void Hide()
        {
            if (_slashInk != null) _slashInk.enabled = _slashFill.enabled = false;
        }

        public void Draw(float travel, float center, float halfArc, float reach)
        {
            float head = Mathf.Min(travel, 1f);
            float tail = Mathf.Max(0f, travel - 0.82f);
            float span = head - tail;
            if (span < 0.001f)
            {
                Hide();
                return;
            }

            if (_slashInk == null)
            {
                _slashInk = CreateSlashStroke("Slash ink", new Color(0.14f, 0.10f, 0.08f), -2);
                _slashFill = CreateSlashStroke("Slash fill", new Color(1f, 0.91f, 0.66f), -1);
            }

            _slashInk.enabled = _slashFill.enabled = true;
            float width = reach * 0.31f * Mathf.Sqrt(span / 0.82f);
            _slashInk.widthMultiplier = width;
            _slashFill.widthMultiplier = width * 0.77f;
            for (int i = 0; i < _slashInk.positionCount; i++)
            {
                float t = i / (float)(_slashInk.positionCount - 1);
                float sweep = Mathf.Lerp(tail, head, t);
                float radians = (center + halfArc - 2f * halfArc * sweep) * Mathf.Deg2Rad;
                Vector3 radial = _armPivot.parent.TransformVector(new Vector3(Mathf.Cos(radians), Mathf.Sin(radians))).normalized;
                // Pull the trailing tip inward so the slash hooks around the swing.
                Vector3 point = _armPivot.position + radial * (reach * (0.98f - 0.28f * (1f - t) * span / 0.82f));
                _slashInk.SetPosition(i, point);
                _slashFill.SetPosition(i, point);
            }
        }

        private LineRenderer CreateSlashStroke(string strokeName, Color color, int orderOffset)
        {
            var sprite = _sprite;
            var stroke = new GameObject(strokeName);
            stroke.layer = sprite.gameObject.layer;
            stroke.transform.SetParent(_armPivot, false);
            var line = stroke.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.sharedMaterial = sprite.sharedMaterial;
            line.sortingLayerID = sprite.sortingLayerID;
            line.sortingOrder = sprite.sortingOrder + orderOffset;
            line.widthCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 1f),
                new Keyframe(0.66f, 1f), new Keyframe(1f, 0f, -3f, 0f));
            line.startColor = line.endColor = color;
            line.positionCount = 28;
            return line;
        }
    }
}
