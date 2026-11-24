using UnityEngine;

namespace SketchLand
{
    public abstract class SwingItemSO : ItemSO
    {
        public override bool CanStartWhilePrimaryHeld => true;

        [field: Header("Swing Settings")]
        [field: Tooltip("Complete swings per second while primary action is held.")]
        [field: SerializeField, Min(0.01f)] public float SwingsPerSecond { get; private set; } = 1f;
        [field: SerializeField, Range(1, 360), InspectorName("Strike Zone Angle")] public int SwingArcAngle { get; private set; } = 120;
        [field: SerializeField, Min(0.01f)] public float StrikeReach { get; private set; } = 1.425f;

        [field: Tooltip("Gameplay impact time as a fraction of the complete swing. Independent of visual clips.")]
        [field: SerializeField, Range(0f, 1f)] public float ImpactFraction { get; private set; } = 0.47f;

        [field: Header("Hit Settings")]
        [field: SerializeField] public int MeleeDamage { get; private set; } = 10;
        [field: SerializeField, Range(0, ServerCharacter.MaxKnockbackForce)] public int KnockbackForce { get; private set; } = 4;

        public virtual float GetSwingRate(PlayerFood food) => SwingsPerSecond;
        public virtual float GetStrikeReach(PlayerFood food) => StrikeReach;
        public virtual int GetMeleeDamage(PlayerFood food) => MeleeDamage;

        public override void OnPrimaryActionStarted(Player player) => StartSwing(player);
        public override void OnPrimaryActionHeld(Player player) => StartSwing(player);

        private void StartSwing(Player player)
        {
            player.Actions.TrySwing(player, this, WorldManager.MouseWorldPosition);
        }

        protected string DescribeStats(string stats) => string.IsNullOrWhiteSpace(Description)
            ? stats : $"{Description}<br>{stats}";
    }
}
