using System;
using UnityEngine;

namespace SketchLand
{
    public enum LifeState
    {
        Alive,
        IFrame,
        Dead
    }

    public enum StatType
    {
        MoveSpeed,
        MaxHealth,
        Defense
    }

    public enum StatModifierType
    {
        Flat,
        Percent
    }

    public readonly struct StatModifier : IEquatable<StatModifier>
    {
        public StatModifierType Type { get; }
        public float Value { get; }

        public StatModifier(StatModifierType type, float value)
        {
            Type = type;
            Value = value;
        }

        public bool Equals(StatModifier other) => Type == other.Type && Value.Equals(other.Value);
        public override bool Equals(object obj) => obj is StatModifier other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Type, Value);
    }

    public enum ArmorType
    {
        Helmet,
        Chestplate,
        Pants
    }

    [Serializable]
    public struct ArmorStatModifier
    {
        public StatType TargetStat;
        public StatModifierType ModifierType;
        public float Value;
    }

    [Serializable]
    public struct AccessoryStatModifier
    {
        public StatType TargetStat;
        public StatModifierType ModifierType;
        public float Value;
    }

    public enum ProjectileFaction
    {
        Friendly, // Damages enemies / hostile NPCs
        Hostile, // Damages players / friendly NPCs
        Neutral // Damages all or none (environmental hazard)
    }

    public enum HarvestType
    {
        None = 0, // Can be mined by any tool (or bare hands / weapons: e.g. torches, vines, grass)
        Pickaxe = 1, // Stone, ores, minerals, bricks
        Axe = 2, // Trees, logs, wooden structures
    }
}