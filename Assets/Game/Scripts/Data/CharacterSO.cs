using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

namespace SketchLand
{
    [CreateAssetMenu(fileName = "New Character SO", menuName = "OceanGame/CharacterSO")]
    public class CharacterSO : ScriptableObject
    {
        [Header("AI State Machine")]
        [SerializeReference]
        public CharacterRootState RootState;

        [Header("Core Stats")]
        [field: SerializeField] public Vector2 BodyColliderSize { get; private set; } = Vector2.one;
        [field: SerializeField] public int BaseMaxHealth { get; private set; }
        [field: SerializeField] public int BaseDefense { get; private set; }
        [field: SerializeField] public int BaseDamage { get; private set; }
        [field: Tooltip("Fraction of the global maximum knockback speed. 0 = none, 1 = full strength before target resistance.")]
        [field: SerializeField, Range(0f, 1f)] public float KnockbackForce { get; private set; }
        [field: Tooltip("Fraction of incoming knockback blocked. 0 = no resistance, 1 = immune.")]
        [field: SerializeField, Range(0f, 1f)] public float BaseKbResist { get; private set; }
        [field: SerializeField] public int BaseSpeed { get; private set; }
        [field: SerializeField] public float BaseIFrameDuration { get; private set; }
        [field: SerializeField] public float BaseTurnSharpness { get; private set; }
        [field: SerializeField] public bool CanBeKnockedBacked { get; private set; } = true;
        [field: SerializeField] public bool CanDie { get; private set; } = true;
        [field: SerializeField] public bool CanDamagePlayerOnTouch { get; private set; }
        [field: SerializeField] public ServerCharacter CharacterPrefab { get; private set; }

        [field: Header("Spawning")]
        [field: Tooltip("Amount this NPC contributes to local and global population limits. Supports fractions, e.g. 0.5 for small creatures or 3 for large monsters.")]
        [field: SerializeField] public float SpawnCost { get; private set; } = 1f;

        [field: Header("Game Feel")]
        [field: SerializeField] public EventReference CharacterDamagedSFX { get; private set; }
        [field: SerializeField] public EventReference CharacterDeathSFX { get; private set; }

        [field: Header("Drops")]

        [field: SerializeField] public List<LootDropEntry> LootDrops = new();

        public void DropLoot(Vector2 position)
        {
            if (LootDrops == null) return;

            for (int i = 0; i < LootDrops.Count; i++)
            {
                LootDrops[i].TryDrop(position);
            }
        }
    }
}
