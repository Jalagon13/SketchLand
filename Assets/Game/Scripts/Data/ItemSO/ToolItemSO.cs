using UnityEngine;

namespace SketchLand
{
    [CreateAssetMenu(fileName = "New ToolItemSO", menuName = "OceanGame/Item/ToolItemSO")]
    public class ToolItemSO : SwingItemSO
    {
        [field: Header("Tool Settings")]
        [field: SerializeField] public HarvestType HarvestType { get; private set; } = HarvestType.None;
        [field: SerializeField] public int MiningDamage { get; private set; } = 15;

        public bool CanHarvest(TileConfigSO tile) => tile != null
            && (tile.RequiredHarvestType == HarvestType.None || HarvestType == tile.RequiredHarvestType);

        public override float GetSwingRate(PlayerFood food) => food != null
            ? food.ApplyMiningSpeedBonus(SwingsPerSecond) : SwingsPerSecond;

        public override string GetDescription() => DescribeStats(
            $"<b>Tool</b><br>Mining damage: {MiningDamage}<br>Mining speed: {SwingsPerSecond:0.##} swings/sec"
            + $"<br>Damage: {MeleeDamage}<br>Reach: {StrikeReach:0.##}<br>Strike angle: {SwingArcAngle}°<br>Knockback: {KnockbackForce}");
    }
}
