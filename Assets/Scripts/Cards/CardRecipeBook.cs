using System.Collections.Generic;
using UnityEngine;

namespace Slimelands.Cards
{
    /// <summary>合成レシピの一覧を保持し、カード群の内容から過不足なく一致するレシピを検索するデータベース。</summary>
    [CreateAssetMenu(fileName = "CardRecipeBook", menuName = "Slimelands/Cards/Card Recipe Book", order = 41)]
    public class CardRecipeBook : ScriptableObject
    {
        [SerializeField] private List<CardRecipeData> recipes = new();

        public bool TryFindRecipe(IReadOnlyList<CardData> cards, out CardRecipeData recipe)
        {
            foreach (var candidate in recipes)
            {
                if (candidate != null && candidate.Matches(cards))
                {
                    recipe = candidate;
                    return true;
                }
            }

            recipe = null;
            return false;
        }
    }
}
