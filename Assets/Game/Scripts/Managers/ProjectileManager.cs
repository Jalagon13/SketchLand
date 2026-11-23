using Unity.Netcode;
using UnityEngine;

namespace SketchLand
{
    public static class ProjectileManager
    {
        public static Projectile SpawnProjectile(
            ProjectileSO projectileSO,
            Vector2 position,
            Vector2 initialVelocity,
            Entity sourceEntity = null,
            int? damageOverride = null,
            int? knockbackOverride = null,
            ProjectileFaction? factionOverride = null)
        {
            if (projectileSO == null || projectileSO.ProjectilePrefab == null)
            {
                Debug.LogWarning("Cannot spawn projectile: ProjectileSO or Prefab is null.");
                return null;
            }

            // Instantiate the projectile prefab
            Projectile projectile = Object.Instantiate(
                projectileSO.ProjectilePrefab,
                position,
                Quaternion.identity
            );

            // Resolve faction (infer from source entity if not explicitly provided)
            ProjectileFaction faction = factionOverride ?? InferFaction(sourceEntity, projectileSO.DefaultFaction);

            int damage = damageOverride ?? projectileSO.MaxBaseDamage;

            // Initialize stats (SO defaults + optional weapon overrides)
            projectile.Initialize(
                so: projectileSO,
                initialVelocity: initialVelocity,
                source: sourceEntity,
                damage: damage,
                knockback: knockbackOverride ?? projectileSO.MaxBaseKnockbackForce,
                faction: faction
            );

            // NGO Network Spawn (only if server/host and has NetworkObject)
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                if (projectile.TryGetComponent<NetworkObject>(out var netObj))
                {
                    netObj.Spawn(destroyWithScene: true);
                }
            }
            
            return projectile;
        }

        private static ProjectileFaction InferFaction(Entity source, ProjectileFaction defaultFaction)
        {
            if (source is ServerCharacter character)
            {
                return character.gameObject.layer == 3 ? ProjectileFaction.Friendly : ProjectileFaction.Hostile;
            }

            return defaultFaction;
        }
    }
}
