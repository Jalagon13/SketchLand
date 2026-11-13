using Sirenix.OdinInspector;
using UnityEngine;

namespace SketchLand
{
    [RequireComponent(typeof(CharacterHealth), typeof(DamageReceiver))]
    public class ServerCharacter : Entity
    {
        [SerializeField] private bool _debugStateOn;
        [SerializeField] private CharacterSO _data;
        public CharacterSO Data => _data;

        [Header("Visuals")]
        [SerializeField] private Transform _visuals;
        [SerializeField] private Animator _animator;

        [HideInInspector] public Vector2 DesiredDirection;
        public bool IsKnockedBack => _knockbackTimer > 0f;

        public StateMachine Machine { get; private set; }
        public CharacterHealth Health { get; private set; }
        public CharacterStats Stats { get; private set; }
        public DamageReceiver DamageReceiver { get; private set; }
        public CharacterRootState RootState { get; private set; }
        public Animator Animator => _animator;
        public float FacingDirection => _visuals != null && _visuals.localScale.x < 0f ? -1f : 1f;

        public const int MaxKnockbackForce = 100;

        // Shared by every character. Duration scales with strength after resistance.
        private const float MinKnockbackDuration = 0.15f;
        private const float MaxKnockbackDuration = 0.5f;
        private const float HorizontalKnockbackRatio = 0.55f;
        private const float UpwardKnockbackRatio = 0.45f;
        private const float KnockbackDirectionEpsilon = 0.001f;

        private float _knockbackTimer;
        private float? _facingOverride;

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
            ClearKnockback();
            RootState?.Dispose();
            RootState = _data.RootState?.CreateInstance();
            Machine = null;

            if (RootState == null) return;

            RootState.Initialize(this);
            Machine = new StateMachineBuilder(RootState).Build(_debugStateOn);
            Machine.Start();
        }

        public override void OnNetworkDespawn()
        {
            ClearKnockback();
            RootState?.Dispose();
            RootState = null;
            Machine = null;
            base.OnNetworkDespawn();
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

            if (!IsKnockedBack)
                SetFacing(DesiredDirection.x);

            Machine.FixedTick(fixedDeltaTime);
            base.FixedTick(fixedDeltaTime);

            // Grid physics resolves position; discard momentum into the surfaces it hit.
            if ((CollisionResult.TouchingLeft && Velocity.x < 0f) || (CollisionResult.TouchingRight && Velocity.x > 0f))
                Velocity.x = 0f;

            if ((CollisionResult.TouchingBottom && Velocity.y < 0f) || (CollisionResult.TouchingTop && Velocity.y > 0f))
                Velocity.y = 0f;

            // Keep the final knockback tick intact. Movement resumes next tick from this velocity.
            _knockbackTimer = Mathf.Max(0f, _knockbackTimer - fixedDeltaTime);
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

        public void SetFacing(float horizontal)
        {
            horizontal = _facingOverride ?? horizontal;

            if (_visuals == null || Mathf.Abs(horizontal) < 0.01f)
                return;

            Vector3 scale = _visuals.localScale;
            scale.x = Mathf.Abs(scale.x) * (horizontal > 0f ? 1f : -1f);
            _visuals.localScale = scale;
        }

        public void SetFacingOverride(float horizontal)
        {
            if (_visuals == null) return;

            // A vertical swing keeps the current facing, but still locks it
            // against movement until the swing finishes.
            _facingOverride = Mathf.Abs(horizontal) < 0.01f
                ? Mathf.Sign(_visuals.localScale.x)
                : Mathf.Sign(horizontal);
            SetFacing(horizontal);
        }

        public void ClearFacingOverride()
        {
            // Leave the visual orientation in place. Movement can change it
            // on a later tick when there is horizontal input again.
            _facingOverride = null;
        }

        public void UpdateHorizontalMovement(float targetSpeed, float fixedDeltaTime)
        {
            if (IsKnockedBack) return;

            float acceleration = Mathf.Max(1f, Mathf.Max(Data.BaseSpeed, Mathf.Abs(targetSpeed))) * Data.BaseTurnSharpness;
            Velocity.x = Mathf.MoveTowards(Velocity.x, targetSpeed, acceleration * fixedDeltaTime);
        }

        public void ApplyGravity(float gravityForce, float terminalVelocity, float fixedDeltaTime)
        {
            Velocity.y = Mathf.Max(Velocity.y - gravityForce * fixedDeltaTime, terminalVelocity);
        }

        public void ApplyKnockback(int force, Vector2 sourcePosition)
        {
            if (!Data.CanBeKnockedBacked || Health.CurrentLifeState.Value == LifeState.Dead) return;

            float strength = Mathf.Clamp(force, 0, MaxKnockbackForce) * (1f - Mathf.Clamp01(Data.BaseKbResist));
            if (strength <= 0f) return;

            float horizontalDistance = transform.position.x - sourcePosition.x;
            float direction = Mathf.Abs(horizontalDistance) <= KnockbackDirectionEpsilon
                ? (Random.value < 0.5f ? -1f : 1f)
                : Mathf.Sign(horizontalDistance);

            Velocity = new Vector2(strength * HorizontalKnockbackRatio * direction, strength * UpwardKnockbackRatio);
            _knockbackTimer = Mathf.Lerp(MinKnockbackDuration, MaxKnockbackDuration, strength / MaxKnockbackForce);
        }

        public void ClearKnockback()
        {
            _knockbackTimer = 0f;
        }
    }
}
