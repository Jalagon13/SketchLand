using UnityEngine;

namespace SketchLand
{
    [CreateAssetMenu(fileName = "New ToolItemSO", menuName = "OceanGame/Item/ToolItemSO")]
    public class ToolItemSO : ItemSO
    {
        public override bool CanStartWhilePrimaryHeld => true;

        public override string GetDescription()
        {
            string stats = $"Damage: {MeleeDamage}<br>Swing speed: {SwingsPerSecond:0.##} swings/sec<br>Mining damage: {(CanBreakTilesAndObjects ? MiningDamage : 0)}";
            return string.IsNullOrWhiteSpace(Description) ? stats : $"{Description}<br>{stats}";
        }

        public override void OnPrimaryActionStarted(Player player) => StartSwing(player);
        public override void OnPrimaryActionHeld(Player player) => StartSwing(player);

        private void StartSwing(Player player)
        {
            player.ArmHandler.TrySwing(player, this, WorldManager.MouseWorldPosition);
        }

        public bool CanHarvest(TileConfigSO tile) => CanBreakTilesAndObjects && tile != null
            && (tile.RequiredHarvestType == HarvestType.None || HarvestType == tile.RequiredHarvestType);

        [field: Header("Tool Settings")]
        [field: Tooltip("Allows this tool to damage and break tiles and placed objects.")]
        [field: SerializeField] public bool CanBreakTilesAndObjects { get; private set; } = true;
        [field: SerializeField] public HarvestType HarvestType { get; private set; } = HarvestType.None;
        [field: SerializeField] public int MiningDamage { get; private set; } = 15; 

        [field: Header("Melee Settings")]
        [field: SerializeField] public int MeleeDamage { get; private set; } = 10;
        [field: SerializeField, Range(0, ServerCharacter.MaxKnockbackForce)] public int KnockbackForce { get; private set; } = 4;
        [field: SerializeField, Min(0.01f)] public float StrikeReach { get; private set; } = 1.425f;

        [field: Header("Swing Settings")]
        [field: Tooltip("Complete swings per second while attack is held. 2 = half a second per swing.")]
        [field: SerializeField, Min(0.01f)] public float SwingsPerSecond { get; private set; } = 1f;
        [field: SerializeField, Range(1, 360), InspectorName("Strike Zone Angle")] public int SwingArcAngle { get; private set; } = 120;
    }
}
