using UnityEngine;

namespace SketchLand
{
    public sealed class BatSwoopState : State
    {
        private readonly BatRootState _root;
        private readonly ServerCharacter _ctx;
        private Vector2 _direction;
        private float _elapsed;

        public BatSwoopState(BatRootState root, ServerCharacter ctx) : base(null, root)
        {
            _root = root;
            _ctx = ctx;
        }

        protected override void OnEnter()
        {
            _elapsed = 0f;
            _direction = _root.TryGetTarget(out var player)
                ? ((Vector2)player.transform.position - (Vector2)_ctx.transform.position).normalized
                : Vector2.down;
            if (_direction.sqrMagnitude < 0.01f) _direction = Vector2.down;
        }

        protected override State GetTransition()
        {
            if (!_ctx.IsServer) return null;
            if (_root.IsLeaving || !_root.TryGetTarget(out var player) || _elapsed >= _root.MaximumSwoopDuration)
                return _root.Pursue;
            // Passing the player's former location must not cancel an attack on a moving target.
            Vector2 toPlayer = (Vector2)player.transform.position - (Vector2)_ctx.transform.position;
            if (_elapsed > 0.1f && Vector2.Dot(toPlayer, _direction) <= 0f)
                return _root.Pursue;
            return null;
        }

        protected override void OnFixedUpdate(float deltaTime)
        {
            if (!_ctx.IsServer || _root.IsLeaving) return;
            _elapsed += deltaTime;
            if (!_root.TryGetTarget(out var player)) return;
            Vector2 towardPlayer = ((Vector2)player.transform.position - (Vector2)_ctx.transform.position).normalized;
            _direction = Vector2.Lerp(_direction, towardPlayer, 1f - Mathf.Exp(-_root.SwoopSteering * deltaTime)).normalized;
            _root.Steer(_direction * _root.FlightSpeed, deltaTime);
        }

        protected override void OnExit() => _root.EndSwoop();
    }
}
