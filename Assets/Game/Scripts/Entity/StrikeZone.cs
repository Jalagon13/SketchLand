using UnityEngine;

namespace SketchLand
{
    // World-space sector against the same axis-aligned bounds used by GridPhysics.
    internal readonly struct StrikeZone
    {
        public readonly Vector2 Origin, Direction;
        public readonly float HalfAngle, Reach;
        private readonly Ray _leftEdge, _rightEdge;

        public StrikeZone(Vector2 origin, Vector2 direction, float halfAngle, float reach)
        {
            Origin = origin;
            Direction = direction;
            HalfAngle = halfAngle;
            Reach = reach;
            _leftEdge = new Ray(origin, Quaternion.Euler(0f, 0f, halfAngle) * direction);
            _rightEdge = new Ray(origin, Quaternion.Euler(0f, 0f, -halfAngle) * direction);
        }

        public bool Intersects(Vector2 center, Vector2 size)
        {
            var bounds = new Bounds(center, (Vector3)size + Vector3.forward);
            Vector2 nearest = (Vector2)bounds.ClosestPoint(Origin) - Origin;
            if (nearest.sqrMagnitude > Reach * Reach) return false;

            // A box may cross either radial edge even if its nearest point is outside the arc.
            return Vector2.Angle(Direction, nearest) <= HalfAngle
                || (bounds.IntersectRay(_leftEdge, out float left) && left <= Reach)
                || (bounds.IntersectRay(_rightEdge, out float right) && right <= Reach);
        }
    }
}
