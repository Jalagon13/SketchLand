using UnityEngine;

namespace SketchLand
{
    public sealed class BatPursueState : State
    {
        private readonly BatRootState _root;
        private readonly ServerCharacter _ctx;
        private float _height, _range, _side, _untilSwoop, _ascentTime;

        public BatPursueState(BatRootState root, ServerCharacter ctx) : base(null, root)
        {
            _root = root;
            _ctx = ctx;
        }

        protected override void OnEnter()
        {
            _height = _root.RandomCirclingHeight();
            _range = _root.RandomCirclingRange();
            _untilSwoop = _root.RandomSwoopInterval();
            _ascentTime = _root.TakeRecovery() ? _root.RandomRetreatDuration() : 0f;
            _side = _root.TryGetTarget(out var player) && _ctx.transform.position.x > player.transform.position.x ? -1f : 1f;
        }

        protected override State GetTransition()
        {
            if (!_ctx.IsServer || _root.IsLeaving || _ctx.IsKnockedBack || _ascentTime > 0f || _untilSwoop > 0f) return null;
            if (_root.TryGetTarget(out var player) && _ctx.transform.position.y >= player.transform.position.y + _height - 0.75f)
                return _root.Swoop;
            return null;
        }

        protected override void OnFixedUpdate(float deltaTime)
        {
            if (!_ctx.IsServer || _root.IsLeaving) return;
            if (!_root.TryGetTarget(out var player))
            {
                _root.Steer(Vector2.zero, deltaTime);
                return;
            }
            if (_ctx.IsKnockedBack) return;
            _untilSwoop -= deltaTime;
            _ascentTime = Mathf.Max(0f, _ascentTime - deltaTime);
            Vector2 position = _ctx.transform.position;
            Vector2 playerPosition = player.transform.position;
            float targetX = playerPosition.x + _side * _range;
            if ((_side > 0f && position.x >= targetX - 0.5f) || (_side < 0f && position.x <= targetX + 0.5f))
            {
                _side = -_side;
                targetX = playerPosition.x + _side * _range;
            }

            float altitudeError = playerPosition.y + _height - position.y;
            Vector2 desired;
            if (_ascentTime > 0f || altitudeError > 0.75f)
            {
                // Regain altitude first, with a little lateral motion to avoid a vertical hover.
                float horizontal = _ascentTime > 0f ? Mathf.Sign(position.x - playerPosition.x) : Mathf.Sign(targetX - position.x);
                desired = new Vector2(horizontal * 0.35f, 1f).normalized * _root.FlightSpeed;
            }
            else
            {
                desired = new Vector2(Mathf.Sign(targetX - position.x) * _root.FlightSpeed, altitudeError * 2f);
                desired = Vector2.ClampMagnitude(desired, _root.FlightSpeed);
            }
            _root.Steer(desired, deltaTime);
        }
    }
}
