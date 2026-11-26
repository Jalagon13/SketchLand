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
            $"Damage: {MeleeDamage}<br>Knockback: {KnockbackForce:P0}"
            + $"<br>Swing speed: {SwingsPerSecond:0.##} swings/sec");
    }
}
