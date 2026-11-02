using System;
using UnityEngine;

namespace SketchLand
{
    #region Root State

    [Serializable]
    public class DeerRootState : CharacterRootState
    {
        [Header("AI Settings")]
        [SerializeField] private float _minWanderTime;
        [SerializeField] private float _maxWanderTime;
        [SerializeField] private float _minIdleTime;
        [SerializeField] private float _maxIdleTime;

        [field: Header("Gravity Settings")]
        [field: SerializeField] public float GravityForce { get; private set; }
        [field: SerializeField] public float TerminalVelocity { get; private set; }

        [field: Header("Jump Settings")]
        [field: SerializeField] public float MinJumpSpeed { get; private set; }
        [field: SerializeField] public float AirborneMoveSpeedMultiplier { get; private set; }

        public DeerGroundedState Grounded { get; private set; }
        public DeerAirborneState Airborne { get; private set; }
        public bool IsWandering => _wanderTimer > 0f;

        private float _wanderTimer;
        private float _wanderDirection;

        public DeerRootState() : base() { }

        public override void Initialize(ServerCharacter ctx)
        {
            base.Initialize(ctx);

            // Construct child states once the context is available
            Grounded = new DeerGroundedState(null, this, ctx);
            Airborne = new DeerAirborneState(null, this, ctx);
        }

        protected override State GetInitialState() => Grounded;

        protected override void OnEnter()
        {
            BeginWander();
        }

        protected override void OnExit()
        {
            _ctx.DesiredDirection = Vector2.zero;
        }

        protected override void OnFixedUpdate(float fixedDeltaTime)
        {
            if (_ctx.IsKnockedBack) return;

            _wanderTimer = Mathf.Max(0f, _wanderTimer - fixedDeltaTime);
            _ctx.DesiredDirection = IsWandering ? Vector2.right * _wanderDirection : Vector2.zero;
        }

        public float GetRandomWanderTime()
        {
            return UnityEngine.Random.Range(_minWanderTime, _maxWanderTime);
        }

        public float GetRandomIdleTime()
        {
            return UnityEngine.Random.Range(_minIdleTime, _maxIdleTime);
        }

        public void BeginWander()
        {
            _wanderDirection = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            _wanderTimer = GetRandomWanderTime();
            _ctx.DesiredDirection = IsWandering ? Vector2.right * _wanderDirection : Vector2.zero;
        }

        public void UpdateHorizontalMovement(float fixedDeltaTime, float speedMultiplier = 1f)
        {
            if (_ctx.IsKnockedBack) return;

            _ctx.DesiredDirection = IsWandering ? Vector2.right * _wanderDirection : Vector2.zero;
            var currentSpeed = _ctx.Data.BaseSpeed * speedMultiplier;
            _ctx.Velocity.x = Mathf.Lerp(_ctx.Velocity.x, _ctx.DesiredDirection.x * currentSpeed, fixedDeltaTime * _ctx.Data.BaseTurnSharpness);

            if (!IsWandering && Mathf.Abs(_ctx.Velocity.x) <= 0.01f)
            {
                _ctx.Velocity.x = 0f;
            }
        }
    }

    #endregion

    #region Grounded State
    
    public class DeerGroundedState : State
    {
        public readonly DeerIdleState Idle;
        public readonly DeerMoveState Move;

        private readonly ServerCharacter _ctx;

        public DeerGroundedState(StateMachine m, State parent, ServerCharacter ctx) : base(m, parent)
        {
            _ctx = ctx;

            Idle = new DeerIdleState(m, this, _ctx);
            Move = new DeerMoveState(m, this, _ctx);
        }

        protected override State GetInitialState() => ((DeerRootState)Parent).IsWandering ? Move : Idle;

        protected override State GetTransition()
        {
            var root = (DeerRootState)Parent;

            if (!_ctx.CollisionResult.TouchingBottom || _ctx.Velocity.y > 0f)
            {
                return root.Airborne;
            }

            if (!_ctx.IsKnockedBack && root.IsWandering && ((_ctx.DesiredDirection.x < 0f && _ctx.CollisionResult.TouchingLeft) || (_ctx.DesiredDirection.x > 0f && _ctx.CollisionResult.TouchingRight)))
            {
                _ctx.Velocity.y = root.MinJumpSpeed;
                return root.Airborne;
            }

            return null;
        }

        protected override void OnFixedUpdate(float fixedDeltaTime)
        {
            if (_ctx.IsKnockedBack) return;

            _ctx.Velocity.y = -0.1f; // Keep the deer pressed to the floor
        }
    }

    #endregion

    #region Airborne State
    
    public class DeerAirborneState : State
    {
        private readonly ServerCharacter _ctx;

        public DeerAirborneState(StateMachine m, State parent, ServerCharacter ctx) : base(m, parent)
        {
            _ctx = ctx;
        }

        protected override State GetTransition()
        {
            if (_ctx.CollisionResult.TouchingBottom && _ctx.Velocity.y <= 0f)
            {
                return ((DeerRootState)Parent).Grounded;
            }

            return null;
        }

        protected override void OnFixedUpdate(float fixedDeltaTime)
        {
            var root = (DeerRootState)Parent;

            if (_ctx.IsKnockedBack)
            {
                _ctx.KnockbackVelocity.y = Mathf.Max(_ctx.KnockbackVelocity.y - root.GravityForce * fixedDeltaTime, root.TerminalVelocity);
                return;
            }

            if (_ctx.CollisionResult.TouchingTop && _ctx.Velocity.y > 0f)
            {
                _ctx.Velocity.y = 0f;
            }

            _ctx.Velocity.y = Mathf.Max(_ctx.Velocity.y - root.GravityForce * fixedDeltaTime, root.TerminalVelocity);
            root.UpdateHorizontalMovement(fixedDeltaTime, root.AirborneMoveSpeedMultiplier);
        }
    }

    #endregion

    #region Idle State

    public class DeerIdleState : State
    {
        private readonly ServerCharacter _ctx;
        private float _idleTimer;

        public DeerIdleState(StateMachine m, State parent, ServerCharacter ctx) : base(m, parent)
        {
            _ctx = ctx;
        }

        protected override State GetTransition()
        {
            if (!_ctx.IsKnockedBack && _ctx.Velocity.x == 0f && _idleTimer <= 0f)
            {
                ((DeerRootState)Parent.Parent).BeginWander();
                return ((DeerGroundedState)Parent).Move;
            }

            return null;
        }

        protected override void OnEnter()
        {
            _idleTimer = ((DeerRootState)Parent.Parent).GetRandomIdleTime();
        }

        protected override void OnFixedUpdate(float fixedDeltaTime)
        {
            if (_ctx.IsKnockedBack) return;

            ((DeerRootState)Parent.Parent).UpdateHorizontalMovement(fixedDeltaTime);

            if (_ctx.Velocity.x == 0f)
            {
                _idleTimer -= fixedDeltaTime;
            }
        }
    }

    #endregion

    #region Move State

    public class DeerMoveState : State
    {
        private readonly ServerCharacter _ctx;

        public DeerMoveState(StateMachine m, State parent, ServerCharacter ctx) : base(m, parent)
        {
            _ctx = ctx;
        }

        protected override State GetTransition()
        {
            if (!((DeerRootState)Parent.Parent).IsWandering)
            {
                return ((DeerGroundedState)Parent).Idle;
            }

            return null;
        }

        protected override void OnFixedUpdate(float fixedDeltaTime)
        {
            ((DeerRootState)Parent.Parent).UpdateHorizontalMovement(fixedDeltaTime);
        }
    }

    #endregion
}
