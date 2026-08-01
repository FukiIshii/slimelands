using UnityEngine;
using Slimelands.Slime;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// 戦闘ダメージ計算式。具体的な係数はバランス未調整のため仮の値。
    /// </summary>
    public static class BattleFormulas
    {
        /// <summary>
        /// attacker→defenderへの与ダメージを計算する。
        /// 計算式: max(1, attack + power - defense) にタイプ相性倍率を掛けて丸める(最低1)。
        /// </summary>
        public static int CalculateDamage(
            int attackerAttack,
            int defenderDefense,
            int power,
            SlimeType attackerType,
            SlimeType defenderType,
            TypeAffinityData affinityData)
        {
            int baseDamage = Mathf.Max(1, attackerAttack + power - defenderDefense);
            float multiplier = affinityData != null ? affinityData.GetMultiplier(attackerType, defenderType) : 1f;
            return Mathf.Max(1, Mathf.RoundToInt(baseDamage * multiplier));
        }
    }
}
