using FMODUnity;
using UnityEngine;

namespace SketchLand
{
    [CreateAssetMenu(fileName = "New Bow", menuName = "OceanGame/Item/BowItemSO")]
    public class BowItemSO : ItemSO
    {
        public override bool CanStartWhilePrimaryHeld => true;

        [field: Header("Bow Settings")]
        [field: SerializeField, Min(0.01f)] public float ChargeDuration { get; private set; } = 0.8f;
        [field: SerializeField, Range(0f, 1f)] public float MinimumCharge { get; private set; } = 0.15f;
        [field: SerializeField, Min(0f)] public float MinimumSpeed { get; private set; } = 5f;
        [field: SerializeField, Min(0f)] public float MaximumSpeed { get; private set; } = 22f;
        [field: SerializeField] public AnimationCurve SpeedCurve { get; private set; } = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [field: Header("Feel")]
        [field: Tooltip("Stringless bow sprite used while aiming. The bowstring is rendered separately.")]
        [field: SerializeField] public Sprite DrawnSprite { get; private set; }
        [field: SerializeField] public EventReference ShootSFX { get; private set; }

        public float GetLaunchSpeed(float charge)
        {
            float strength = SpeedCurve.Evaluate(Mathf.Clamp01(charge));
            return Mathf.Lerp(MinimumSpeed, MaximumSpeed, strength);
        }

        public override void OnPrimaryActionStarted(Player player) => player.Actions.StartCharge(player, this, WorldManager.MouseWorldPosition);
        public override void OnPrimaryActionHeld(Player player) { /* Gameplay advances the charge clock. */ }
        public override void OnPrimaryActionRelease(Player player) => player.Actions.ReleaseCharge(this, WorldManager.MouseWorldPosition);
        public override void OnPrimaryActionCancelled(Player player) => player.Actions.Cancel();

        public override bool OnSecondaryActionStarted(Player player)
        {
            if (!player.Actions.IsCharging) return false;
            player.Actions.Cancel();
            return true;
        }
    }
}
