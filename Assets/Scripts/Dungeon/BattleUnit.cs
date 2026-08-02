using UnityEngine;
using Slimelands.Slime;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// 戦闘中の1体(プレイヤー側・敵側どちらも)の実行時状態。MonoBehaviourではない純粋なC#クラス。
    /// プレイヤー側はSourceIndividualを持ち、戦闘終了時にHPを個体へ書き戻す。
    /// 敵側は新しいデータ構造を作らずSlimeSpeciesDataから都度生成する(SourceIndividualはnull)。
    /// </summary>
    public class BattleUnit
    {
        public string DisplayName { get; }
        public SlimeType Type { get; }
        public SlimeAttackType AttackType { get; }
        public SlimeSpeciesData Species { get; }
        public SlimeStats Stats { get; }
        public bool IsPlayerSide { get; }
        public SlimeIndividual SourceIndividual { get; }
        public SlimeSkillData UltimateSkill { get; }

        public int CurrentHp { get; private set; }
        public float NormalAttackTimer { get; set; }
        public float UltimateCooldownRemaining { get; set; }

        public bool IsFainted => CurrentHp <= 0;
        public bool HasUltimate => UltimateSkill != null;
        public bool CanActivateUltimate => HasUltimate && !IsFainted && UltimateCooldownRemaining <= 0f;

        public BattleUnit(string displayName, SlimeType type, SlimeAttackType attackType, SlimeSpeciesData species,
            SlimeStats stats, bool isPlayerSide, SlimeIndividual sourceIndividual, SlimeSkillData ultimateSkill,
            int startingHp)
        {
            DisplayName = displayName;
            Type = type;
            AttackType = attackType;
            Species = species;
            Stats = stats;
            IsPlayerSide = isPlayerSide;
            SourceIndividual = sourceIndividual;
            UltimateSkill = ultimateSkill;
            CurrentHp = Mathf.Clamp(startingHp, 0, stats.maxHp);
        }

        public void ApplyDamage(int amount)
        {
            CurrentHp = Mathf.Max(0, CurrentHp - Mathf.Max(0, amount));
        }
    }
}
