using System;
using Unity.Netcode;
using UnityEngine;

namespace SketchLand
{
    [RequireComponent(typeof(ServerCharacter))]
    public class DamageReceiver : NetworkBehaviour
    {
        private ServerCharacter _character;

        private void Awake()
        {
            _character = GetComponent<ServerCharacter>();
        }

        // Any damage enters through here
        public void ReceiveHit(SyncHitData hit)
        {
            if(_character.Health.CurrentLifeState.Value != LifeState.Alive) return;
            
            ReceiveHitServerRpc(hit);
        }
        
        [Rpc(SendTo.Server)]
        private void ReceiveHitServerRpc(SyncHitData hit)
        {
            if(_character.Health.CurrentLifeState.Value != LifeState.Alive) return;
        
            float difficultyMult = 0.5f; // Placeholder for difficulty multiplier, 0.5 for normal, 0.75 for hard, 1 for insane TENT mults
            int defense = Mathf.RoundToInt(_character.Stats.Defense.GetValue() * difficultyMult);
            int netDamage = Mathf.Max(1, hit.Damage - defense); // Clamp it to 1

            _character.Health.TakeDamage(netDamage);
            
            // TEMP: Spawn damage number popup. I will put this code in a client side game feel system later. Same with the SFX
            if(_character == Player.Instance.Character)
            {
                WorldTextPopupManager.Instance.SpawnPlayerDamageNumbers(_character.transform.position, netDamage);
            }
            else
            {
                WorldTextPopupManager.Instance.SpawnCharacterDamageNumbers(_character.transform.position, netDamage);
            }
            
            _character.ApplyKnockback(hit.KnockbackForce, hit.SourcePosition);
        }
    }
    
    public struct SyncHitData : IEquatable<SyncHitData>, INetworkSerializable
    {
        public int Damage;
        [Range(0, ServerCharacter.MaxKnockbackForce)] public int KnockbackForce;
        public Vector2 SourcePosition;
        
        public SyncHitData(int damage, int knockbackForce, Vector2 sourcePosition)
        {
            Damage = damage;
            KnockbackForce = knockbackForce;
            SourcePosition = sourcePosition;
        }

        public bool Equals(SyncHitData other)
        {
            if(Damage != other.Damage || 
                KnockbackForce != other.KnockbackForce || 
                SourcePosition.x != other.SourcePosition.x || 
                SourcePosition.y != other.SourcePosition.y) return false;
                
            return true;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Damage);
            serializer.SerializeValue(ref KnockbackForce);
            serializer.SerializeValue(ref SourcePosition);
        }
    }
}
