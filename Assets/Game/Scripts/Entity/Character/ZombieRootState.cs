using System;
using UnityEngine;

namespace SketchLand
{
    #region Root State

    [Serializable]
    public class ZombieRootState : CharacterRootState
    {
        private static readonly int DamagedAnimation = Animator.StringToHash("Base Layer.Zombie_Damaged");
        private static readonly int IsAirborne = Animator.StringToHash("IsAirborne");

        [field: Header("Gravity Settings")]
        [field: SerializeField] public float GravityForce { get; private set; }
        [field: SerializeField] public float TerminalVelocity { get; private set; }

        [field: Header("Jump Settings")]
        [field: SerializeField] public float MinJumpSpeed { get; private set; }
        [field: SerializeField] public float AirborneMoveSpeedMultiplier { get; private set; }

        public ZombieGroundedState Grounded { get; private set; }
        public ZombieAirborneState Airborne { get; private set; }

        public ZombieRootState() : base() { }

        public override CharacterRootState CreateInstance()
        {
            return new ZombieRootState
            {
                GravityForce = GravityForce,
                TerminalVelocity = TerminalVelocity,
                MinJumpSpeed = MinJumpSpeed,
                AirborneMoveSpeedMultiplier = AirborneMoveSpeedMultiplier
            };
        }

        public override void Initialize(ServerCharacter ctx)
        {
            base.Initialize(ctx);

            // Construct child states once the context is available
            Grounded = new ZombieGroundedState(null, this, ctx);
            Airborne = new ZombieAirborneState(null, this, ctx);
        }

        protected override State GetInitialState() => Grounded;

        protected override void OnDamaged()
        {
            PlayDamageAnimation(DamagedAnimation);
        }

        protected override void OnEnter()
        {
            SetAirborneAnimation(false);
            UpdatePursuitDirection();
        }

        public void SetAirborneAnimation(bool airborne)
        {
            if (_ctx.Animator != null)
                _ctx.Animator.SetBool(IsAirborne, airborne);
        }

        protected override void OnExit()
        {
            _ctx.DesiredDirection = Vector2.zero;
        }

        protected override void OnUpdate(float deltaTime)
        {
            UpdatePursuitDirection();
        }

        public void UpdateHorizontalMovement(float fixedDeltaTime, float speedMultiplier = 1f)
        {
            if (_ctx.IsKnockedBack) return;

            UpdatePursuitDirection();
            var currentSpeed = _ctx.Data.BaseSpeed * speedMultiplier;
            _ctx.UpdateHorizontalMovement(_ctx.DesiredDirection.x * currentSpeed, fixedDeltaTime);

            if (_ctx.DesiredDirection.x == 0f && Mathf.Abs(_ctx.Velocity.x) <= 0.01f)
            {
                _ctx.Velocity.x = 0f;
            }
        }

        private void UpdatePursuitDirection()
        {
            var player = Player.Instance;

            if (player == null || player.Character == null)
            {
                _ctx.DesiredDirection = Vector2.zero;
                return;
            }

            float distance = player.Character.transform.position.x - _ctx.transform.position.x;
            _ctx.DesiredDirection = Mathf.Abs(distance) > 0.1f ? Vector2.right * Mathf.Sign(distance) : Vector2.zero;
        }
    }

    #endregion

    #region Grounded State

    public class ZombieGroundedState : State
    {
        public readonly ZombieIdleState Idle;
        public readonly ZombieMoveState Move;

        private readonly ServerCharacter _ctx;

        public ZombieGroundedState(StateMachine m, State parent, ServerCharacter ctx) : base(m, parent)
        {
            _ctx = ctx;

            Idle = new ZombieIdleState(m, this, ctx);
            Move = new ZombieMoveState(m, this, ctx);
        }

        protected override State GetInitialState() => Idle;

        protected override State GetTransition()
        {
            var root = (ZombieRootState)Parent;

            if (!_ctx.CollisionResult.TouchingBottom || _ctx.Velocity.y > 0f)
            {
                return root.Airborne;
            }

            if (!_ctx.IsKnockedBack && ((_ctx.DesiredDirection.x < 0f && _ctx.CollisionResult.TouchingLeft) || (_ctx.DesiredDirection.x > 0f && _ctx.CollisionResult.TouchingRight)))
            {
                _ctx.Velocity.y = root.MinJumpSpeed;
                return root.Airborne;
            }

            return null;
        }

        protected override void OnFixedUpdate(float fixedDeltaTime)
        {
            if (_ctx.IsKnockedBack)
            {
                var root = (ZombieRootState)Parent;
                _ctx.ApplyGravity(root.GravityForce, root.TerminalVelocity, fixedDeltaTime);
                return;
            }

            _ctx.Velocity.y = -0.1f; // Keep the zombie pressed to the floor
        }
    }

    #endregion

    #region Airborne State

    public class ZombieAirborneState : State
    {
        private readonly ServerCharacter _ctx;

        public ZombieAirborneState(StateMachine m, State parent, ServerCharacter ctx) : base(m, parent)
        {
            _ctx = ctx;
        }

        protected override void OnEnter()
        {
            ((ZombieRootState)Parent).SetAirborneAnimation(true);
        }

        protected override void OnExit()
        {
            ((ZombieRootState)Parent).SetAirborneAnimation(false);
        }

        protected override State GetTransition()
        {
            if (_ctx.CollisionResult.TouchingBottom && _ctx.Velocity.y <= 0f)
            {
                return ((ZombieRootState)Parent).Grounded;
            }

            return null;
        }

        protected override void OnFixedUpdate(float fixedDeltaTime)
        {
            var root = (ZombieRootState)Parent;

            if (_ctx.CollisionResult.TouchingTop && _ctx.Velocity.y > 0f)
            {
                _ctx.Velocity.y = 0f;
            }

            _ctx.ApplyGravity(root.GravityForce, root.TerminalVelocity, fixedDeltaTime);
            root.UpdateHorizontalMovement(fixedDeltaTime, root.AirborneMoveSpeedMultiplier);
        }
    }

    #endregion

    #region Idle State

    public class ZombieIdleState : State
    {
        private readonly ServerCharacter _ctx;

        public ZombieIdleState(StateMachine m, State parent, ServerCharacter ctx) : base(m, parent)
        {
            _ctx = ctx;
        }

        protected override State GetTransition()
        {
            if (_ctx.DesiredDirection.x != 0f)
            {
                return ((ZombieGroundedState)Parent).Move;
            }

            return null;
        }

        protected override void OnFixedUpdate(float fixedDeltaTime)
        {
            ((ZombieRootState)Parent.Parent).UpdateHorizontalMovement(fixedDeltaTime);
        }
    }

    #endregion

    #region Move State

    public class ZombieMoveState : State
    {
        private readonly ServerCharacter _ctx;

        public ZombieMoveState(StateMachine m, State parent, ServerCharacter ctx) : base(m, parent)
        {
            _ctx = ctx;
        }

        protected override State GetTransition()
        {
            if (_ctx.DesiredDirection.x == 0f)
            {
                return ((ZombieGroundedState)Parent).Idle;
            }

            return null;
        }

        protected override void OnFixedUpdate(float fixedDeltaTime)
        {
            ((ZombieRootState)Parent.Parent).UpdateHorizontalMovement(fixedDeltaTime);
        }
    }

    #endregion
}
