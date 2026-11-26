using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SketchLand
{
    public enum CharacterSpawnLocation { Ground, OpenAir }

    public class CharacterSpawnRules : MonoBehaviour
    {
        public static CharacterSpawnRules Instance { get; private set; }
        
        [SerializeField] private int _maxCharacterCount = 6;
        [SerializeField] private float _perSecondSpawnChance = 0.1f;
        [SerializeField] private List<CharacterSpawnEntry> _spawnEntries = new();
        
        private CharacterSpawnPool _spawnPool;

        private void Awake() 
        {
            Instance = this;
            _spawnPool = new CharacterSpawnPool(_spawnEntries);
        }
        
        public CharacterSpawnCircumstance GetCircumstances(ServerCharacter host)
        {
            return new CharacterSpawnCircumstance(_maxCharacterCount, _perSecondSpawnChance, _spawnPool);
        }
    }

    [Serializable]
    public sealed class CharacterSpawnEntry
    {
        [SerializeField] private CharacterSO _character;
        [SerializeField, Range(0f, 1f)] private float _weight = 1f;

        [SerializeField] private SpawnPhases _surfacePhases = SpawnPhases.All;
        [SerializeField] private SpawnPhases _undergroundPhases = SpawnPhases.All;
        [SerializeField] private CharacterSpawnLocation _spawnLocation = CharacterSpawnLocation.Ground;
        [Tooltip("Require an empty background tile at the spawn location just above the ground.")]
        [SerializeField] private bool _requireOpenBackground;
        [Tooltip("Only spawn on the outdoor ground, with no solid foreground roof above the spawn column.")]
        [SerializeField] private bool _requireSkyAccess;

        public bool RequireSkyAccess => _requireSkyAccess;
        public CharacterSpawnLocation SpawnLocation => _spawnLocation;

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
