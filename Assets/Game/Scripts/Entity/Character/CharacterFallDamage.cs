using UnityEngine;

namespace SketchLand
{
    // Per-character fall history, independent of movement and AI states.
    public sealed class CharacterFallDamage
    {
        private bool _trackingFall;
        private float _peakY;

        public void Reset(float height)
        {
            _trackingFall = false;
            _peakY = height;
        }

        public int Track(float previousY, float currentY, bool grounded, float incomingVerticalVelocity,
            float safeDistance, float damagePerExtraTile)
        {
            _peakY = Mathf.Max(_peakY, Mathf.Max(previousY, currentY));
            if (!grounded || incomingVerticalVelocity > 0f)
            {
                _trackingFall = true;
                return 0;
            }

            // Include a fall that starts and lands within a single physics tick.
            bool landed = _trackingFall || currentY < previousY;
            float distance = _peakY - currentY;
            Reset(currentY);
            return landed
                ? Mathf.RoundToInt(Mathf.Max(0f, distance - Mathf.Max(0f, safeDistance)) * Mathf.Max(0f, damagePerExtraTile))
                : 0;
        }
    }
}
