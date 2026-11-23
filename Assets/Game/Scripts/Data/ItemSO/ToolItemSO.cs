using UnityEngine;

namespace SketchLand
{
    [CreateAssetMenu(fileName = "New ToolItemSO", menuName = "OceanGame/Item/ToolItemSO")]
    public class ToolItemSO : SwingItemSO
    {
        [field: Header("Tool Settings")]
        [field: SerializeField] public HarvestType HarvestType { get; private set; } = HarvestType.None;
        [field: SerializeField, Min(1)] public int MiningDamage { get; private set; } = 15;
        [field: Tooltip("Mining damage ticks per second, independent of swing animation speed.")]
        [field: SerializeField, Min(0.01f)] public float MiningTicksPerSecond { get; private set; } = 2f;

        public bool CanHarvest(TileConfigSO tile) => tile != null
            && (tile.RequiredHarvestType == HarvestType.None || HarvestType == tile.RequiredHarvestType);

        public float GetMiningTickRate(PlayerFood food) => food != null
            ? food.ApplyMiningSpeedBonus(MiningTicksPerSecond) : MiningTicksPerSecond;

        public override void OnPrimaryActionStarted(Player player)
        {
            MiningManager.Instance.StartMining(player, this);
            base.OnPrimaryActionStarted(player);
        }

        public override void OnPrimaryActionHeld(Player player)
        {
            MiningManager.Instance.StartMining(player, this);
            base.OnPrimaryActionHeld(player);
        }

        public override void OnPrimaryActionRelease(Player player) => MiningManager.Instance?.StopMining(player, this);
        public override void OnPrimaryActionCancelled(Player player) => MiningManager.Instance?.StopMining(player, this);

        public override string GetDescription() => DescribeStats(
            $"Mining damage: {MiningDamage}<br>Mining speed: {MiningTicksPerSecond:0.##} ticks/sec"
            + $"<br>Damage: {MeleeDamage}<br>Knockback: {KnockbackForce}");
    }
}
