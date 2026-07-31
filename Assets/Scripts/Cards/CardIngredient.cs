using System;
using UnityEngine;

namespace Slimelands.Cards
{
    /// <summary>
    /// レシピの素材1枠。特定の1種類のカードを指定するか、カテゴリさえ一致すれば
    /// 種類を問わない指定(例:スライムなら何でも良い)かのどちらかを表す。
    /// </summary>
    [Serializable]
    public class CardIngredient
    {
        public enum MatchMode
        {
            SpecificCard,
            AnyOfCategory,
        }

        [SerializeField] private MatchMode mode = MatchMode.SpecificCard;
        [SerializeField] private CardData specificCard;
        [SerializeField] private CardCategory category;
        [Tooltip("falseにすると、このカードは合成完了時に消費されずその場に残る(施設やスライムなど)。")]
        [SerializeField] private bool isConsumed = true;

        public MatchMode Mode => mode;
        public CardData SpecificCard => specificCard;
        public CardCategory Category => category;
        public bool IsConsumed => isConsumed;

        public bool Matches(CardData card)
        {
            if (card == null)
            {
                return false;
            }

            return mode == MatchMode.SpecificCard
                ? card == specificCard
                : card.Category == category;
        }
    }
}
