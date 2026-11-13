using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SketchLand
{
    public class CharacterSpawnRules : MonoBehaviour
    {
        public static CharacterSpawnRules Instance { get; private set; }
        
        [FormerlySerializedAs("_tempMaxCharCount")]
        [SerializeField] private int _maxCharacterCount = 6;
        [FormerlySerializedAs("_tempPerSecSpawnChance")]
        [SerializeField] private float _perSecondSpawnChance = 0.1f;
        [FormerlySerializedAs("_tempSpawnEntries")]
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
}
