using System;
using UnityEngine;

namespace SketchLand
{
    #region Root State

    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "SketchLand", null, "ZombieRootState")]
    [Serializable]
    public class GoblinRootState : CharacterRootState
    {
        private static readonly int DamagedAnimation = Animator.StringToHash("Base Layer.Goblin_Damaged");
        private static readonly int IsAirborne = Animator.StringToHash("IsAirborne");
        private static readonly int IsMoving = Animator.StringToHash("IsMoving");

        [field: Header("Gravity Settings")]
        [field: SerializeField] public float GravityForce { get; private set; }
        [field: SerializeField] public float TerminalVelocity { get; private set; }

        [field: Header("Jump Settings")]
        [field: SerializeField] public float MinJumpSpeed { get; private set; }
        [field: SerializeField] public float AirborneMoveSpeedMultiplier { get; private set; }

        [SerializeField, Min(0.1f), Tooltip("Minimum seconds between random jump attempts.")]
        private float _minJumpTimerDuration = 2f;
        [SerializeField, Min(0.1f), Tooltip("Maximum seconds between random jump attempts.")]
        private float _maxJumpTimerDuration = 6f;

        [Header("AI Settings")]
        [SerializeField, Tooltip("Minimum seconds without forward progress before idling and wandering.")]
        private float _minStuckTime = 1.5f;
        [SerializeField, Tooltip("Maximum seconds without forward progress before idling and wandering.")]
        private float _maxStuckTime = 3f;
        [SerializeField, Min(0.1f)] private float _minRetryTime = 5f;
        [SerializeField, Min(0.1f)] private float _maxRetryTime = 10f;
        [SerializeField, Min(0.1f)] private float _minIdleTime = 1f;
        [SerializeField, Min(0.1f)] private float _maxIdleTime = 2.5f;
        [SerializeField, Min(0.1f)] private float _minWanderTime = 1.5f;
        [SerializeField, Min(0.1f)] private float _maxWanderTime = 3f;
        [SerializeField, Min(0f), Tooltip("Seeing the player again within this range can restart pursuit early.")]
        private float _sightRange = 16f;

        [Header("Departure")]
        [SerializeField, Min(0.1f), Tooltip("Seconds without dealing or receiving damage before walking away and despawning.")]
        private float _combatInactivityTimeout = 120f;
        private const float MaximumDepartureDuration = 10f;

        public bool IsPursuing { get; private set; }
        public bool IsLeaving { get; private set; }
        public bool RandomJumpReady => _jumpTimer <= 0f;

        private float _jumpTimer;
        private float _stuckTimer;
        private float _stuckTime;
        private float _progressX;
        private float _pursuitDirection;
        private float _retryTimer;
        private float _wanderTimer;
        private float _wanderDirection;
        private bool _playerWasVisible;
        private float _timeSinceCombat;
        private float _departureTimer;
        private float _departureDirection;

        public GoblinGroundedState Grounded { get; private set; }
        public GoblinAirborneState Airborne { get; private set; }

        public GoblinRootState() : base() { }

        public override CharacterRootState CreateInstance()
        {
            return new GoblinRootState
            {
                GravityForce = GravityForce,
                TerminalVelocity = TerminalVelocity,
                MinJumpSpeed = MinJumpSpeed,
                AirborneMoveSpeedMultiplier = AirborneMoveSpeedMultiplier,
                _minJumpTimerDuration = _minJumpTimerDuration,
                _maxJumpTimerDuration = _maxJumpTimerDuration,
                _minStuckTime = _minStuckTime,
                _maxStuckTime = _maxStuckTime,
                _minRetryTime = _minRetryTime,
                _maxRetryTime = _maxRetryTime,
                _minIdleTime = _minIdleTime,
                _maxIdleTime = _maxIdleTime,
                _minWanderTime = _minWanderTime,
                _maxWanderTime = _maxWanderTime,
                _sightRange = _sightRange,
                _combatInactivityTimeout = _combatInactivityTimeout
            };
        }

        public override void Initialize(ServerCharacter ctx)
        {
            base.Initialize(ctx);

            // Construct child states once the context is available
            Grounded = new GoblinGroundedState(null, this, ctx);
            Airborne = new GoblinAirborneState(null, this, ctx);
        }

        protected override State GetInitialState() => Grounded;

        protected override void OnDamaged()
        {
            // The controller can contain only the clips that have been authored so far.
            if (_ctx.Animator != null && _ctx.Animator.HasState(0, DamagedAnimation))
                PlayDamageAnimation(DamagedAnimation);
        }

        protected override void OnEnter()
        {
            SetAirborneAnimation(false);
            IsLeaving = false;
            _timeSinceCombat = 0f;
            _departureTimer = 0f;
            ResetJumpTimer();
            BeginPursuit();
            UpdateAI(0f);
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
            _jumpTimer = Mathf.Max(0f, _jumpTimer - deltaTime);
            UpdateAI(deltaTime);
            if (_ctx.Animator != null)
                _ctx.Animator.SetBool(IsMoving, Mathf.Abs(_ctx.Velocity.x) > 0.1f);
        }

        protected override void OnFixedUpdate(float fixedDeltaTime)
        {
            if (!_ctx.IsServer || _ctx.Health.CurrentLifeState.Value == LifeState.Dead) return;
            _timeSinceCombat += fixedDeltaTime;
            if (!IsLeaving && _timeSinceCombat >= Mathf.Max(0.1f, _combatInactivityTimeout))
            {
                IsLeaving = true;
                IsPursuing = false;
                var player = Player.Instance != null ? Player.Instance.Character : null;
                float away = player != null ? _ctx.transform.position.x - player.transform.position.x : _ctx.FacingDirection;
                _departureDirection = Mathf.Abs(away) > 0.01f ? Mathf.Sign(away) : _ctx.FacingDirection;
                _ctx.DesiredDirection = Vector2.right * _departureDirection;
            }
            if (!IsLeaving) return;

            _departureTimer += fixedDeltaTime;
            var camera = Camera.main;
            float viewportX = camera != null ? camera.WorldToViewportPoint(_ctx.transform.position).x : 0.5f;
            bool offscreen = viewportX < -0.15f || viewportX > 1.15f;
            // Match bats: a trapped NPC eventually leaves without a death or loot event.
            if (offscreen || _departureTimer >= MaximumDepartureDuration)
                CharacterManager.Instance.DespawnCharacter(_ctx);
        }

        public override void OnDamageReceived() => RecordCombatActivity();
        public override void OnContactDamageDealt(ServerCharacter target) => RecordCombatActivity();

        private void RecordCombatActivity()
        {
            if (!_ctx.IsServer || _ctx.Health.CurrentLifeState.Value == LifeState.Dead) return;
            _timeSinceCombat = 0f;
            if (!IsLeaving) return;
            IsLeaving = false;
            _departureTimer = 0f;
            BeginPursuit();
        }

        public void UpdateHorizontalMovement(float fixedDeltaTime, float speedMultiplier = 1f)
        {
            if (_ctx.IsKnockedBack) return;

            var currentSpeed = _ctx.Data.BaseSpeed * speedMultiplier;
            _ctx.UpdateHorizontalMovement(_ctx.DesiredDirection.x * currentSpeed, fixedDeltaTime);

            if (_ctx.DesiredDirection.x == 0f && Mathf.Abs(_ctx.Velocity.x) <= 0.01f)
            {
                _ctx.Velocity.x = 0f;
            }
        }

        public void ResetJumpTimer()
        {
            float minimum = Mathf.Max(0.1f, _minJumpTimerDuration);
            _jumpTimer = UnityEngine.Random.Range(minimum, Mathf.Max(minimum, _maxJumpTimerDuration));
        }

        private void BeginPursuit()
        {
            IsPursuing = true;
            _stuckTimer = 0f;
            _stuckTime = UnityEngine.Random.Range(_minStuckTime, Mathf.Max(_minStuckTime, _maxStuckTime));
            _progressX = _ctx.transform.position.x;
            _pursuitDirection = 0f;
        }

        private void UpdateAI(float deltaTime)
        {
            if (IsLeaving)
            {
                _ctx.DesiredDirection = Vector2.right * _departureDirection;
                return;
            }
            var player = Player.Instance != null ? Player.Instance.Character : null;
            if (player == null)
            {
                _ctx.DesiredDirection = Vector2.zero;
                return;
            }

            if (_ctx.IsKnockedBack)
            {
                _stuckTimer = 0f;
                _progressX = _ctx.transform.position.x;
                return;
            }

            if (!IsPursuing)
            {
                _retryTimer -= deltaTime;
                bool visible = CanSeePlayer(player.transform.position);
                // A player who stayed visible behind an obstacle must not cancel the break immediately.
                bool spottedPlayer = visible && !_playerWasVisible;
                _playerWasVisible = visible;
                if (_retryTimer <= 0f || spottedPlayer)
                    BeginPursuit();
                else
                {
                    _wanderTimer -= deltaTime;
                    bool blocked = (_wanderDirection < 0f && _ctx.CollisionResult.TouchingLeft)
                        || (_wanderDirection > 0f && _ctx.CollisionResult.TouchingRight);
                    if (_wanderTimer <= 0f || blocked)
                    {
                        _wanderDirection = _wanderDirection == 0f ? (UnityEngine.Random.value < 0.5f ? -1f : 1f) : 0f;
                        _wanderTimer = _wanderDirection == 0f
                            ? UnityEngine.Random.Range(_minIdleTime, Mathf.Max(_minIdleTime, _maxIdleTime))
                            : UnityEngine.Random.Range(_minWanderTime, Mathf.Max(_minWanderTime, _maxWanderTime));
                    }
                    _ctx.DesiredDirection = Vector2.right * _wanderDirection;
                    return;
                }
            }

            float distance = player.transform.position.x - _ctx.transform.position.x;
            _ctx.DesiredDirection = Mathf.Abs(distance) > 0.1f ? Vector2.right * Mathf.Sign(distance) : Vector2.zero;
            float direction = _ctx.DesiredDirection.x;
            // Measure horizontal travel, so jumping in place cannot reset the stuck timer.
            if (direction == 0f || direction != _pursuitDirection
                || (_ctx.transform.position.x - _progressX) * direction >= 0.1f)
            {
                _stuckTimer = 0f;
                _progressX = _ctx.transform.position.x;
                _pursuitDirection = direction;
            }
            else _stuckTimer += deltaTime;

            if (_stuckTimer < _stuckTime) return;

            IsPursuing = false;
            _retryTimer = UnityEngine.Random.Range(_minRetryTime, Mathf.Max(_minRetryTime, _maxRetryTime));
            _wanderDirection = 0f;
            _wanderTimer = UnityEngine.Random.Range(_minIdleTime, Mathf.Max(_minIdleTime, _maxIdleTime));
            _playerWasVisible = CanSeePlayer(player.transform.position);
            _ctx.DesiredDirection = Vector2.zero;
        }

        private bool CanSeePlayer(Vector2 playerPosition)
        {
            Vector2 position = _ctx.transform.position;
            if ((playerPosition - position).sqrMagnitude > _sightRange * _sightRange) return false;

            // Check the foreground tiles along the line between the goblin and player.
            Vector2Int tile = Vector2Int.FloorToInt(position);
            Vector2Int end = Vector2Int.FloorToInt(playerPosition);
            int dx = Mathf.Abs(end.x - tile.x), dy = Mathf.Abs(end.y - tile.y);
            int stepX = tile.x < end.x ? 1 : -1, stepY = tile.y < end.y ? 1 : -1;
            int error = dx - dy;
            while (true)
            {
                var data = WorldManager.Instance.FgGrid.GetTileData(tile.x, tile.y);
                if (data.IsOutOfBounds || (data.HasTile && data.IsSolid)) return false;
                if (tile == end) return true;
                int twiceError = error * 2;
                if (twiceError > -dy) { error -= dy; tile.x += stepX; }
                if (twiceError < dx) { error += dx; tile.y += stepY; }
            }
        }
    }

    #endregion

    #region Grounded State

    public class GoblinGroundedState : State
    {
        public readonly GoblinIdleState Idle;
        public readonly GoblinMoveState Move;

        private readonly ServerCharacter _ctx;

        public GoblinGroundedState(StateMachine m, State parent, ServerCharacter ctx) : base(m, parent)
        {
            _ctx = ctx;

            Idle = new GoblinIdleState(m, this, ctx);
            Move = new GoblinMoveState(m, this, ctx);
        }

        protected override State GetInitialState() => Idle;

        protected override State GetTransition()
        {
            var root = (GoblinRootState)Parent;

            if (!_ctx.CollisionResult.TouchingBottom || _ctx.Velocity.y > 0f)
            {
                return root.Airborne;
            }

            bool blockedAhead = (_ctx.DesiredDirection.x < 0f && _ctx.CollisionResult.TouchingLeft)
                || (_ctx.DesiredDirection.x > 0f && _ctx.CollisionResult.TouchingRight);
            if (!_ctx.IsKnockedBack && !_ctx.CollisionResult.TouchingTop
                && _ctx.Health.CurrentLifeState.Value != LifeState.Dead && root.MinJumpSpeed > 0f
                && (root.RandomJumpReady || ((root.IsPursuing || root.IsLeaving) && blockedAhead)))
            {
                _ctx.Velocity.y = root.MinJumpSpeed;
                root.ResetJumpTimer();
                return root.Airborne;
            }

            return null;
        }

        protected override void OnFixedUpdate(float fixedDeltaTime)
        {
            if (_ctx.IsKnockedBack)
            {
                var root = (GoblinRootState)Parent;
                _ctx.ApplyGravity(root.GravityForce, root.TerminalVelocity, fixedDeltaTime);
                return;
            }

            _ctx.Velocity.y = -0.1f; // Keep the goblin pressed to the floor
        }
    }

    #endregion

    #region Airborne State

    public class GoblinAirborneState : State
    {
        private readonly ServerCharacter _ctx;

        public GoblinAirborneState(StateMachine m, State parent, ServerCharacter ctx) : base(m, parent)
        {
            _ctx = ctx;
        }

        protected override void OnEnter()
        {
            ((GoblinRootState)Parent).SetAirborneAnimation(true);
        }

        protected override void OnExit()
        {
            ((GoblinRootState)Parent).SetAirborneAnimation(false);
        }

        protected override State GetTransition()
        {
            if (_ctx.CollisionResult.TouchingBottom && _ctx.Velocity.y <= 0f)
            {
                return ((GoblinRootState)Parent).Grounded;
            }

            return null;
        }

        protected override void OnFixedUpdate(float fixedDeltaTime)
        {
            var root = (GoblinRootState)Parent;

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

    public class GoblinIdleState : State
    {
        private readonly ServerCharacter _ctx;

        public GoblinIdleState(StateMachine m, State parent, ServerCharacter ctx) : base(m, parent)
        {
            _ctx = ctx;
        }

        protected override State GetTransition()
        {
            if (_ctx.DesiredDirection.x != 0f)
            {
                return ((GoblinGroundedState)Parent).Move;
            }

            return null;
        }

        protected override void OnFixedUpdate(float fixedDeltaTime)
        {
            ((GoblinRootState)Parent.Parent).UpdateHorizontalMovement(fixedDeltaTime);
        }
    }

    #endregion

    #region Move State

    public class GoblinMoveState : State
    {
        private readonly ServerCharacter _ctx;

        public GoblinMoveState(StateMachine m, State parent, ServerCharacter ctx) : base(m, parent)
        {
            _ctx = ctx;
        }

        protected override State GetTransition()
        {
            if (_ctx.DesiredDirection.x == 0f)
            {
                return ((GoblinGroundedState)Parent).Idle;
            }

            return null;
        }

        protected override void OnFixedUpdate(float fixedDeltaTime)
        {
            ((GoblinRootState)Parent.Parent).UpdateHorizontalMovement(fixedDeltaTime);
        }
    }

    #endregion
}
