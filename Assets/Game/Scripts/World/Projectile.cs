using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace SketchLand
{
    public abstract class Projectile : Entity
    {
        [Header("Projectile Base Settings")]
        [SerializeField] private ProjectileSO _data;
        public ProjectileSO Data => _data;

        [SerializeField] private Transform _visuals; // Child transform for sprite rotation/scaling

        // Runtime Stats
        public ProjectileFaction Faction { get; private set; }
        public Entity SourceEntity { get; private set; }
        public int Damage { get; private set; }
        public float KnockbackForce { get; private set; }
        public int PenetrationRemaining { get; private set; }
        public float LifetimeRemaining { get; private set; }

        private bool _hasKilled;
        private readonly HashSet<Entity> _hitEntities = new();

        #region Lifecycle & Registration

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

        // Called right after instantiation by ProjectileManager to configure this projectile.
        public virtual void Initialize(ProjectileSO so, Vector2 initialVelocity, Entity source, int damage, float knockback, ProjectileFaction faction)
        {
            _data = so;
            SourceEntity = source;
            Damage = damage;
            KnockbackForce = knockback;
            Faction = faction;

            Velocity = initialVelocity;
            ColliderSize = so.ColliderSize;
            IgnoreCollision = so.IgnoreTileCollision;
            PenetrationRemaining = so.BasePenetration;
            LifetimeRemaining = so.BaseLifetime;

            _hitEntities.Clear();
            _hasKilled = false;

            UpdateVisualRotation();
            OnInit();
        }

        #endregion

        #region Physics & Ticking

        public override void FixedTick(float fixedDeltaTime)
        {
            if (!WorldManager.Instance.IsWorldReady || _hasKilled) return;

            // Tick lifetime
            LifetimeRemaining -= fixedDeltaTime;
            if (LifetimeRemaining <= 0f)
            {
                Kill();
                return;
            }

            // Custom trajectory & velocity modification hook (gravity, homing, drag)
            OnFixedUpdateBehavior(fixedDeltaTime);

            // Move and resolve tile collisions via Entity base (GridPhysics.MoveAndResolve)
            base.FixedTick(fixedDeltaTime);

            // Check if we hit any solid tile
            if (!IgnoreCollision && HasCollidedWithTile(CollisionResult))
            {
                OnTileCollide(CollisionResult);
            }

            // Keep visuals facing the direction of velocity (like Terraria arrows/bullets)
            UpdateVisualRotation();
        }

        private bool HasCollidedWithTile(GridPhysics.CollisionResult result)
        {
            return result.TouchingLeft || result.TouchingRight || result.TouchingTop || result.TouchingBottom;
        }

        private void UpdateVisualRotation()
        {
            if (_visuals == null || Velocity.sqrMagnitude < 0.001f) return;

            float angle = Mathf.Atan2(Velocity.y, Velocity.x) * Mathf.Rad2Deg;
            _visuals.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        #endregion

        #region Entity Overlap & Damage

        public override void OnEntityOverlap(Entity other)
        {
            if (_hasKilled || other == null) return;

            // Ignore the shooter who fired this projectile
            if (other == SourceEntity) return;

            // Prevent multi-hitting the same entity on adjacent physics frames
            if (_hitEntities.Contains(other)) return;

            // Check if target is a character with health and damage receiver
            if (other is not ServerCharacter target) return;
            if (target.Health == null || target.Health.CurrentLifeState.Value != LifeState.Alive) return;

            // Filter targets based on Faction
            if (!CanDamageTarget(target)) return;

            // Target is valid! Record hit
            _hitEntities.Add(target);

            // Apply Damage via your existing DamageReceiver system
            if (target.DamageReceiver != null)
            {
                var hitData = new SyncHitData(Damage, KnockbackForce, transform.position);
                target.DamageReceiver.ReceiveHit(hitData);
            }

            // Subclass hook for special effects (poison debuffs, lifesteal, sound, etc.)
            OnHitEntity(target);

            // Handle Penetration (piercing)
            if (PenetrationRemaining > 0)
            {
                PenetrationRemaining--;
                if (PenetrationRemaining <= 0)
                {
                    Kill();
                }
            }
            // Note: If PenetrationRemaining < 0 (e.g. -1), it has infinite pierce and will not kill here
        }

        private bool CanDamageTarget(ServerCharacter target)
        {
            bool isPlayer = target.gameObject.layer == 3;

            switch (Faction)
            {
                case ProjectileFaction.Friendly:
                    // Friendly projectiles only damage enemies (non-players)
                    return !isPlayer;

                case ProjectileFaction.Hostile:
                    // Hostile projectiles only damage players
                    return isPlayer;

                case ProjectileFaction.Neutral:
                    // Damages both players and enemies
                    return true;

                default:
                    return false;
            }
        }

        #endregion

        #region Destruction / Despawn

        public void Kill()
        {
            if (_hasKilled) return;
            _hasKilled = true;

            // Subclass hook (spawn explosion, drop item, play sound, death particles)
            OnKill();

            // Multiplayer-safe despawn via NGO
            if (TryGetComponent<NetworkObject>(out var netObj) && netObj.IsSpawned)
            {
                netObj.Despawn(destroy: true);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        #endregion

        #region Virtual Extension Hooks (For Subclasses)

        // Called when the projectile is spawned and initialized.
        protected virtual void OnInit() { }

        // Modify Velocity here every fixed frame (apply gravity, drag, homing, or acceleration).
        protected virtual void OnFixedUpdateBehavior(float fixedDeltaTime) { }

        // Called when the projectile contacts a solid tile. By default, it destroys the projectile.
        protected virtual void OnTileCollide(GridPhysics.CollisionResult collision)
        {
            Kill();
        }

        // Called when successfully dealing damage to an entity.
        protected virtual void OnHitEntity(Entity target) { }

        // Called immediately before destruction. Spawn dust/burst FX here.
        protected virtual void OnKill() { }

        #endregion
    }
}