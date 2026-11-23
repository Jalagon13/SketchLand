using UnityEngine;

namespace SketchLand
{
    [CreateAssetMenu(fileName = "New MeleeWeaponItemSO", menuName = "OceanGame/Item/MeleeWeaponItemSO")]
    public class MeleeWeaponItemSO : SwingItemSO
    {
        public override float GetSwingRate(PlayerFood food) => food != null
            ? food.ApplyMeleeSpeedBonus(SwingsPerSecond) : SwingsPerSecond;

        public override float GetStrikeReach(PlayerFood food) => food != null
            ? food.ApplyMeleeRangeBonus(StrikeReach) : StrikeReach;

        public override int GetMeleeDamage(PlayerFood food) => food != null
            ? food.ApplyMeleeDamageBonus(MeleeDamage) : MeleeDamage;

        public override string GetDescription() => DescribeStats(
            $"<b>Melee Weapon</b><br>Melee damage: {MeleeDamage}<br>Melee speed: {SwingsPerSecond:0.##} swings/sec"
            + $"<br>Melee range: {StrikeReach:0.##}<br>Strike angle: {SwingArcAngle}°<br>Knockback: {KnockbackForce}");
    }
}
