using System;
using System.Collections.Generic;
using UnityEngine;

namespace SketchLand
{
    public readonly struct CharacterSpawnCircumstance
    {
        public int MaxCharacterCount { get; }
        public float PerSecondSpawnChance { get; }
        public CharacterSpawnPool SpawnPool { get; }

        public CharacterSpawnCircumstance(int maxCharacterCount, float perSecondSpawnChance, CharacterSpawnPool spawnPool)
        {
            MaxCharacterCount = maxCharacterCount;
            PerSecondSpawnChance = perSecondSpawnChance;
            SpawnPool = spawnPool;
        }
        
    }

    public sealed class CharacterSpawnPool
    {
        private readonly List<CharacterSpawnEntry> _entries;

        public CharacterSpawnPool(List<CharacterSpawnEntry> entries)
        {
            _entries = entries;
        }

        public bool TrySelectCharacter(WorldZone zone, DayPhase phase, bool openBackground, out CharacterSpawnEntry selectedEntry)
        {
            float totalWeight = 0f;

            foreach (CharacterSpawnEntry entry in _entries)
            {
                if (entry.CanSpawn(zone, phase, openBackground))
                    totalWeight += entry.Weight;
            }

            if (totalWeight <= 0f)
            {
                selectedEntry = default;
                return false;
            }

            float roll = UnityEngine.Random.value * totalWeight;

            foreach (CharacterSpawnEntry entry in _entries)
            {
                if (!entry.CanSpawn(zone, phase, openBackground))
                    continue;

                roll -= entry.Weight;

                if (roll <= 0f)
                {
                    selectedEntry = entry;
                    return true;
                }
            }

            selectedEntry = default;
            return false;
        }
    }

    [Flags]
    public enum SpawnPhases
    {
        None = 0,
        Sunrise = 1 << (int)DayPhase.Sunrise,
        Day = 1 << (int)DayPhase.Day,
        Sunset = 1 << (int)DayPhase.Sunset,
        Night = 1 << (int)DayPhase.Night,
        All = Sunrise | Day | Sunset | Night
    }
}