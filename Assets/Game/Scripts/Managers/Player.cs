using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SketchLand
{
    public class Player : MonoBehaviour
    {
        public static Player Instance { get; private set; }
        
        public event Action<ServerCharacter> PlayerReady;
        
        [field: Header("Player Settings")]
        [field: SerializeField] public float InteractRange { get; private set; } = 4.5f;
        [field: SerializeField] public Vector2 WalkingBoxColliderSize { get; private set; }
        [field: SerializeField] public float SprintSpeed { get; private set; } = 6;
        [field: SerializeField, Min(0f)] public float SprintTransitionSharpness { get; private set; } = 5f;

        [field: Header("Jump Settings")]
        [field: SerializeField] public float MinJumpSpeed { get; private set; }
        [field: SerializeField] public float MaxJumpHoldDuration { get; private set; }
        [field: SerializeField] public float AirborneMoveSpeedMultiplier { get; private set; }
        [field: SerializeField] public float JumpBufferDuration { get; private set; }
        [field: SerializeField] public float CoyoteTimeBufferDuration { get; private set; }

        [field: Header("Gravity Settings")]
        [field: SerializeField] public float GravityForce { get; private set; }
        [field: SerializeField] public float TerminalVelocity { get; private set; }

        [field: Header("Health Regen")]
        [field: SerializeField, Min(0)] public float HealthRegenTickDuration { get; private set; } = 1;
        [field: SerializeField, Min(0)] public int HealthRegenPerTick { get; private set; } = 1;

        public bool JumpPressed { get; set; }
        public Vector2 RespawnPoint { get; private set; }
        public PlayerArmHandler ArmHandler { get; private set; }
        public PlayerRangedHandler RangedHandler { get; private set; }
        public ServerCharacter Character { get; private set; }
        public PlayerEquipment Equipment { get; private set; }

        private bool _hasInitialSpawn;
        private Coroutine _healthRegenerationRoutine;

        private void Awake() 
        {
            Instance = this;    
        }
        
        private void Start() 
        {
            GameInput.Instance.OnMoveInputPressed += OnMoveInputPressed;
            GameInput.Instance.OnJumpPressed += OnJumpPressed;
            GameInput.Instance.OnSecondaryActionPressed += InteractWithObject;
            WorldManager.Instance.OnWorldReady += InitializeSpawn;
            AudioManager.Instance.StartSurfaceAmbience(FMODEvents.Instance.SurfaceAmbience);

            InitializeSpawn();
        }
        
        private void OnDestroy() 
        {
            GameInput.Instance.OnMoveInputPressed -= OnMoveInputPressed;
            GameInput.Instance.OnJumpPressed -= OnJumpPressed;
            GameInput.Instance.OnSecondaryActionPressed -= InteractWithObject;
            WorldManager.Instance.OnWorldReady -= InitializeSpawn;

            if (Character != null) Character.Health.CurrentLifeState.OnValueChanged -= OnLifeStateChanged;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopSurfaceAmbience();
            }
        }

        private void Update()
        {
            if (!WorldManager.Instance.IsWorldReady || Character == null) return;

            // If player is not dead and is holding down move input keep updating desired direction with it
            if (Character.Health.CurrentLifeState.Value == LifeState.Dead) return;

            if (GameInput.Instance.MoveInput != Vector2.zero)
            {
                Character.DesiredDirection = GameInput.Instance.MoveInput;
            }
        }

        private void InitializeSpawn()
        {
            if (_hasInitialSpawn || Character == null || !WorldManager.Instance.IsWorldReady)
                return;

            if (!WorldManager.Instance.TryGetPlayerSpawnPosition(WalkingBoxColliderSize, out Vector2 spawnPosition))
            {
                Debug.LogWarning($"Could not find a spawn position for player, defaulting to prefab position");
                Character.transform.position = Character.transform.position;
                Character.Velocity = Vector2.zero;
                Character.DesiredDirection = Vector2.zero;
                Character.KnockbackVelocity = Vector2.zero;

                RespawnPoint = Character.transform.position;
                _hasInitialSpawn = true;
                return;
            }

            Character.transform.position = spawnPosition;
            Character.Velocity = Vector2.zero;
            Character.DesiredDirection = Vector2.zero;
            Character.KnockbackVelocity = Vector2.zero;

            RespawnPoint = spawnPosition;
            _hasInitialSpawn = true;
        }

        public void BindCharacter(ServerCharacter character)
        {
            if (Character == character) return;

            if (Character != null) Character.Health.CurrentLifeState.OnValueChanged -= OnLifeStateChanged;
            if (_healthRegenerationRoutine != null) StopCoroutine(_healthRegenerationRoutine);

            Character = character;
            Character.Health.CurrentLifeState.OnValueChanged += OnLifeStateChanged;

            Equipment = Character.GetComponent<PlayerEquipment>();
            ArmHandler = Character.GetComponent<PlayerArmHandler>();
            RangedHandler = Character.GetComponent<PlayerRangedHandler>();

            InitializeSpawn();

            _healthRegenerationRoutine = StartCoroutine(HealthRegenerationRoutine());

            PlayerReady?.Invoke(Character);
        }

        private IEnumerator HealthRegenerationRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(HealthRegenTickDuration);
                Character.Health.RestoreHealth(HealthRegenPerTick);
            }
        }

        private void OnLifeStateChanged(LifeState previousValue, LifeState newValue)
        {
            if(newValue == LifeState.Dead)
            {
                Character.KnockbackVelocity = Vector2.zero;
                Character.DesiredDirection = Vector2.zero;
                Character.Velocity = Vector2.zero;
            }
            
            if(previousValue == LifeState.Dead && newValue == LifeState.Alive)
            {
                // respawn
                Character.transform.position = RespawnPoint;
                Character.Health.CurrentHealth.Value = Character.Stats.MaxHealth.GetValue();
            }
        }

        private void InteractWithObject(InputAction.CallbackContext context)
        {
            // Interact with interactable logic here
            if (!WorldManager.Instance.IsWorldReady || WorldManager.Instance.MouseOverUI || !InventoryCursorManager.Instance.CursorSlot.IsEmpty || context.phase != InputActionPhase.Started || Character.Health.CurrentLifeState.Value == LifeState.Dead) return;

            var pos = WorldManager.MouseWorldTilePosition;
            var fgtd = WorldManager.Instance.FgGrid.GetTileData(pos.x, pos.y);

            if(fgtd.HasTile && fgtd.TileConfig.InteractBehavior != null)
            {
                fgtd.TileConfig.InteractBehavior.Interact(pos.x, pos.y);
            }
        }

        private void OnJumpPressed()
        {
            if(!WorldManager.Instance.IsWorldReady || Character.Health.CurrentLifeState.Value == LifeState.Dead) return;
        
            bool canJumpFromGround = Character.CollisionResult.TouchingBottom;

            if (canJumpFromGround)
            {
                JumpPressed = true;
            }
        }

        private void OnMoveInputPressed(Vector2 rawMoveInput)
        {
            if (!WorldManager.Instance.IsWorldReady || Character.Health.CurrentLifeState.Value == LifeState.Dead) return;

            Character.DesiredDirection = rawMoveInput;
        }
    }
}
