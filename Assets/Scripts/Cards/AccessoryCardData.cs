using UnityEngine;
using Slimelands.Slime;

namespace Slimelands.Cards
{
    /// <summary>
    /// アクセサリーカード。スライムに装備させることで、装備している間だけ有効なステータス補正
    /// (個体値強化とは別枠の一時的な補正、SlimeIndividual.CalculateCurrentStatsで加算される)を持つ。
    /// 「特殊効果」の具体的な種類はGDD未確定のため、今回はステータス補正のみ実装している
    /// (docs/progress_report.md参照)。
    /// </summary>
    [CreateAssetMenu(fileName = "NewAccessoryCard", menuName = "Slimelands/Cards/Accessory Card", order = 25)]
    public class AccessoryCardData : CardData
    {
        [Tooltip("装備している間だけスライムのステータスに加算される補正値。")]
        [SerializeField] private SlimeStats statBonus;

        public override CardCategory Category => CardCategory.Accessory;
        public SlimeStats StatBonus => statBonus;
    }
}
