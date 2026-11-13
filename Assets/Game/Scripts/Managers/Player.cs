using System;
using System.Collections;
using UnityEngine;

namespace SketchLand
{
    public class Player : MonoBehaviour
    {
        public static Player Instance { get; private set; }
        
        public event Action<ServerCharacter> PlayerReady;
        
        [field: Header("Player Settings")]
        [field: SerializeField] public float InteractRange { get; private set; } = 4.5f;
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

        public PlayerFood Food { get; private set; }

        [field: SerializeField] public PlayerStamina Stamina { get; private set; } = new();

        public bool JumpPressed { get; set; }
        public Vector2 RespawnPoint { get; private set; }
        public PlayerArmHandler ArmHandler { get; private set; }
        public PlayerRangedHandler RangedHandler { get; private set; }
        public ServerCharacter Character { get; private set; }
        public PlayerEquipment Equipment { get; private set; }

        private bool _hasInitialSpawn;

        private void Awake() 
        {
            Instance = this;    
        }
        
        private void Start() 
        {
            GameInput.Instance.OnMoveInputPressed += OnMoveInputPressed;
            GameInput.Instance.OnJumpPressed += OnJumpPressed;
            WorldManager.Instance.OnWorldReady += InitializeSpawn;
            AudioManager.Instance.StartSurfaceAmbience(FMODEvents.Instance.SurfaceAmbience);

            InitializeSpawn();
        }
        
        private void OnDestroy() 
        {
            GameInput.Instance.OnMoveInputPressed -= OnMoveInputPressed;
            GameInput.Instance.OnJumpPressed -= OnJumpPressed;
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

            Character.DesiredDirection = GameInput.Instance.MoveInput;
        }

        private void InitializeSpawn()
        {
            if (_hasInitialSpawn || Character == null || !WorldManager.Instance.IsWorldReady)
                return;

            if (!WorldManager.Instance.TryGetPlayerSpawnPosition(Character.Data.BodyColliderSize, out Vector2 spawnPosition))
            {
                Debug.LogWarning($"Could not find a spawn position for player, defaulting to prefab position");
                Character.transform.position = Character.transform.position;
                Character.Velocity = Vector2.zero;
                Character.DesiredDirection = Vector2.zero;
                Character.ClearKnockback();

                RespawnPoint = Character.transform.position;
                _hasInitialSpawn = true;
                return;
            }

            Character.transform.position = spawnPosition;
            Character.Velocity = Vector2.zero;
            Character.DesiredDirection = Vector2.zero;
            Character.ClearKnockback();

            RespawnPoint = spawnPosition;
            _hasInitialSpawn = true;
        }

        public void BindCharacter(ServerCharacter character)
        {
            if (Character == character) return;

            if (Character != null) Character.Health.CurrentLifeState.OnValueChanged -= OnLifeStateChanged;

            Character = character;
            Food = character.GetComponent<PlayerFood>();
            Stamina.SetFoodMaxBonus(0);
            Stamina.Refill();
            Character.Health.CurrentLifeState.OnValueChanged += OnLifeStateChanged;

            Equipment = Character.GetComponent<PlayerEquipment>();
            ArmHandler = Character.GetComponent<PlayerArmHandler>();
            RangedHandler = Character.GetComponent<PlayerRangedHandler>();

            InitializeSpawn();

            PlayerReady?.Invoke(Character);
        }

        private void OnLifeStateChanged(LifeState previousValue, LifeState newValue)
        {
            if(newValue == LifeState.Dead)
            {
                Character.ClearKnockback();
                Character.DesiredDirection = Vector2.zero;
                Character.Velocity = Vector2.zero;
            }
            
            if(previousValue == LifeState.Dead && newValue == LifeState.Alive)
            {
                Stamina.Refill();
                // respawn
                Character.transform.position = RespawnPoint;
                Character.Health.CurrentHealth.Value = Character.Health.Maximum;
            }
        }

        public bool TryInteractWithObject()
        {
            var pos = WorldManager.MouseWorldTilePosition;
            var fgtd = WorldManager.Instance.FgGrid.GetTileData(pos.x, pos.y);

            if(fgtd.HasTile && fgtd.TileConfig.InteractBehavior != null)
            {
                fgtd.TileConfig.InteractBehavior.Interact(pos.x, pos.y);
                return true;
            }
            return false;
        }

        private void OnJumpPressed()
        {
            if(!WorldManager.Instance.IsWorldReady || Character.Health.CurrentLifeState.Value == LifeState.Dead || Character.IsKnockedBack) return;
        
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
