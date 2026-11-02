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

        public DeerRootState() : base() { }

        public override void Initialize(ServerCharacter ctx)
        {
            base.Initialize(ctx);

            // Construct child states once the context is available
            Grounded = new DeerGroundedState(null, this, ctx);
            Airborne = new DeerAirborneState(null, this, ctx);
        }
        
        public float GetRandomWanderTime()
        {
            return default;
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
        
        
    }

    #endregion

    #region Idle State

    public class DeerIdleState : State
    {
        private readonly ServerCharacter _ctx;

        public DeerIdleState(StateMachine m, State parent, ServerCharacter ctx) : base(m, parent)
        {
            _ctx = ctx;
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
    }

    #endregion
}
