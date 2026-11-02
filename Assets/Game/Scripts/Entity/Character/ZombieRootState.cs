using UnityEngine;

namespace SketchLand
{
    public class ZombieRootState : CharacterRootState
    {
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
    }

    public class ZombieGroundedState : State
    {
        public readonly ZombieIdleState Idle;
        public readonly ZombieMoveState Move;

        public ZombieGroundedState(StateMachine m, State parent, ServerCharacter ctx) : base(m, parent)
        {
            Idle = new ZombieIdleState(m, this, ctx);
            Move = new ZombieMoveState(m, this, ctx);
        }

        protected override State GetInitialState() => Idle;
    }

    public class ZombieAirborneState : State
    {
        public ZombieAirborneState(StateMachine m, State parent, ServerCharacter ctx) : base(m, parent)
        {
        }
    }

    public class ZombieIdleState : State
    {
        public ZombieIdleState(StateMachine m, State parent, ServerCharacter ctx) : base(m, parent)
        {
        }
    }

    public class ZombieMoveState : State
    {
        public ZombieMoveState(StateMachine m, State parent, ServerCharacter ctx) : base(m, parent)
        {
        }
    }
}
