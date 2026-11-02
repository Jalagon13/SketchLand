using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace SketchLand
{
    public class CharacterManager : MonoBehaviour
    {
        public static CharacterManager Instance { get; private set; }

        [SerializeField] private bool _enableSpawning = true;
        
        [SerializeField] private int _globalMaxCharacterCap = 200;
        public int GlobalMaxCharCap => _globalMaxCharacterCap;
        
        [SerializeField] private float _durationTillCharacterDespawns = 20f;
        
        private List<CharacterSpawner> _spawners = new();
        private readonly Dictionary<ServerCharacter, CharacterLifetime> _characterDespawnTimers = new();
        private class CharacterLifetime
        {
            public CharacterSpawner Owner;
            public float RemainingTime;
            
            public CharacterLifetime(CharacterSpawner owner, float remainingTime)
            {
                Owner = owner;
                RemainingTime = remainingTime;
            }
        }

        public int CurrentCharacterCount
        {
            get
            {
                int count = 0;

                foreach (CharacterSpawner spawner in _spawners)
                {
                    count += spawner.LocalCurrentCharCount;
                }

                return count;
            }
        }

        private void Awake() 
        {
            Instance = this;
        }
        
        private void OnDrawGizmos()
        {
            if (_spawners == null || _spawners.Count == 0) return;

            for (int i = 0; i < _spawners.Count; i++)
            {
                CharacterSpawner spawner = _spawners[i];
                if (spawner == null) continue;

                Gizmos.color = Color.green;
                DrawRectIntGizmo(spawner.SpawnArea);
                Gizmos.color = Color.red;
                DrawRectIntGizmo(spawner.NoSpawnArea);
                Gizmos.color = Color.black;
                DrawRectIntGizmo(spawner.ActiveArea);
                Gizmos.color = Color.white;
                DrawRectIntGizmo(spawner.TimerSafeArea);
            }
        }

        // Ticks after everything in the entitymanager so it uses up to date positions
        public void TickCharacters() 
        {
            if(!WorldManager.Instance.IsWorldReady) return;
            
            if(_spawners.Count > 0)
            {
                for (int i = _spawners.Count - 1; i >= 0; i--)
                {
                    _spawners[i].TickSpawner();
                }
            }
            
            TickCharacterLifetimes();
        }

        private void TickCharacterLifetimes()
        {
            List<ServerCharacter> charactersToDespawn = new();

            // Loop through all spawned characters
            foreach (KeyValuePair<ServerCharacter, CharacterLifetime> entry in _characterDespawnTimers)
            {
                ServerCharacter character = entry.Key;

                // Check if null for any reason
                if (character == null)
                {
                    charactersToDespawn.Add(character);
                    continue;
                }

                // Check if dead
                if (character.Health != null && character.Health.CurrentLifeState.Value == LifeState.Dead)
                {
                    charactersToDespawn.Add(character);
                    continue;
                }

                Vector2Int position = new(Mathf.FloorToInt(character.transform.position.x), Mathf.FloorToInt(character.transform.position.y));

                bool isInActiveArea = false;
                bool isInTimerSafeArea = false;

                foreach (CharacterSpawner spawner in _spawners)
                {
                    if (spawner.ActiveArea.Contains(position))
                        isInActiveArea = true;

                    if (spawner.TimerSafeArea.Contains(position))
                        isInTimerSafeArea = true;

                    if (isInActiveArea && isInTimerSafeArea)
                        break;
                }

                if (!isInActiveArea)
                {
                    charactersToDespawn.Add(character);
                    continue;
                }

                if (isInTimerSafeArea)
                {
                    entry.Value.RemainingTime = _durationTillCharacterDespawns;
                    continue;
                }

                // Decrement timer and check if should despawn
                float remainingTime = entry.Value.RemainingTime - Time.fixedDeltaTime;

                if (remainingTime <= 0f)
                {
                    charactersToDespawn.Add(character);
                }
                else
                {
                    entry.Value.RemainingTime = remainingTime;
                }
            }

            // Cleanly despawn marked characters
            for (int i = 0; i < charactersToDespawn.Count; i++)
            {
                DespawnCharacter(charactersToDespawn[i]);
            }
        }

        public bool TrySpawnCharacter(CharacterSpawner owner, ServerCharacter prefab, Vector2 spawnPosition, out ServerCharacter spawnedCharacter)
        {
            spawnedCharacter = null;
            
            if (!CanSpawnCharacter() || prefab == null)
                return false;

            // Instantiate the prefab on the server
            ServerCharacter character = Instantiate(prefab, spawnPosition, Quaternion.identity);

            if (character == null)
                return false;

            // Spawn over the network via NGO
            if (character.TryGetComponent<NetworkObject>(out var networkObject))
            {
                networkObject.Spawn(destroyWithScene: true);
            }

            // Assign out parameter & track despawn lifetime
            spawnedCharacter = character;
            CharacterLifetime lifetime = new(owner, _durationTillCharacterDespawns);
            _characterDespawnTimers.Add(spawnedCharacter, lifetime);
            Debug.Log($"Spawned {spawnedCharacter.name} at {spawnPosition}!");
            return true;
        }

        public void DespawnCharacter(ServerCharacter character)
        {
            if (character == null)
            {
                _characterDespawnTimers.Remove(null);
                return;
            }
            
            // Unregister from the spawner owner and remove from timer dictionary
            if (_characterDespawnTimers.TryGetValue(character, out CharacterLifetime lifetime))
            {
                lifetime.Owner.UnregisterOwnedCharacter(character);
                _characterDespawnTimers.Remove(character);
            }

            // Despawn across NGO or fallback to Destroy for local objects
            // Debug.Log($"{character.name} despawned.");
            if (character.TryGetComponent<NetworkObject>(out var networkObject) && networkObject.IsSpawned)
            {
                networkObject.Despawn(destroy: true);
            }
            else
            {
                Destroy(character.gameObject);
            }
        }

        // Register and unregister spawners when players enter and leave the world
        public void RegisterSpawner(ServerCharacter host)
        {
            for (int i = 0; i < _spawners.Count; i++)
            {
                if (_spawners[i].Host == host)
                {
                    return; // Already registered, exit early
                }
            }

            CharacterSpawner spawner = new(host);
            _spawners.Add(spawner);
            Debug.Log($"{host} registered as spawner");
        }
        
        public void UnRegisterSpawner(ServerCharacter host)
        {
            for (int i = 0; i < _spawners.Count; i++)
            {
                if (_spawners[i].Host == host)
                {
                    _spawners.RemoveAt(i);
                    Debug.Log($"{host} un registered as spawner");
                    return; // Found and removed, exit early
                }
            }
        }

        public bool CanSpawnCharacter()
        { 
            return _enableSpawning && CurrentCharacterCount < _globalMaxCharacterCap;
        }

        private void DrawRectIntGizmo(RectInt rect)
        {
            Vector3 center = new(rect.center.x, rect.center.y, 0f);
            Vector3 size = new(rect.size.x, rect.size.y, 0f);

            Gizmos.DrawWireCube(center, size);
        }
    }
}
