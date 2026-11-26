using UnityEngine;

namespace SketchLand
{
    public sealed class BatPursueState : State
    {
        private readonly BatRootState _root;
        private readonly ServerCharacter _ctx;
        private float _height, _range, _side, _untilSwoop, _ascentTime;
        private bool _hadTarget;

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
            _hadTarget = _root.TryGetTarget(out _);
            _ascentTime = _root.TakeRecovery() && _hadTarget ? _root.RandomRetreatDuration() : 0f;
            _side = _ctx.transform.position.x > _root.PatrolCenter.x ? -1f : 1f;
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
            bool hasTarget = _root.TryGetTarget(out _);
            if (!hasTarget)
            {
                // Hold the last patrol anchor and save a fresh attack delay for reacquisition.
                if (_hadTarget) _untilSwoop = _root.RandomSwoopInterval();
                _ascentTime = 0f;
            }
            _hadTarget = hasTarget;
            if (!_ctx.IsKnockedBack)
            {
                if (hasTarget) _untilSwoop -= deltaTime;
                _ascentTime = Mathf.Max(0f, _ascentTime - deltaTime);
            }
            Vector2 position = _ctx.transform.position;
            Vector2 anchor = _root.PatrolCenter;
            float targetX = anchor.x + _side * _range;
            if ((_side > 0f && position.x >= targetX - 0.5f) || (_side < 0f && position.x <= targetX + 0.5f))
            {
                _side = -_side;
                targetX = anchor.x + _side * _range;
            }

            float altitudeError = anchor.y + _height - position.y;
            Vector2 desired;
            if (_ascentTime > 0f || altitudeError > 0.75f)
            {
                // Regain altitude first, with a little lateral motion to avoid a vertical hover.
                float horizontal = _ascentTime > 0f ? Mathf.Sign(position.x - anchor.x) : Mathf.Sign(targetX - position.x);
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
