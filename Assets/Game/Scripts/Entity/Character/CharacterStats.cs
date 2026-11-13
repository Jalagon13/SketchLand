using System;

namespace SketchLand
{
    public class CharacterStats
    {
        public Stat MoveSpeed { get; }
        public Stat MaxHealth { get; }
        public Stat Defense { get; }

        public CharacterStats(CharacterSO data, int baseHealth)
        {
            MoveSpeed = new(data.BaseSpeed);
            MaxHealth = new(baseHealth);
            Defense = new(data.BaseDefense);
        }

        public Stat GetStat(StatType type)
        {
            return type switch
            {
                StatType.MoveSpeed => MoveSpeed,
                StatType.MaxHealth => MaxHealth,
                StatType.Defense => Defense,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
    }
}