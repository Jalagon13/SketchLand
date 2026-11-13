using System;
using Unity.Netcode;
using UnityEngine;

namespace SketchLand
{
    // Inventory belongs to the owning client. Replicate its three food choices so
    // the server can apply the same health cap and regeneration on remote players.
    public class PlayerFood : NetworkBehaviour
    {
        public const int Capacity = 3;
        [field: SerializeField, Min(1)] public int BaseHealth { get; private set; } = 50;
        [SerializeField, Min(0)] private float _baseHealthRegen = 1;
        private NetworkList<Meal> _meals;
        private ServerCharacter _character;
        private float _healing;

        public struct Meal : INetworkSerializable, IEquatable<Meal>
        {
            public ushort Item;
            public double End;
            public bool Equals(Meal other) => Item == other.Item && End == other.End;
            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Item);
                serializer.SerializeValue(ref End);
            }
        }

        private void Awake()
        {
            _character = GetComponent<ServerCharacter>();
            _meals = new NetworkList<Meal>(writePerm: NetworkVariableWritePermission.Owner);
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;
            while (_meals.Count < Capacity) _meals.Add(default);
            Player.Instance.BindCharacter(_character);
        }

        public float Remaining(int index) => index < _meals.Count
            ? Mathf.Max(0, (float)(_meals[index].End - NetworkManager.ServerTime.Time)) : 0;

        public ConsumableItemSO At(int index) => Remaining(index) > 0
            ? GameDataRegistry.Instance.GetItemSOFromItemId(_meals[index].Item) as ConsumableItemSO : null;

        private int AvailableSlot(ConsumableItemSO food)
        {
            int empty = -1;
            for (int i = 0; i < _meals.Count; i++)
            {
                var active = At(i);
                if (active == food) return -1;
                if (active == null && empty < 0) empty = i;
            }
            return empty;
        }

        public bool CanEat(ConsumableItemSO food) => IsSpawned && IsOwner && food != null &&
            _character.Health.CurrentLifeState.Value != LifeState.Dead && AvailableSlot(food) >= 0;

        public void Eat(InventorySlot slot, ConsumableItemSO food)
        {
            if (slot.IsEmpty || slot.GetItemSO() != food || !CanEat(food)) return;
            _meals[AvailableSlot(food)] = new Meal { Item = slot.ItemId, End = NetworkManager.ServerTime.Time + food.Duration };
            slot.RemoveFromCurrentAmount(1);
            ApplyLimits();
            InventoryManager.Instance.RefreshInventory();
            if (!food.ConsumeSFX.IsNull) AudioManager.Instance.PlayOneShot(food.ConsumeSFX, transform.position);
        }

        public void Remove(int index)
        {
            if (!IsOwner || index < 0 || index >= _meals.Count) return;
            _meals[index] = default;
            ApplyLimits();
        }

        public int HealthBonus
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _meals.Count; i++) total += At(i)?.Health ?? 0;
                return total;
            }
        }

        private void Update()
        {
            if (!IsSpawned || (!IsServer && !IsOwner)) return;
            ApplyLimits();
            if (!IsServer) return;
            var health = _character.Health;
            if (health.CurrentLifeState.Value == LifeState.Dead || health.CurrentHealth.Value >= health.Maximum)
            {
                _healing = 0;
                return;
            }
            float rate = _baseHealthRegen;
            for (int i = 0; i < _meals.Count; i++) rate += At(i)?.HealthRegen ?? 0;
            _healing += rate * Time.deltaTime;
            int whole = Mathf.FloorToInt(_healing);
            if (whole > 0)
            {
                _healing -= whole;
                health.RestoreHealth(whole);
            }
        }

        private void ApplyLimits()
        {
            if (IsOwner)
            {
                float max = 0;
                for (int i = 0; i < _meals.Count; i++)
                {
                    var food = At(i);
                    if (food == null) continue;
                    max += food.Stamina;
                }
                Player.Instance.Stamina.SetFoodMaxBonus(max);
            }
            if (IsServer && _character.Health.CurrentHealth.Value > _character.Health.Maximum)
                _character.Health.CurrentHealth.Value = _character.Health.Maximum;
        }
    }
}
