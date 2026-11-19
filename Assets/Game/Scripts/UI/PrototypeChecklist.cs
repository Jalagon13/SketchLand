using TMPro;
using UnityEngine;

namespace SketchLand
{
    // Temporary, scene-local playtest guide. No save data or persistent state.
    public sealed class PrototypeChecklist : MonoBehaviour
    {
        public static PrototypeChecklist Instance { get; private set; }

        [SerializeField] private ItemSO _wood;
        [SerializeField] private ItemSO _workshop;
        [SerializeField] private ItemSO _bow;
        [SerializeField] private ArmorItemSO _ironHelmet;
        [SerializeField] private ArmorItemSO _ironChestplate;
        [SerializeField] private ArmorItemSO _ironBoots;
        [SerializeField] private TMP_Text[] _taskLabels;
        [SerializeField] private TMP_Text _congratulations;

        private static readonly string[] Tasks =
        {
            "Collect wood", "Craft a workshop", "Craft a bow",
            "Put on a full set of iron armor", "Survive one night", "Eat something"
        };

        private readonly bool[] _complete = new bool[Tasks.Length];
        private InventoryManager _inventory;
        private Player _player;
        private PlayerEquipment _equipment;
        private CharacterHealth _health;
        private PlayerFood _food;
        private bool _observedClock;
        private DayPhase _previousPhase;
        private long _nightDay;
        private bool _survivingNight;

        private void Awake()
        {
            Instance = this;
            RefreshUI();
        }

        private void Start()
        {
            _inventory = InventoryManager.Instance;
            _player = Player.Instance;
            if (_inventory != null) _inventory.OnPlayerInventoryChanged += CheckWood;
            if (_player != null)
            {
                _player.PlayerReady += BindCharacter;
                if (_player.Character != null) BindCharacter(_player.Character);
            }
            CraftingSlotUI.OnRecipeCrafted += OnCrafted;
            CheckWood();
        }

        private void OnDestroy()
        {
            if (_inventory != null) _inventory.OnPlayerInventoryChanged -= CheckWood;
            if (_player != null) _player.PlayerReady -= BindCharacter;
            CraftingSlotUI.OnRecipeCrafted -= OnCrafted;
            UnbindCharacter();
            if (Instance == this) Instance = null;
        }

        private void UnbindCharacter()
        {
            if (_equipment != null) _equipment.OnEquipmentChanged -= CheckArmor;
            if (_health != null) _health.CurrentLifeState.OnValueChanged -= OnLifeStateChanged;
            if (_food != null) _food.OnFoodEaten -= OnFoodEaten;
        }

        private void BindCharacter(ServerCharacter character)
        {
            UnbindCharacter();
            _equipment = character.GetComponent<PlayerEquipment>();
            _health = character.Health;
            _food = character.GetComponent<PlayerFood>();
            if (_food != null) _food.OnFoodEaten += OnFoodEaten;
            if (_equipment != null) _equipment.OnEquipmentChanged += CheckArmor;
            if (_health != null) _health.CurrentLifeState.OnValueChanged += OnLifeStateChanged;
            _survivingNight = false;
            _observedClock = false;
            CheckArmor();
        }

        private void CheckWood()
        {
            if (_wood != null && _inventory != null && _inventory.GetTotalItemCount(_wood) > 0)
                Complete(0);
        }

        private void OnFoodEaten(ConsumableItemSO food) => Complete(5);

        private void OnCrafted(RecipeSO recipe)
        {
            if (recipe == null) return;
            if (_workshop != null && recipe.OutputItem == _workshop) Complete(1);
            if (_bow != null && recipe.OutputItem == _bow) Complete(2);
        }

        private void CheckArmor()
        {
            if (_equipment == null || _ironHelmet == null || _ironChestplate == null || _ironBoots == null) return;
            if (_equipment.GetEquippedArmorSO(ArmorType.Helmet) == _ironHelmet &&
                _equipment.GetEquippedArmorSO(ArmorType.Chestplate) == _ironChestplate &&
                _equipment.GetEquippedArmorSO(ArmorType.Pants) == _ironBoots)
                Complete(3);
        }

        private void OnLifeStateChanged(LifeState previous, LifeState current)
        {
            if (current == LifeState.Dead) _survivingNight = false;
        }

        private void Update()
        {
            var world = WorldManager.Instance;
            if (_complete[4] || world == null || !world.IsWorldReady || _health == null) return;
            ObserveNight(world.Clock.CurrentPhase, world.Clock.DayNumber,
                _health.CurrentLifeState.Value != LifeState.Dead);
        }

        private void ObserveNight(DayPhase phase, long day, bool alive)
        {
            if (!alive) _survivingNight = false;
            if (_observedClock && phase != _previousPhase)
            {
                if (phase == DayPhase.Night)
                {
                    _survivingNight = alive;
                    _nightDay = day;
                }
                else if (_previousPhase == DayPhase.Night)
                {
                    if (phase == DayPhase.Sunrise && day == _nightDay + 1 && _survivingNight && alive)
                        Complete(4);
                    _survivingNight = false;
                }
            }
            _previousPhase = phase;
            _observedClock = true;
        }

        private void Complete(int task)
        {
            if (_complete[task]) return;
            _complete[task] = true;
            RefreshUI();
        }

        private void RefreshUI()
        {
            bool allComplete = true;
            for (int i = 0; i < _complete.Length; i++)
            {
                allComplete &= _complete[i];
                if (_taskLabels == null || i >= _taskLabels.Length || _taskLabels[i] == null) continue;
                _taskLabels[i].text = (_complete[i] ? "[x]  " : "[ ]  ") + Tasks[i];
                _taskLabels[i].color = _complete[i] ? new Color(0.55f, 0.88f, 0.6f) : Color.white;
            }
            if (_congratulations != null) _congratulations.gameObject.SetActive(allComplete);
        }
    }
}
