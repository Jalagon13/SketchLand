using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace SketchLand
{
    [RequireComponent(typeof(ServerCharacter))]
    public class CharacterHealth : NetworkBehaviour
    {
        [HideInInspector]
        public NetworkVariable<int> CurrentHealth = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [HideInInspector]
        public NetworkVariable<LifeState> CurrentLifeState = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        
        public event EventHandler<HealthChangedArgs> OnHealthChanged;
        public event Action OnDamaged;
        // Presentation only. Sent before the normal NPC despawn, never on range despawns.
        public event Action<Vector2> OnDied;
        public class HealthChangedArgs : EventArgs
        {
            public int MaxHP { get; }
            public int CurrentHp { get; }
            public int PreviousHp { get; }
            
            public HealthChangedArgs(int maxHp, int currentHp, int previousHp)
            {
                MaxHP = maxHp;
                CurrentHp = currentHp;
                PreviousHp = previousHp;
            }
        }
        
        private ServerCharacter _character;
        private PlayerFood _food;
        public int Maximum => _character.Stats.MaxHealth.GetValue() + (_food != null ? _food.HealthBonus : 0);
        private WaitForSeconds _iFrameDuration;
        private Vector2? _pendingDeathVelocity;

        private void Awake()
        {
            _food = GetComponent<PlayerFood>();
            _character = GetComponent<ServerCharacter>();
            _iFrameDuration = new(_character.Data.BaseIFrameDuration);
        }

        public override void OnDestroy()
        {
            CurrentHealth.OnValueChanged -= OnCurrentHealthChanged;
            
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            CurrentHealth.OnValueChanged += OnCurrentHealthChanged;
            CurrentHealth.Value = Maximum;
            CurrentLifeState.Value = LifeState.Alive;

            OnCurrentHealthChanged(0, CurrentHealth.Value);
        }

        private void OnCurrentHealthChanged(int previousValue, int newValue)
        {
            OnHealthChanged?.Invoke(this, new(Maximum, CurrentHealth.Value, previousValue));
            
            if (_character.Data.CanDie && CurrentHealth.Value <= 0 && CurrentLifeState.Value != LifeState.Dead)
            {
                Vector2 deathVelocity = _pendingDeathVelocity ?? _character.Velocity;
                CurrentLifeState.Value = LifeState.Dead;
                NotifyDeathRpc(deathVelocity);
                AudioManager.Instance.PlayOneShot(_character.Data.CharacterDeathSFX, transform.position);

                // Server-authoritative loot drop on death
                if (IsServer && _character != null && _character.Data != null)
                {
                    _character.Data.DropLoot(_character.transform.position);
                }
            }
        }

        public void TakeDamage(int netDamage, bool triggerIFrame = true, Vector2? deathVelocity = null)
        {
            if (!IsServer || netDamage <= 0) return;
            if (triggerIFrame && CurrentLifeState.Value == LifeState.IFrame) return;

            int minimumHealth = _character.Data.CanDie ? 0 : 1;
            try
            {
                _pendingDeathVelocity = deathVelocity;
                CurrentHealth.Value = Mathf.Max(minimumHealth, CurrentHealth.Value - netDamage);
            }
            finally { _pendingDeathVelocity = null; }
            
            if(CurrentHealth.Value > 0)
            {
                AudioManager.Instance.PlayOneShot(_character.Data.CharacterDamagedSFX, transform.position);
                NotifyDamagedRpc();
            }

            if (triggerIFrame && CurrentLifeState.Value == LifeState.Alive)
            {
                StartCoroutine(IFrameRoutine());
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void NotifyDeathRpc(Vector2 velocity)
        {
            OnDied?.Invoke(velocity);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void NotifyDamagedRpc()
        {
            OnDamaged?.Invoke();
        }

        public void RestoreHealth(int amount)
        {
            if (amount <= 0 || CurrentLifeState.Value == LifeState.Dead) return;

            int maxHp = Maximum;
            if (CurrentHealth.Value >= maxHp) return;
            
            if (IsServer)
            {
                CurrentHealth.Value = Mathf.Min(maxHp, CurrentHealth.Value + amount);
                // Debug.Log($"{name} Restored {amount} HP. {CurrentHealth.Value}/{maxHp}");
            }
            else
            {
                RestoreHealthServerRpc(amount);
            }
        }

        [Rpc(SendTo.Server)]
        private void RestoreHealthServerRpc(int amount)
        {
            if (amount <= 0 || CurrentLifeState.Value == LifeState.Dead) return;

            int maxHp = Maximum;
            CurrentHealth.Value = Mathf.Min(maxHp, CurrentHealth.Value + amount);
            // Debug.Log($"{name} Restored {amount} HP on Server. {CurrentHealth.Value}/{maxHp}");
        }

        private IEnumerator IFrameRoutine()
        {
            CurrentLifeState.Value = LifeState.IFrame;
            yield return _iFrameDuration;
            CurrentLifeState.Value = LifeState.Alive;
        }
    }
    
    
}
