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

    [Serializable]
    public sealed class CharacterSpawnEntry
    {
        [SerializeField] private CharacterSO _character;
        [SerializeField, Range(0f, 1f)] private float _weight = 1f;

        [SerializeField] private SpawnPhases _surfacePhases = SpawnPhases.All;
        [SerializeField] private SpawnPhases _undergroundPhases = SpawnPhases.All;
        [Tooltip("Require an empty background tile at the spawn location just above the ground.")]
        [SerializeField] private bool _requireOpenBackground;

        public bool CanSpawn(WorldZone zone, DayPhase phase, bool openBackground)
        {
            SpawnPhases allowed = zone.AllowsNaturalDaylight ? _surfacePhases : _undergroundPhases;
            return _character != null && _weight > 0f && (!_requireOpenBackground || openBackground)
                && (allowed & (SpawnPhases)(1 << (int)phase)) != 0;
        }

        public CharacterSO Character => _character;
        public float Weight => _weight;
    }
}