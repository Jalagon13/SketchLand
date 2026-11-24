using UnityEngine;

namespace SketchLand
{
    [CreateAssetMenu(fileName = "New ToolItemSO", menuName = "OceanGame/Item/ToolItemSO")]
    public class ToolItemSO : SwingItemSO
    {
        [field: Header("Tool Settings")]
        [field: SerializeField] public HarvestType HarvestType { get; private set; } = HarvestType.None;
        [field: Tooltip("Each tier above 1 reduces the target's required strikes by one, to a minimum of one strike.")]
        [field: SerializeField, Min(1)] public int Tier { get; private set; } = 1;

        public bool CanHarvest(TileConfigSO tile) => tile != null
            && (tile.RequiredHarvestType == HarvestType.None || HarvestType == tile.RequiredHarvestType);

        public int GetRequiredStrikes(TileConfigSO tile) => Mathf.Max(1,
            tile.BaseStrikesToBreak - (Mathf.Max(1, Tier) - 1));

        public double GetBreakProgressPerStrike(TileConfigSO tile) => 1d / GetRequiredStrikes(tile);

        public override float GetSwingRate(PlayerFood food) => food != null
            ? food.ApplyMiningSpeedBonus(SwingsPerSecond) : SwingsPerSecond;

        public override string GetDescription() => DescribeStats(
            $"Mining tier: {Tier}<br>Mining speed: {SwingsPerSecond:0.##} swings/sec"
            + $"<br>Damage: {MeleeDamage}<br>Knockback: {KnockbackForce}");
    }
}
