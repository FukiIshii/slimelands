using System.Collections.Generic;
using UnityEngine;
using Slimelands.Cards;

namespace Slimelands.Slime
{
    /// <summary>
    /// スライムの種族(種類)を表すマスターデータ。
    /// 個体ごとに変化するレベル・経験値・才能値/個体値は持たない(SlimeIndividualが担当)。
    /// </summary>
    [CreateAssetMenu(fileName = "NewSlimeSpecies", menuName = "Slimelands/Slime Species", order = 10)]
    public class SlimeSpeciesData : ScriptableObject
    {
        [Header("基本情報")]
        [SerializeField] private string speciesName;
        [SerializeField] private SlimeType slimeType;
        [Tooltip("基本攻撃の方式(GDD確定仕様)。近接=突進、遠距離=スライム弾。ダンジョン戦闘の見た目にも使う。")]
        [SerializeField] private SlimeAttackType attackType = SlimeAttackType.Melee;
        [Tooltip("ダンジョン戦闘中に表示するスプライト。未設定の場合はタイプに応じたプレースホルダー形状で代替する。")]
        [SerializeField] private Sprite battleSprite;

        [Header("基本ステータス(Lv1時点)")]
        [SerializeField] private SlimeStats baseStats;

        [Header("レベルアップ成長値(1レベルごとの加算値)")]
        [SerializeField] private SlimeStats statsGrowthPerLevel;

        [Header("スキル")]
        [SerializeField] private List<SlimeSkillData> learnableSkills = new();
        [Tooltip("同時習得できるスキル数の上限。GDD上「スキルの上限数」は未決定のため種族ごとに調整できるようにしている。")]
        [SerializeField] private int maxSkillSlots = 4;

        [Header("村の食料・売却(仮の値。GDD上「食料値の具体的バランス」は未決定)")]
        [Tooltip("月齢が1つ経過するごとに消費する食料の固定量。")]
        [SerializeField] private int monthlyFoodConsumption = 5;
        [Tooltip("このスライムを売却した際に得られる資源カード(仮の交換レート)。")]
        [SerializeField] private ResourceCardData sellResource;
        [Tooltip("売却時に得られるsellResourceの枚数。")]
        [SerializeField] private int sellAmount = 1;

        [Header("ガチャ(仮の値。排出率のバランスは未調整)")]
        [Tooltip("ガチャでの排出重み。値が大きいほど排出されやすい(低レア)。0以下はガチャの対象外になる。" +
            "例: コモン=100, レア=20, 激レア=3 のように種族間の相対値でレア度を表現する。")]
        [SerializeField] private int gachaWeight = 10;

        public string SpeciesName => speciesName;
        public SlimeType Type => slimeType;
        public SlimeAttackType AttackType => attackType;
        public Sprite BattleSprite => battleSprite;
        public SlimeStats BaseStats => baseStats;
        public SlimeStats StatsGrowthPerLevel => statsGrowthPerLevel;
        public IReadOnlyList<SlimeSkillData> LearnableSkills => learnableSkills;
        public int MaxSkillSlots => maxSkillSlots;
        public int MonthlyFoodConsumption => monthlyFoodConsumption;
        public ResourceCardData SellResource => sellResource;
        public int SellAmount => sellAmount;
        public int GachaWeight => gachaWeight;

        /// <summary>指定レベル時点での、個体差(個体値)を含まない種族ベースのステータス。</summary>
        public SlimeStats CalculateBaseStatsAtLevel(int level)
        {
            int extraLevels = Mathf.Max(0, level - 1);
            return baseStats + statsGrowthPerLevel * extraLevels;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (learnableSkills.Count > maxSkillSlots)
            {
                Debug.LogWarning(
                    $"{name}: 習得可能スキル数({learnableSkills.Count})がmaxSkillSlots({maxSkillSlots})を超えています。",
                    this);
            }
        }
#endif
    }
}
