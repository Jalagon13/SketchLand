using UnityEngine;

namespace SketchLand
{
    // Local food effects for singleplayer (the game runs as a solo host).
    [RequireComponent(typeof(ServerCharacter))]
    public class PlayerFood : MonoBehaviour
    {
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

        public float BonusMiningSpeedPercentage => SumBonus(food => food.BonusMiningSpeedPercentage);
        public float BonusMeleeDamagePercentage => SumBonus(food => food.BonusMeleeDamagePercentage);
        public float BonusMeleeSpeedPercentage => SumBonus(food => food.BonusMeleeSpeedPercentage);
        public float BonusMeleeRangePercentage => SumBonus(food => food.BonusMeleeRangePercentage);

        private float SumBonus(System.Func<ConsumableItemSO, float> select)
        {
            float total = 0f;
            for (int i = 0; i < Capacity; i++)
            {
                var food = At(i);
                if (food != null) total += select(food);
            }
            return total;
        }

        public float ApplyMiningSpeedBonus(float rate) => rate * (1f + BonusMiningSpeedPercentage * 0.01f);
        public int ApplyMeleeDamageBonus(int damage) => Mathf.RoundToInt(damage * (1f + BonusMeleeDamagePercentage * 0.01f));
        public float ApplyMeleeSpeedBonus(float rate) => rate * (1f + BonusMeleeSpeedPercentage * 0.01f);
        public float ApplyMeleeRangeBonus(float range) => range * (1f + BonusMeleeRangePercentage * 0.01f);

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
