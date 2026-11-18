using System;
using UnityEngine;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace SketchLand
{
    public class Item : Entity
    {
        [SerializeField] private SpriteRenderer _sprite;
        [SerializeField] private Vector2 _colliderSize = new(0.4f, 0.4f);

        [Header("Detection Setup")]
        [SerializeField] private float _ableToCollectTimer = 0.5f;
        [SerializeField] private float _detectRange = 5f;

        public ItemContext Ctx = new();

        private const float DETECTION_INTERVAL = 0.125f;
        private float _timer;
        private StateMachine _machine;
        private State _root;

        private void Awake() 
        {
            ColliderSize = _colliderSize;

            Ctx.Item = this;
            Ctx.ClosestPlayer = null;
            Ctx.CanBeCollected = false;
            Ctx.HasBeenCollected = false;

            _root = new ItemRootState(null, Ctx);
            var builder = new StateMachineBuilder(_root);
            _machine = builder.Build();
            _machine.Start();
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

        private IEnumerator Start() 
        {
            yield return new WaitForSeconds(_ableToCollectTimer);
            
            Ctx.CanBeCollected = true;
        }

        private void Update()
        {
            if (!WorldManager.Instance.IsWorldReady) return;

            _machine.UpdateTick(Time.deltaTime);
        }

        public override void FixedTick(float fixedDeltaTime)
        {
            if (!WorldManager.Instance.IsWorldReady) return;
            if (Ctx.HasBeenCollected) return;

            _machine.FixedTick(fixedDeltaTime);

            _timer -= Time.deltaTime;
            if (_timer <= 0 && Ctx.CanBeCollected && Ctx.ItemSlot != null && !Ctx.HasBeenCollected)
            {
                _timer = DETECTION_INTERVAL;
                DetectPlayer();
            }

            base.FixedTick(fixedDeltaTime);
        }

        public override void OnEntityOverlap(Entity other)
        {
            if (!Ctx.CanBeCollected || Ctx.HasBeenCollected || Ctx.ItemSlot == null) return;
            if (other is not ServerCharacter character) return;
            if (character.gameObject.layer != 3) return;
            if (character.Health != null && character.Health.CurrentLifeState.Value != LifeState.Alive) return;

            bool canThisPlayerAcceptThisItem = InventoryManager.Instance.CanAcceptItem(Ctx.ItemSlot.ItemId, Ctx.ItemSlot.Amount);
            if (canThisPlayerAcceptThisItem)
            {
                OnItemCollected();
            }
        }

        public void InitializeItem(InventorySlot item, Vector2 startingVelocity = default)
        {
            Ctx.ItemSlot = item;
            Velocity = startingVelocity;

            // Visuals
            _sprite.sprite = GameDataRegistry.Instance.GetItemSOFromItemId(item.ItemId).DisplayIcon;
        }

        private void DetectPlayer()
        {
            ServerCharacter closestPlayer = null;
            float closestDistance = _detectRange;

            if (EntityManager.Instance != null)
            {
                IReadOnlyList<Entity> entities = EntityManager.Instance.Entities;
                for (int i = 0; i < entities.Count; i++)
                {
                    Entity entity = entities[i];
                    if (entity == null || entity == this) continue;

                    if (entity is ServerCharacter character && character.gameObject.layer == 3)
                    {
                        if (character.Health != null && character.Health.CurrentLifeState.Value != LifeState.Alive) continue;

                        // Only detect players who can accept this item. In the future, query each player for can accept item somehow
                        bool canThisPlayerAcceptThisItem = InventoryManager.Instance.CanAcceptItem(Ctx.ItemSlot.ItemId, Ctx.ItemSlot.Amount);

                        if (canThisPlayerAcceptThisItem)
                        {
                            float distance = Vector2.Distance(transform.position, character.transform.position);

                            if (distance < closestDistance)
                            {
                                closestDistance = distance;
                                closestPlayer = character;
                            }
                        }
                    }
                }
            }
            
            Ctx.ClosestPlayer = closestPlayer;
        }
        
        public void OnItemCollected()
        {
            int attemptedAmount = Ctx.ItemSlot.Amount;
            int remainder = InventoryManager.Instance.CollectItem(Ctx.ItemSlot.ItemId, attemptedAmount);

            if (remainder <= 0)
            {
                Ctx.HasBeenCollected = true;
                Destroy(gameObject);
            }
            else // If has a remainder, assign the item to the remainder and re calculate closest player again to see if it can accept the item again
            {
                Ctx.ItemSlot.AssignItem(Ctx.ItemSlot.ItemId, remainder);
                Ctx.ClosestPlayer = null;
            }
        }
    }
    
    [Serializable]
    public class ItemContext
    {
        public InventorySlot ItemSlot; // Not hidden for debug purposes
        
        [Header("Attraction")]
        public float CollectRange = 0.2f;
        public float AttractSpeed = 20f;
        public float TurnSharpness = 20f;
        public float ThrowAirResistance = 5;
        
        [Header("Gravity")]
        public float GravityForce = 25f;
        public float TerminalVelocity = -40f;

        [HideInInspector] public Item Item;
        [HideInInspector] public Vector2 DesiredDirection;
        [HideInInspector] public ServerCharacter ClosestPlayer;
        [HideInInspector] public bool CanBeCollected;
        [HideInInspector] public bool HasBeenCollected;

        public Transform Transform => Item != null ? Item.transform : null;
        public ref Vector2 Velocity => ref Item.Velocity;
        public GridPhysics.CollisionResult CollisionResult => Item != null ? Item.CollisionResult : default;
        public bool IgnoreCollisions
        {
            get => Item != null && Item.IgnoreCollision;
            set
            {
                if (Item != null) Item.IgnoreCollision = value;
            }
        }
    }
}
