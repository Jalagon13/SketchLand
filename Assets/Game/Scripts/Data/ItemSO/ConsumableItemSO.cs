using FMODUnity;
using UnityEngine;

namespace SketchLand
{
    [CreateAssetMenu(fileName = "New ConsumableItemSO", menuName = "OceanGame/Item/ConsumableItemSO")]
    public class ConsumableItemSO : ItemSO
    {
        public const string PotionSicknessBuffName = "Potion Sickness";

        [field: Header("Health Restoration")]
        [field: Tooltip("Amount of HP this consumable restores.")]
        [field: SerializeField] public int RestoreHpAmount { get; private set; } = 15;

        [field: Header("Potion Sickness Settings")]
        [field: Tooltip("Duration of Potion Sickness debuff in seconds (Terraria default is 60s).")]
        [field: SerializeField] public float PotionSicknessDuration { get; private set; } = 60f;
        
        [field: Tooltip("Icon displayed in BuffUI during the Potion Sickness cooldown.")]
        [field: SerializeField] public Sprite PotionSicknessIcon { get; private set; }

        [field: Header("Feel")]
        [field: SerializeField] public EventReference ConsumeSFX { get; private set; }

        public override string GetDescription()
        {
            string restoreDescription = $"Restores {RestoreHpAmount} HP.";
            return string.IsNullOrWhiteSpace(Description) ? restoreDescription : $"{Description}<br>{restoreDescription}";
        }

        public override void OnSecondaryActionStarted(Player player)
        {
            if (player == null || player.Character == null) return;
            if (player.Character.Health.CurrentLifeState.Value == LifeState.Dead) return;

            // Terraria Check: Deny healing if Potion Sickness is active
            if (player.Character.Stats.HasBuff(PotionSicknessBuffName))
            {
                return;
            }

            // Prevent wasting potions if already at full HP
            int maxHp = player.Character.Stats.MaxHealth.GetValue();
            if (player.Character.Health.CurrentHealth.Value >= maxHp)
            {
                return;
            }

            // Restore Health
            player.Character.Health.RestoreHealth(RestoreHpAmount);

            // Apply Potion Sickness debuff (flat 0 so no stat penalties, timed duration)
            var sicknessBuff = new Buff(
                name: PotionSicknessBuffName,
                targetStat: StatType.MoveSpeed,
                flatAmount: 0,
                percentAmount: 0f,
                duration: PotionSicknessDuration,
                icon: PotionSicknessIcon
            );
            
            player.Character.Stats.StartBuff(sicknessBuff);

            // Deduct 1 potion from the active slot
            AudioManager.Instance.PlayOneShot(ConsumeSFX, Player.Instance.transform.position);
            InventoryInputManager.Instance.GetActiveInvSlot().RemoveFromCurrentAmount(1);
        }
    }
}