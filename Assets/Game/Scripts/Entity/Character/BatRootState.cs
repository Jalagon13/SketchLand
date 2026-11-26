using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SketchLand
{
    [Serializable]
    public class BatRootState : CharacterRootState
    {
        [Header("Pursue Flight")]
        [SerializeField] private Vector2 _circlingHeight = new(5f, 8f);
        [SerializeField] private Vector2 _circlingHorizontalRange = new(4f, 6f);
        [SerializeField] private float _velocitySteering = 3f;
        [SerializeField, Min(0.1f), Tooltip("Distance in world units at which a patrolling bat notices a living player. An existing chase continues until the target is lost.")]
        private float _targetDetectionRange = 16f;

        [Header("Knockback Recovery")]
        [SerializeField, Tooltip("How quickly hit velocity blends back toward intended flight during knockback. Independent of normal flight steering; higher values recover faster.")]
        private float _knockbackRecoverySteering = 10f;

        [Header("Swoop")]
        [SerializeField] private Vector2 _swoopInterval = new(5f, 8f);
        [SerializeField] private Vector2 _retreatAscentDuration = new(1f, 2f);
        [SerializeField, Min(0f)] private float _swoopSteering = 0.65f;
        [SerializeField, Min(0.1f)] private float _maximumSwoopDuration = 4.5f;

        [Header("Tile Collision")]
        [SerializeField, Range(0f, 1f)] private float _bounceDamping = 0.75f;
        [SerializeField, Min(0f), Tooltip("Seconds to gradually restore full steering immediately after a bounce.")]
        private float _postCollisionSteeringRecovery = 1.2f;

        [Header("Departure")]
        [SerializeField, Tooltip("Seconds without dealing or receiving damage before flying away.")]
        private float _combatInactivityTimeout = 120f;

        // Runtime instances read the asset settings live, including Inspector edits during play.
        [NonSerialized] private BatRootState _settingsSource;

        private BatRootState Settings => _settingsSource ?? this;
        private FlyingNpcMovement _movement;
        private bool _recoverAfterSwoop;
        private bool _leaving;
        private float _departureTimer;
        private float _timeSinceCombat;
        private ServerCharacter _target;
        private CharacterHealth _targetHealth;
        private Vector2 _patrolCenter;

        public BatPursueState Pursue { get; private set; }
        public BatSwoopState Swoop { get; private set; }
        public Vector2 PatrolCenter => _patrolCenter;
        public bool IsLeaving => _leaving || WorldManager.Instance.Clock.CurrentPhase != DayPhase.Night
            || _timeSinceCombat >= Mathf.Max(0.1f, Settings._combatInactivityTimeout);
        public float FlightSpeed => Mathf.Max(0, _ctx.Stats.MoveSpeed.GetValue());
        public float SwoopSteering => Mathf.Max(0f, Settings._swoopSteering);
        public float MaximumSwoopDuration => Mathf.Max(0.1f, Settings._maximumSwoopDuration);

        public override CharacterRootState CreateInstance() => new BatRootState { _settingsSource = this };

        public override void Initialize(ServerCharacter ctx)
        {
            base.Initialize(ctx);
            _patrolCenter = ctx.transform.position;
            _movement = new FlyingNpcMovement();
            Pursue = new BatPursueState(this, ctx);
            Swoop = new BatSwoopState(this, ctx);
        }

        protected override State GetInitialState() => Pursue;

        public bool TryGetTarget(out ServerCharacter player)
        {
            player = Player.Instance != null ? Player.Instance.Character : null;
            if (player == null || !player.gameObject.activeInHierarchy || player.Health == null
                || player.Health.CurrentLifeState.Value == LifeState.Dead)
            {
                if (_target != null) _patrolCenter = _target.transform.position;
                ReleaseTarget();
                player = null;
                return false;
            }

            if (_target != player)
            {
                ReleaseTarget();
                float range = Mathf.Max(0.1f, Settings._targetDetectionRange);
                if (((Vector2)player.transform.position - (Vector2)_ctx.transform.position).sqrMagnitude > range * range)
                {
                    player = null;
                    return false;
                }
                _target = player;
                _targetHealth = player.Health;
                _targetHealth.CurrentLifeState.OnValueChanged += OnTargetLifeStateChanged;
            }

            _patrolCenter = player.transform.position;
            return true;
        }

        private void OnTargetLifeStateChanged(LifeState previous, LifeState current)
        {
            if (current != LifeState.Dead) return;
            if (_target != null) _patrolCenter = _target.transform.position;
            ReleaseTarget();
        }

        private void ReleaseTarget()
        {
            if (_targetHealth != null)
                _targetHealth.CurrentLifeState.OnValueChanged -= OnTargetLifeStateChanged;
            _targetHealth = null;
            _target = null;
        }

        public override void Dispose()
        {
            ReleaseTarget();
            base.Dispose();
        }

        public float RandomCirclingHeight() => Sample(Settings._circlingHeight);
        public float RandomCirclingRange() => Sample(Settings._circlingHorizontalRange);
        public float RandomSwoopInterval() => Sample(Settings._swoopInterval);
        public float RandomRetreatDuration() => Sample(Settings._retreatAscentDuration);

        private static float Sample(Vector2 range)
        {
            float min = Mathf.Max(0f, Mathf.Min(range.x, range.y));
            return Random.Range(min, Mathf.Max(min, Mathf.Max(range.x, range.y)));
        }

        public bool TakeRecovery()
        {
            bool recover = _recoverAfterSwoop;
            _recoverAfterSwoop = false;
            return recover;
        }

        public void EndSwoop() => _recoverAfterSwoop = true;

        public void Steer(Vector2 desiredVelocity, float deltaTime)
        {
            if (!_ctx.IsServer || _ctx.Health.CurrentLifeState.Value == LifeState.Dead) return;
            if (_ctx.IsKnockedBack)
            {
                // Keep the full hit impulse, then ease toward flight without gravity,
                // a flight-speed clamp, or the tile-bounce recovery ramp.
                float blend = 1f - Mathf.Exp(-Mathf.Max(0f, Settings._knockbackRecoverySteering) * deltaTime);
                _ctx.Velocity = Vector2.Lerp(_ctx.Velocity, desiredVelocity, blend);
            }
            else
                _ctx.Velocity = _movement.Steer(_ctx.Velocity, desiredVelocity, Settings._velocitySteering, deltaTime);
            _ctx.DesiredDirection = _ctx.Velocity.normalized;
        }

        protected override void OnFixedUpdate(float fixedDeltaTime)
        {
            if (!_ctx.IsServer) return;
            _movement.Tick(fixedDeltaTime);
            if (_ctx.Health.CurrentLifeState.Value == LifeState.Dead)
            {
                _ctx.Velocity = Vector2.zero;
                return;
            }

            _timeSinceCombat += fixedDeltaTime;
            if (!IsLeaving) return;
            _leaving = true;
            _departureTimer += fixedDeltaTime;
            Steer(Vector2.up * FlightSpeed, fixedDeltaTime);
            var camera = Camera.main;
            bool offscreenAbove = camera != null && camera.WorldToViewportPoint(_ctx.transform.position).y > 1.15f;
            // A roof can block the escape: leave without a death/loot event after a short grace period.
            if ((_departureTimer > 1f && offscreenAbove) || _departureTimer >= 10f)
                CharacterManager.Instance.DespawnCharacter(_ctx);
        }

        public override void OnTileCollision(GridPhysics.CollisionResult collision, Vector2 incomingVelocity)
        {
            if (!_ctx.IsServer || _ctx.Health.CurrentLifeState.Value == LifeState.Dead) return;
            if (_movement.Bounce(incomingVelocity, collision, Settings._bounceDamping,
                out Vector2 reflected,
                steeringRecoveryDuration: Settings._postCollisionSteeringRecovery))
                _ctx.Velocity = reflected;
        }

        public override void OnDamageReceived() => RecordCombatActivity();
        public override void OnContactDamageDealt(ServerCharacter target) => RecordCombatActivity();

        private void RecordCombatActivity()
        {
            if (!_ctx.IsServer) return;
            _timeSinceCombat = 0f;
            InterruptSwoop();
        }

        private void InterruptSwoop()
        {
            if (_ctx.IsServer && _ctx.Health.CurrentLifeState.Value != LifeState.Dead && ActiveChild == Swoop)
                Machine.ChangeState(Swoop, Pursue);
        }
    }
}
