using System;
using UnityEngine;

namespace SketchLand
{
    #region Root State

    [Serializable]
    public class PlayerRootState : CharacterRootState
    {
        public PlayerGroundedState Grounded { get; private set; }
        public PlayerAirborneState Airborne { get; private set; }

        // Parameterless constructor for Unity serialization
        public PlayerRootState() : base() { }

        public override void Initialize(ServerCharacter ctx)
        {
            base.Initialize(ctx);
            
            // Construct child states once the context is available
            Grounded = new PlayerGroundedState(null, this, ctx);
            Airborne = new PlayerAirborneState(null, this, ctx);
        }

        protected override State GetInitialState() => Grounded;
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
            if(_player.JumpPressed)
            {
                _player.JumpPressed = false;
                _ctx.Velocity.y = _player.MinJumpSpeed;
                
                return (Parent as PlayerRootState).Airborne;
            }
        
            if(!_ctx.CollisionResult.TouchingBottom)
            {
                return (Parent as PlayerRootState).Airborne;
            }
        
            return null;
        }

        protected override void OnEnter()
        {
            // On Grounded Animation set here
            _player = Player.Instance;
            _ctx.ColliderSize = Player.Instance.WalkingBoxColliderSize;
        }

        protected override void OnExit()
        {
            
        }

        protected override void OnFixedUpdate(float fixedDeltaTime)
        {
            if(_ctx.IsKnockedBack)
            {
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
            _ctx.ColliderSize = _player.WalkingBoxColliderSize;
            _jumpBufferTimer = 0;
            _coyoteTimer = _ctx.Velocity.y <= 0f ? _player.CoyoteTimeBufferDuration : 0f;
            _coyoteJumpRequested = false;
            _topTouchedFlag = false;

            // On Jump Animation set here
            if (GameInput.Instance.JumpHold && _ctx.Velocity.y > 0)
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

            GameInput.Instance.OnJumpPressed -= OnJumpPressed;
        }

        protected override void OnFixedUpdate(float fixedDeltaTime)
        {
            if (_ctx.IsKnockedBack)
            {
                _ctx.KnockbackVelocity.y -= _player.GravityForce * fixedDeltaTime;

                if (_ctx.KnockbackVelocity.y < _player.TerminalVelocity)
                {
                    _ctx.KnockbackVelocity.y = _player.TerminalVelocity;
                }

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
            var currentSpeed = _ctx.Data.BaseSpeed * _player.AirborneMoveSpeedMultiplier;
            _ctx.Velocity.x = Mathf.Lerp(_ctx.Velocity.x, _ctx.DesiredDirection.x * currentSpeed, fixedDeltaTime * _ctx.Data.BaseTurnSharpness);
        }

        private void OnJumpPressed()
        {
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
            _ctx.Velocity.y = 0f;
        }

        protected override void OnExit()
        {

        }

        protected override void OnFixedUpdate(float fixedDeltaTime)
        {
            if (_ctx.IsKnockedBack)
                return;

            var currentSpeed = _ctx.Data.BaseSpeed; // For player, only allow speed buffs to change swim speed
            _ctx.Velocity = Vector2.Lerp(_ctx.Velocity, _ctx.DesiredDirection * currentSpeed, fixedDeltaTime * _ctx.Data.BaseTurnSharpness);

            _ctx.Animator.SetFloat("Velocity", _ctx.Velocity.sqrMagnitude);
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

            var currentSpeed = _ctx.Data.BaseSpeed; // For player, only allow speed buffs to change swim speed
            _ctx.Velocity = Vector2.Lerp(_ctx.Velocity, _ctx.DesiredDirection * currentSpeed, fixedDeltaTime * _ctx.Data.BaseTurnSharpness);
            _ctx.Animator.SetFloat("Velocity", _ctx.Velocity.sqrMagnitude);
        }
    }

    #endregion
}
