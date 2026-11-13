using System;
using UnityEngine;

namespace SketchLand
{
    #region Root State

    [Serializable]
    public class PlayerRootState : CharacterRootState
    {
        private static readonly int DamagedAnimation = Animator.StringToHash("Base Layer.StickFigure_Damaged");

        public PlayerGroundedState Grounded { get; private set; }
        public PlayerAirborneState Airborne { get; private set; }
        public float MovementSpeed { get; private set; }

        private bool _isSprinting;
        private bool _sprintExhausted;

        // Parameterless constructor for Unity serialization
        public PlayerRootState() : base() { }

        public override CharacterRootState CreateInstance()
        {
            return new PlayerRootState();
        }

        public override void Initialize(ServerCharacter ctx)
        {
            base.Initialize(ctx);
            
            // Construct child states once the context is available
            Grounded = new PlayerGroundedState(null, this, ctx);
            Airborne = new PlayerAirborneState(null, this, ctx);
        }

        protected override State GetInitialState() => Grounded;

        protected override void OnDamaged()
        {
            PlayDamageAnimation(DamagedAnimation);
        }

        protected override void OnEnter()
        {
            MovementSpeed = _ctx.Data.BaseSpeed;
            _ctx.Animator.SetBool("IsSprinting", false);
        }

        protected override void OnExit()
        {
            _ctx.Animator.SetBool("IsSprinting", false);
        }

        protected override void OnFixedUpdate(float fixedDeltaTime)
        {
            var player = Player.Instance;
            
            bool sprintHeld = _ctx.IsOwner && GameInput.Instance.SprintHeld;
            if (!sprintHeld)
                _sprintExhausted = false;

            var stamina = player.Stamina;
            if (sprintHeld && stamina.Current <= 0f)
                _sprintExhausted = true;

            bool moving = Mathf.Abs(_ctx.DesiredDirection.x) > 0.01f
                && !(_ctx.DesiredDirection.x > 0f ? _ctx.CollisionResult.TouchingRight : _ctx.CollisionResult.TouchingLeft);
            _isSprinting = sprintHeld && !_sprintExhausted && moving && !_ctx.IsKnockedBack;
            if (_ctx.IsOwner)
            {
                stamina.Tick(fixedDeltaTime, _isSprinting);
                if (_isSprinting && stamina.Current <= 0f)
                {
                    _sprintExhausted = true;
                    _isSprinting = false;
                }
            }

            float targetSpeed = _isSprinting ? player.SprintSpeed : _ctx.Data.BaseSpeed;
            float blend = 1f - Mathf.Exp(-player.SprintTransitionSharpness * fixedDeltaTime);
            
            MovementSpeed = Mathf.Lerp(MovementSpeed, targetSpeed, blend);
            
            UpdateLocomotionAnimation(ActiveChild == Grounded);
        }

        public void UpdateLocomotionAnimation(bool isGrounded)
        {
            float horizontalSpeedSquared = _ctx.Velocity.x * _ctx.Velocity.x;
            _ctx.Animator.SetFloat("Velocity", horizontalSpeedSquared);
            _ctx.Animator.SetBool("IsSprinting", isGrounded && !_ctx.IsKnockedBack
                && _isSprinting && Mathf.Abs(_ctx.DesiredDirection.x) > 0.01f
                && horizontalSpeedSquared > 0.8f);
        }
    }

    #endregion

    #region Grounded State

    public class PlayerGroundedState : State
    {
        public readonly PlayerIdleState Idle;
        public readonly PlayerMoveState Move;
    
        private readonly ServerCharacter _ctx;
        private Player _player;
    
        public PlayerGroundedState(StateMachine m, State parent, ServerCharacter ctx) : base(m, parent)
        {
            _ctx = ctx;

            Idle = new PlayerIdleState(m, this, _ctx);
            Move = new PlayerMoveState(m, this, _ctx);
        }

        protected override State GetInitialState() => Idle;

