using System.Collections.Generic;
using UnityEngine;

namespace Slimelands.Cards
{
    /// <summary>
    /// 「素材の組み合わせ → 結果」の合成レシピ1件。素材は特定のカード指定のほか、
    /// カテゴリが一致すれば種類を問わない指定(例:スライムなら何でも良い)もできる。
    /// </summary>
    [CreateAssetMenu(fileName = "NewCardRecipe", menuName = "Slimelands/Cards/Card Recipe", order = 40)]
    public class CardRecipeData : ScriptableObject
    {
        [Tooltip("制作系: スライムの素材は消費せずその場に残す。合成系: スライムを含む素材を全て消費する。")]
        [SerializeField] private CardRecipeType recipeType = CardRecipeType.Production;
        [SerializeField] private List<CardIngredient> ingredients = new();
        [SerializeField] private CardData result;
        [SerializeField] private int resultCount = 1;
        [Tooltip("合成が完了するまでにかかる時間(秒)。0以下なら即時完了。")]
        [SerializeField] private float craftDuration = 0f;
        [Tooltip("0より大きい場合、結果カードを生成する代わりに村の食料ストックへこの量を加算する" +
            "(例: アイテム+納屋→食料ストック加算)。resultはこの用途では未設定にする。")]
        [SerializeField] private int foodStockAmount = 0;

        public CardRecipeType RecipeType => recipeType;
        public IReadOnlyList<CardIngredient> Ingredients => ingredients;
        public CardData Result => result;
        public int ResultCount => resultCount;
        public float CraftDuration => craftDuration;
        public int FoodStockAmount => foodStockAmount;

        /// <summary>
        /// 与えられたカード群(順不同)が、このレシピの素材と過不足なく一致するか判定する。
        /// カード1枚につき素材枠1つを1対1で割り当てられるかをバックトラッキングで探索する
        /// (「スライムなら何でも良い」のような枠が複数あっても正しく判定できるようにするため)。
        /// </summary>
        public bool Matches(IReadOnlyList<CardData> cards)
        {
            return TryMatch(cards, out _);
        }

        /// <summary>
        /// Matchesと同じ判定に加え、各カード(cards[i])がどの素材枠(ingredients[cardToIngredientIndex[i]])に
        /// 割り当てられたかを返す。完了時にカードごとのIsConsumedを参照するために使う。
        /// </summary>
        public bool TryMatch(IReadOnlyList<CardData> cards, out int[] cardToIngredientIndex)
        {
            cardToIngredientIndex = null;
            if (cards == null || cards.Count != ingredients.Count)
            {
                return false;
            }

            var used = new bool[ingredients.Count];
            var assignment = new int[cards.Count];
            if (!TryAssign(cards, 0, used, assignment))
            {
                return false;
            }

            cardToIngredientIndex = assignment;
            return true;
        }

        private bool TryAssign(IReadOnlyList<CardData> cards, int cardIndex, bool[] used, int[] assignment)
        {
            if (cardIndex >= cards.Count)
            {
                return true;
            }

            for (int i = 0; i < ingredients.Count; i++)
            {
                if (used[i] || !ingredients[i].Matches(cards[cardIndex]))
                {
                    continue;
                }

                used[i] = true;
                assignment[cardIndex] = i;
                if (TryAssign(cards, cardIndex + 1, used, assignment))
                {
                    return true;
                }

                used[i] = false;
            }

            return false;
        }
    }
}
