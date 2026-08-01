using System;
using System.Collections.Generic;
using UnityEngine;

namespace Slimelands.Slime
{
    /// <summary>
    /// プレイヤーが所持する個々のスライムの状態(ランタイム/セーブデータ)。
    /// ScriptableObjectにしない理由: ScriptableObjectはアセットであり全参照で共有されるため、
    /// レベルや経験値のような個体ごとに異なる可変データを持たせると個体間で値が衝突する。
    /// 種族の不変データはSlimeSpeciesData側を参照する。
    /// </summary>
    [Serializable]
    public class SlimeIndividual
    {
        [SerializeField] private SlimeSpeciesData species;
        [SerializeField] private int level = 1;
        [SerializeField] private int exp;
        [SerializeField] private SlimeIndividualValues individualValues;
        [SerializeField] private List<SlimeSkillData> learnedSkills = new();
        [Tooltip("-1は未初期化(=満タン扱い)を表す。ダンジョンでの気絶はHP0の状態としてここに保持され、" +
            "回復するまで次のダンジョンに編成できない(気絶=死亡ではないため個体自体は破棄しない)。")]
        [SerializeField] private int currentHp = -1;

        public SlimeSpeciesData Species => species;
        public int Level => level;
        public int Exp => exp;
        public SlimeIndividualValues IndividualValues => individualValues;
        public IReadOnlyList<SlimeSkillData> LearnedSkills => learnedSkills;

        /// <summary>現在のHP。未初期化(-1)の場合は満タン(CalculateCurrentStats().maxHp)を返す。</summary>
        public int CurrentHp => currentHp < 0 ? CalculateCurrentStats().maxHp : Mathf.Min(currentHp, CalculateCurrentStats().maxHp);

        /// <summary>HPが0(気絶状態)かどうか。気絶中はダンジョンに編成できない。</summary>
        public bool IsFainted => CurrentHp <= 0;

        public SlimeIndividual(SlimeSpeciesData species, int level = 1)
        {
            this.species = species;
            this.level = Mathf.Max(1, level);
            individualValues = SlimeIndividualValues.CreateRandom();
        }

        /// <summary>ダンジョン終了時、戦闘結果の残りHPを個体の永続状態に反映する。</summary>
        public void ApplyDungeonResultHp(int remainingHp)
        {
            currentHp = Mathf.Clamp(remainingHp, 0, CalculateCurrentStats().maxHp);
        }

        /// <summary>指定量だけHPを回復させる(神殿などの回復手段から呼ばれる想定。上限は超えない)。</summary>
        public void Heal(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            currentHp = Mathf.Min(CalculateCurrentStats().maxHp, CurrentHp + amount);
        }

        /// <summary>種族の成長値+個体値を反映した現在の実ステータス。</summary>
        public SlimeStats CalculateCurrentStats()
        {
            SlimeStats stats = species.CalculateBaseStatsAtLevel(level);
            stats.maxHp += individualValues.hp;
            stats.attack += individualValues.attack;
            stats.defense += individualValues.defense;
            stats.speed += individualValues.speed;
            return stats;
        }

        public bool LearnSkill(SlimeSkillData skill)
        {
            if (skill == null || learnedSkills.Contains(skill))
            {
                return false;
            }

            if (learnedSkills.Count >= species.MaxSkillSlots)
            {
                return false;
            }

            learnedSkills.Add(skill);
            return true;
        }

        /// <summary>
        /// 経験値を加算する。レベルアップの必要経験値テーブル/マナ鉱石消費との関係はGDD上未決定のため、
        /// ここではレベル自動計算は行わない。
        /// </summary>
        public void AddExp(int amount)
        {
            exp += Mathf.Max(0, amount);
        }

        public void SetLevel(int newLevel)
        {
            level = Mathf.Max(1, newLevel);
        }
    }
}
