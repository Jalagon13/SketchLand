using Sirenix.OdinInspector;
using UnityEngine;

namespace SketchLand
{
    [RequireComponent(typeof(CharacterHealth), typeof(DamageReceiver))]
    public class ServerCharacter : Entity
    {
        [Header("AI State Machine")]
        [SerializeReference]
        public CharacterRootState RootState;

        [SerializeField] private CharacterSO _data;
        public CharacterSO Data => _data;
        
        [SerializeField] private bool _debugStateOn;

        [Header("Visuals")]
        [SerializeField] private Animator _animator;
        public Animator Animator => _animator;

        [HideInInspector] public Vector2 DesiredDirection;
        [HideInInspector] public Vector2 KnockbackVelocity;
        public bool IsKnockedBack => KnockbackVelocity.sqrMagnitude > KnockbackThreshold * KnockbackThreshold;

        public StateMachine Machine { get; private set; }
        
        public CharacterHealth Health { get; private set; }
        public CharacterStats Stats { get; private set; }
        public DamageReceiver DamageReceiver { get; private set; }

        private const float KnockbackThreshold = 1.5f;
        private const float KnockbackDecay = 2f;

        private void Awake()
        {
            Stats = new(_data);
            Health = GetComponent<CharacterHealth>();
            DamageReceiver = GetComponent<DamageReceiver>();

            ColliderSize = _data.BodyColliderSize;
            DesiredDirection = Vector2.zero;
            Velocity = Vector2.zero;
        }

        public override void OnNetworkSpawn()
        {
            State stateToRun = null;

            if (RootState != null)
            {
                RootState.Initialize(this);
                stateToRun = RootState;
            }

            if (stateToRun == null) return;

            Machine = new StateMachineBuilder(stateToRun).Build(_debugStateOn);
            Machine.Start();
        }

        private void OnEnable() 
        {
            if (EntityManager.Instance != null)
            {
                EntityManager.Instance.Register(this);
            }
        }

        private void OnDisable()
        {
            if (EntityManager.Instance != null)
            {
                EntityManager.Instance.UnRegister(this);
            }
        }

        private void Update()
        {
            if (!WorldManager.Instance.IsWorldReady) return;
            if (Machine == null) return;

            Machine.UpdateTick(Time.deltaTime);
            Stats.TickBuffs(Time.deltaTime);
        }

        public override void FixedTick(float fixedDeltaTime)
        {
            if (!WorldManager.Instance.IsWorldReady) return;
            if (Machine == null) return;

            if (IsKnockedBack)
            {
                Velocity = KnockbackVelocity;
                DesiredDirection = Vector2.zero;
            }

            Machine.FixedTick(fixedDeltaTime);

            // State may apply gravity, drag, etc. to KnockbackVelocity.
            // Then use the resulting value as final movement.
            if (IsKnockedBack)
            {
                Velocity = KnockbackVelocity;
                KnockbackVelocity = Vector2.Lerp(KnockbackVelocity, Vector2.zero, KnockbackDecay * fixedDeltaTime);

                if (!IsKnockedBack) KnockbackVelocity = Vector2.zero;
            }

            base.FixedTick(fixedDeltaTime);
        }

        public override void OnEntityOverlap(Entity other)
        {
            if (!Data.CanDamagePlayerOnTouch) return;
            if (other is not ServerCharacter target) return;
            if (!target.CompareTag("Player")) return;
            if (target.Health.CurrentLifeState.Value != LifeState.Alive) return;
            
            var receiver = target.DamageReceiver;
            var hitData = new SyncHitData(_data.BaseDamage, _data.KnockbackForce, transform.position);
            
            receiver.ReceiveHit(hitData);
        }

        public void ApplyKnockback(Vector2 velocity)
        {
            DesiredDirection = Vector2.zero;
            KnockbackVelocity = velocity;
            Velocity = velocity;
        }
    }
}