        protected override State GetTransition()
        {
            if (_ctx.IsKnockedBack)
            {
                _player.JumpPressed = false;
                return !_ctx.CollisionResult.TouchingBottom || _ctx.Velocity.y > 0f
                    ? (Parent as PlayerRootState).Airborne
                    : null;
            }

            if(_player.JumpPressed)
            {
                _player.JumpPressed = false;
                _ctx.Velocity.y = _player.MinJumpSpeed;
                
                return (Parent as PlayerRootState).Airborne;
            }
        
            if(!_ctx.CollisionResult.TouchingBottom || _ctx.Velocity.y > 0f)
            {
                return (Parent as PlayerRootState).Airborne;
            }
        
            return null;
        }

        protected override void OnEnter()
        {
            // On Grounded Animation set here
            _player = Player.Instance;
            _ctx.ColliderSize = Player.Instance.Character.Data.BodyColliderSize;
            ((PlayerRootState)Parent).UpdateLocomotionAnimation(true);
        }

        protected override void OnExit()
        {
            
        }

        protected override void OnFixedUpdate(float fixedDeltaTime)
        {
            if(_ctx.IsKnockedBack)
            {
                _ctx.ApplyGravity(_player.GravityForce, _player.TerminalVelocity, fixedDeltaTime);
                return;
            }
        
            _ctx.Velocity.y = -0.1f; // To firmly keep the character firmly pressed to the floor
        }
    }

    #endregion

    #region Airborne State

    public class PlayerAirborneState : State
    {
        private readonly ServerCharacter _ctx;
        private Player _player;
        private float _jumpHoldTimer; // Used for keeping track of how long jump has been held.
        private bool _jumpHoldEnded;
        private float _jumpBufferTimer; // Used for keeping track of how long the player can buffer a jump after pressing jump while airborne
        private float _coyoteTimer;
        private bool _coyoteJumpRequested;
        private bool _topTouchedFlag; // Used for checking during this ariborne state if the player's head hit a ceiling once.

        public PlayerAirborneState(StateMachine m, State parent, ServerCharacter ctx) : base(m, parent)
        {
            _ctx = ctx;
        }

        protected override State GetTransition()
        {
            if(_ctx.CollisionResult.TouchingBottom && _ctx.Velocity.y <= 0f)
            {
                if (_jumpBufferTimer > 0f)
                {
                    _player.JumpPressed = true;
                }

                return (Parent as PlayerRootState).Grounded;
            }
            
            return null;
        }

        protected override void OnEnter()
        {
            _player = Player.Instance;
            _ctx.ColliderSize = _player.Character.Data.BodyColliderSize;
            _jumpBufferTimer = 0;
            _coyoteTimer = _ctx.Velocity.y <= 0f ? _player.CoyoteTimeBufferDuration : 0f;
            _coyoteJumpRequested = false;
            _topTouchedFlag = false;
            _ctx.Animator.SetBool("IsAirborne", true);
            ((PlayerRootState)Parent).UpdateLocomotionAnimation(false);

            // On Jump Animation set here
            if (!_ctx.IsKnockedBack && GameInput.Instance.JumpHold && _ctx.Velocity.y > 0)
            {
                _jumpHoldTimer = _player.MaxJumpHoldDuration;
                _jumpHoldEnded = false;
            }
            
            GameInput.Instance.OnJumpPressed += OnJumpPressed;
        }

        protected override void OnExit()
        {
            _jumpHoldTimer = 0;
            _jumpBufferTimer = 0;
            _coyoteTimer = 0;
            _coyoteJumpRequested = false;
            _topTouchedFlag = false;
            _ctx.Animator.SetBool("IsAirborne", false);

            GameInput.Instance.OnJumpPressed -= OnJumpPressed;
        }

