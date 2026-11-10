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
        private WaitForSeconds _iFrameDuration;

        private void Awake()
        {
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
            CurrentHealth.Value = _character.Stats.MaxHealth.GetValue();
            CurrentLifeState.Value = LifeState.Alive;

            OnCurrentHealthChanged(0, CurrentHealth.Value);
        }

        private void OnCurrentHealthChanged(int previousValue, int newValue)
        {
            OnHealthChanged?.Invoke(this, new(_character.Stats.MaxHealth.GetValue(), CurrentHealth.Value, previousValue));
            
            if (_character.Data.CanDie && CurrentHealth.Value <= 0 && CurrentLifeState.Value != LifeState.Dead)
            {
                CurrentLifeState.Value = LifeState.Dead;
                AudioManager.Instance.PlayOneShot(_character.Data.CharacterDeathSFX, transform.position);

                // Server-authoritative loot drop on death
                if (IsServer && _character != null && _character.Data != null)
                {
                    _character.Data.DropLoot(_character.transform.position);
                }
            }
        }

        public void TakeDamage(int netDamage, bool triggerIFrame = true)
        {
            if (!IsServer || netDamage <= 0) return;
            if (triggerIFrame && CurrentLifeState.Value == LifeState.IFrame) return;

            int minimumHealth = _character.Data.CanDie ? 0 : 1;
            CurrentHealth.Value = Mathf.Max(minimumHealth, CurrentHealth.Value - netDamage);
            
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
        private void NotifyDamagedRpc()
        {
            OnDamaged?.Invoke();
        }

        public void RestoreHealth(int amount)
        {
            if (amount <= 0 || CurrentLifeState.Value == LifeState.Dead) return;

            int maxHp = _character.Stats.MaxHealth.GetValue();
            if (CurrentHealth.Value >= maxHp) return;
            
            WorldTextPopupManager.Instance.SpawnHealingTextPopup(Player.Instance.Character.transform.position, amount);

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

            int maxHp = _character.Stats.MaxHealth.GetValue();
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
