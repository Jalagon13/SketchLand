using UnityEngine;

namespace SketchLand
{
    /// <summary>Momentum steering and tile bounce shared by flying NPC states.</summary>
    public sealed class FlyingNpcMovement
    {
        private float _recoveryRemaining;
        private float _recoveryDuration;

        public void Tick(float deltaTime)
        {
            _recoveryRemaining = Mathf.Max(0f, _recoveryRemaining - deltaTime);
        }

        public Vector2 Steer(Vector2 velocity, Vector2 desiredVelocity, float strength, float deltaTime)
        {
            float recovery = _recoveryDuration > 0f
                ? Mathf.SmoothStep(0f, 1f, 1f - _recoveryRemaining / _recoveryDuration) : 1f;
            return Vector2.Lerp(velocity, desiredVelocity, 1f - Mathf.Exp(-Mathf.Max(0f, strength) * recovery * deltaTime));
        }

        public bool Bounce(Vector2 incomingVelocity, GridPhysics.CollisionResult collision,
            float damping, out Vector2 reflectedVelocity, float steeringRecoveryDuration = 0f)
        {
            bool hitX = (collision.TouchingLeft && incomingVelocity.x < 0f)
                || (collision.TouchingRight && incomingVelocity.x > 0f);
            bool hitY = (collision.TouchingBottom && incomingVelocity.y < 0f)
                || (collision.TouchingTop && incomingVelocity.y > 0f);
            reflectedVelocity = incomingVelocity;
            if (!hitX && !hitY) return false;

            if (hitX) reflectedVelocity.x = -reflectedVelocity.x;
            if (hitY) reflectedVelocity.y = -reflectedVelocity.y;
            reflectedVelocity *= Mathf.Clamp01(damping);
            _recoveryDuration = Mathf.Max(0f, steeringRecoveryDuration);
            _recoveryRemaining = _recoveryDuration;
            return true;
        }
    }
}
