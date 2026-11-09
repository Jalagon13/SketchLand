using FMODUnity;
using UnityEngine;

namespace SketchLand
{
    [CreateAssetMenu(fileName = "New Projectile SO", menuName = "OceanGame/ProjectileSO")]
    public class ProjectileSO : ScriptableObject
    {
        [field: Header("Physics & Lifetime")]
        [field: SerializeField] public Vector2 ColliderSize { get; private set; } = new(0.4f, 0.4f);
        [field: SerializeField] public float BaseLifetime { get; private set; } = 5f;
        [field: SerializeField] public bool IgnoreTileCollision { get; private set; } = false;

        [field: Header("Combat Stats")]
        [field: SerializeField] public ProjectileFaction DefaultFaction { get; private set; } = ProjectileFaction.Friendly;
        [field: SerializeField] public int BaseDamage { get; private set; } = 10;
        [field: SerializeField] public int BasePenetration { get; private set; } = 1; // 1 = dies on first hit, -1 = infinite
        [field: SerializeField, Range(0, 100)] public int BaseKnockbackForce { get; private set; } = 15;

        [field: Header("Feel")]
        [field: SerializeField] public GameObject HitImpactPrefab { get; private set; }
        [field: SerializeField] public EventReference ImpactSFX { get; private set; }

        [field: Header("Prefab Reference")]
        [field: SerializeField] public Projectile ProjectilePrefab { get; private set; }
    }
}