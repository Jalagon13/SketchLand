using FMODUnity;
using UnityEngine;

namespace SketchLand
{
    [CreateAssetMenu(fileName = "New Bow", menuName = "OceanGame/Item/BowItemSO")]
    public class BowItemSO : ItemSO
    {
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

        public override void OnSecondaryActionStarted(Player player) => player.RangedHandler.StartSecondaryAction(this);
        public override void OnSecondaryActionHeld(Player player) => player.RangedHandler.HoldSecondaryAction(this);
        public override void OnSecondaryActionRelease(Player player) => player.RangedHandler.ReleaseSecondaryAction(this);
        public override void OnSecondaryActionCancelled(Player player) => player.RangedHandler.CancelCharge();
    }
}
