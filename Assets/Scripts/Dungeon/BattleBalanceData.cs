using UnityEngine;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// 戦闘の基本バランス値。数値バランスは未調整のため仮値。コード変更なしに調整できるよう
    /// ScriptableObjectにまとめてある。
    /// </summary>
    [CreateAssetMenu(fileName = "NewBattleBalance", menuName = "Slimelands/Dungeon/Battle Balance Data", order = 62)]
    public class BattleBalanceData : ScriptableObject
    {
        [Tooltip("通常攻撃が自動で発生する間隔(秒)。プレイヤー側・敵側で共通(仮)。")]
        [SerializeField] private float normalAttackInterval = 1.5f;
        [Tooltip("通常攻撃の技威力(SlimeSkillData.Powerと同じ単位)。")]
        [SerializeField] private int normalAttackPower = 10;

        public float NormalAttackInterval => normalAttackInterval;
        public int NormalAttackPower => normalAttackPower;
    }
}
