using UnityEngine;

namespace SketchLand
{
    // Local food effects for singleplayer (the game runs as a solo host).
    [RequireComponent(typeof(ServerCharacter))]
    public class PlayerFood : MonoBehaviour
    {
        public event System.Action<ConsumableItemSO> OnFoodEaten;

        public const int Capacity = 3;
        public int BaseHealth => _character.Data.BaseMaxHealth;
        private readonly Meal[] _meals = new Meal[Capacity];
        private ServerCharacter _character;
        private float _healing;

        private struct Meal
        {
            public ConsumableItemSO Food;
            public float End;
        }

        private bool IsSinglePlayer => _character.IsSpawned && _character.IsServer && _character.IsOwner &&
            _character.NetworkManager.ConnectedClientsIds.Count == 1;

        private void Awake() => _character = GetComponent<ServerCharacter>();

        public float Remaining(int index) => IsSinglePlayer && index >= 0 && index < Capacity
            ? Mathf.Max(0, _meals[index].End - Time.time) : 0;

        public ConsumableItemSO At(int index) => Remaining(index) > 0 ? _meals[index].Food : null;

        private int AvailableSlot(ConsumableItemSO food)
        {
            int empty = -1;
            for (int i = 0; i < Capacity; i++)
            {
                var active = At(i);
                if (active == food) return -1;
                if (active == null && empty < 0) empty = i;
            }
            return empty;
        }

        public bool CanEat(ConsumableItemSO food) => IsSinglePlayer && food != null &&
            _character.Health.CurrentLifeState.Value != LifeState.Dead && AvailableSlot(food) >= 0;

        public void Eat(InventorySlot slot, ConsumableItemSO food)
        {
            if (slot == null || slot.IsEmpty || slot.GetItemSO() != food || !CanEat(food)) return;
            _meals[AvailableSlot(food)] = new Meal { Food = food, End = Time.time + food.Duration };
            slot.RemoveFromCurrentAmount(1);
            OnFoodEaten?.Invoke(food);
            ApplyLimits();
            InventoryManager.Instance.RefreshInventory();
            if (!food.ConsumeSFX.IsNull) AudioManager.Instance.PlayOneShot(food.ConsumeSFX, transform.position);
        }

        public void Remove(int index)
        {
            if (!IsSinglePlayer || index < 0 || index >= Capacity) return;
            _meals[index] = default;
            ApplyLimits();
        }

        public int HealthBonus
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Capacity; i++) total += At(i)?.Health ?? 0;
                return total;
            }
        }

        public float BonusDamagePercentage
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < Capacity; i++) total += At(i)?.BonusDamagePercentage ?? 0f;
                return total;
            }
        }

        public int ApplyDamageBonus(int damage) => Mathf.RoundToInt(damage * (1f + BonusDamagePercentage * 0.01f));

        public float BonusSwingSpeedPercentage
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < Capacity; i++) total += At(i)?.BonusSwingSpeedPercentage ?? 0f;
                return total;
            }
        }

        public float ApplySwingSpeedBonus(float swingsPerSecond) => swingsPerSecond * (1f + BonusSwingSpeedPercentage * 0.01f);

        private void Update()
        {
            if (!IsSinglePlayer) return;
            ApplyLimits();
            var health = _character.Health;
            if (health.CurrentLifeState.Value == LifeState.Dead || health.CurrentHealth.Value >= health.Maximum)
            {
                _healing = 0;
                return;
            }
            float rate = Player.Instance.BaseHealthRegen;
            for (int i = 0; i < Capacity; i++) rate += At(i)?.HealthRegen ?? 0;
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
            if (_character.Health.CurrentHealth.Value > _character.Health.Maximum)
                _character.Health.CurrentHealth.Value = _character.Health.Maximum;
        }
    }
}
