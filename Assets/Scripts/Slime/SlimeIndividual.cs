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
        [Tooltip("生成時(ガチャ排出・合成・配合・進化)に種族のUltimateSkillCandidatesからランダムに" +
            "1つ抽選され、以後変化しない(GDD確定仕様)。候補が空の種族ではnullのまま(必殺技を持たない)。")]
        [SerializeField] private SlimeSkillData assignedUltimate;
        [Tooltip("-1は未初期化(=満タン扱い)を表す。ダンジョンでの気絶(戦闘不能)はHP0の状態として" +
            "ここに保持される(気絶=死亡ではないため個体自体は破棄しない)。専用の状態フラグは持たず、" +
            "常にこの実数値から気絶・編成可否を算出する。")]
        [SerializeField] private int currentHp = -1;

        /// <summary>パーティー編成に必要な最大HP比率。この値未満の間は次のダンジョンに編成できない。</summary>
        private const float PartyEligibleHpRatio = 0.5f;

        public SlimeSpeciesData Species => species;
        public int Level => level;
        public int Exp => exp;
        public SlimeIndividualValues IndividualValues => individualValues;
        public SlimeSkillData AssignedUltimate => assignedUltimate;

        /// <summary>現在のHP。未初期化(-1)の場合は満タン(CalculateCurrentStats().maxHp)を返す。</summary>
        public int CurrentHp => currentHp < 0 ? CalculateCurrentStats().maxHp : Mathf.Min(currentHp, CalculateCurrentStats().maxHp);

        /// <summary>HPが0(戦闘不能=気絶)かどうか。戦闘中の行動可否判定に使う(編成可否とは別)。</summary>
        public bool IsFainted => CurrentHp <= 0;

        /// <summary>
        /// 次のダンジョンに編成できるか(HPが最大HPの50%以上かどうか)。
        /// 「気絶」という専用フラグではなく、常にHPの実数値から判定する。
        /// </summary>
        public bool CanJoinParty => CurrentHp >= CalculateCurrentStats().maxHp * PartyEligibleHpRatio;

        public SlimeIndividual(SlimeSpeciesData species, int level = 1)
        {
            this.species = species;
            this.level = Mathf.Max(1, level);
            individualValues = SlimeIndividualValues.CreateRandom();
            assignedUltimate = DrawRandomUltimate(species);
        }

        /// <summary>
        /// 種族のUltimateSkillCandidatesからランダムに1つ抽選する(候補プール方式、GDD確定仕様)。
        /// 個体生成時(このコンストラクタ)にのみ呼ばれ、以後変化しない。
        /// </summary>
        private static SlimeSkillData DrawRandomUltimate(SlimeSpeciesData species)
        {
            if (species == null)
            {
                return null;
            }

            IReadOnlyList<SlimeSkillData> candidates = species.UltimateSkillCandidates;
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
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

        /// <summary>
        /// 経験値を加算する。種族ごとのExpPerLevel(仮値、要調整)に達するたびレベルが上がる
        /// (一度に大量の経験値が入った場合は複数レベル分まとめて上がる)。
        /// </summary>
        public void AddExp(int amount)
        {
            exp += Mathf.Max(0, amount);

            int required = species.ExpPerLevel;
            while (required > 0 && exp >= required)
            {
                exp -= required;
                level++;
            }
        }

        public void SetLevel(int newLevel)
        {
            level = Mathf.Max(1, newLevel);
        }

#if UNITY_EDITOR
        /// <summary>
        /// テスト専用: AssignedUltimateを指定のスキルへ強制的に差し替える。本来は生成時に
        /// ランダム抽選されたきり変化しない(GDD確定仕様)ため、通常のゲームプレイ経路からは
        /// 呼ばれない。狙ったスキルの個体を確実に用意したいテスト/デバッグ作業のためだけに
        /// 用意する(UNITY_EDITORでのみコンパイルされ、実機ビルドには含まれない)。
        /// </summary>
        public void SetAssignedUltimateForTesting(SlimeSkillData skill)
        {
            assignedUltimate = skill;
        }
#endif
    }
}