        protected override void OnFixedUpdate(float fixedDeltaTime)
        {
            if (_ctx.IsKnockedBack)
            {
                // A hit cancels jump assistance; holding jump must not extend the launch.
                _jumpHoldTimer = 0f;
                _jumpHoldEnded = true;
                _coyoteTimer = 0f;
                _coyoteJumpRequested = false;
                _jumpBufferTimer = 0f;
                _ctx.ApplyGravity(_player.GravityForce, _player.TerminalVelocity, fixedDeltaTime);
                return;
            }

            if (_coyoteTimer > 0f)
            {
                _coyoteTimer -= fixedDeltaTime;
            }

            if (_coyoteJumpRequested)
            {
                _coyoteJumpRequested = false;
                _ctx.Velocity.y = _player.MinJumpSpeed;
                _jumpHoldTimer = GameInput.Instance.JumpHold ? _player.MaxJumpHoldDuration : 0f;
                _jumpHoldEnded = !GameInput.Instance.JumpHold;
            }

            if (_jumpBufferTimer > 0)
            {
                _jumpBufferTimer -= fixedDeltaTime;
            }

            if (_ctx.CollisionResult.TouchingTop && !_topTouchedFlag) // Player bangs his head during jump, set velocity to -0.1 once
            {
                _jumpHoldEnded = true;
                _topTouchedFlag = true;
                _ctx.Velocity.y = -0.1f;
            }

            if (GameInput.Instance.JumpHold && _ctx.Velocity.y > 0 && _jumpHoldTimer > 0 && !_jumpHoldEnded)
            {
                _jumpHoldTimer -= fixedDeltaTime;
                
                // Here we do not set velocity y because the jump value from grounded and swimming are different values so just keep the y the same as what it was set to prior to switching to this state
                // _ctx.Velocity.y = _ctx.Velocity.y;
            }
            else
            {
                _jumpHoldEnded = true;

                _ctx.Velocity.y -= _player.GravityForce * fixedDeltaTime;

                if (_ctx.Velocity.y < _player.TerminalVelocity)
                {
                    _ctx.Velocity.y = _player.TerminalVelocity;
                }
            }
        
            // Allow horizontal movement while airborne
            var currentSpeed = ((PlayerRootState)Parent).MovementSpeed * _player.AirborneMoveSpeedMultiplier;
            _ctx.UpdateHorizontalMovement(_ctx.DesiredDirection.x * currentSpeed, fixedDeltaTime);
        }

        private void OnJumpPressed()
        {
            if (_ctx.IsKnockedBack) return;

            if (_coyoteTimer > 0f)
            {
                _coyoteJumpRequested = true;
                return;
            }

            _jumpBufferTimer = _player.JumpBufferDuration;
        }

    }

    #endregion

    #region Idle State

    public class PlayerIdleState : State
    {
        private readonly ServerCharacter _ctx;

        public PlayerIdleState(StateMachine m, State parent, ServerCharacter ctx) : base(m, parent)
        {
            _ctx = ctx;
        }

        protected override State GetTransition()
        {
            if (!_ctx.CollisionResult.TouchingBottom)
            {
                return ((PlayerGroundedState)Parent).Parent is PlayerRootState root ? root.Airborne : null;
            }

            if (_ctx.Velocity.sqrMagnitude > 0.1f)
            {
                return ((PlayerGroundedState)Parent).Move;
            }
        
            return null;
        }

        protected override void OnEnter()
        {
            if (!_ctx.IsKnockedBack) _ctx.Velocity.y = 0f;
        }

        protected override void OnExit()
        {

        }

        protected override void OnFixedUpdate(float fixedDeltaTime)
        {
            if (_ctx.IsKnockedBack)
                return;

            var currentSpeed = ((PlayerRootState)Parent.Parent).MovementSpeed;
            _ctx.UpdateHorizontalMovement(_ctx.DesiredDirection.x * currentSpeed, fixedDeltaTime);

        }
    }

    #endregion

    #region Move State

    public class PlayerMoveState : State
    {
        private readonly ServerCharacter _ctx;

        public PlayerMoveState(StateMachine m, State parent, ServerCharacter ctx) : base(m, parent)
        {
            _ctx = ctx;
        }

        protected override State GetTransition()
        {
            if (!_ctx.CollisionResult.TouchingBottom)
            {
                return ((PlayerGroundedState)Parent).Parent is PlayerRootState root ? root.Airborne : null;
            }
        
            if (_ctx.Velocity.sqrMagnitude <= 0.1f)
            {
                return ((PlayerGroundedState)Parent).Idle;
            }

            return null; 
        }

        protected override void OnFixedUpdate(float fixedDeltaTime)
        {
            if (_ctx.IsKnockedBack)
                return;

            var currentSpeed = ((PlayerRootState)Parent.Parent).MovementSpeed;
            _ctx.UpdateHorizontalMovement(_ctx.DesiredDirection.x * currentSpeed, fixedDeltaTime);
        }
    }

    #endregion
}
