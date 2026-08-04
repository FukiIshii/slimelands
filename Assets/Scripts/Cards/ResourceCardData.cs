using UnityEngine;

namespace Slimelands.Cards
{
    /// <summary>資源カード(木材、板材など)。種類ごとに個別のアセットとして作成する(Stacklands踏襲)。</summary>
    [CreateAssetMenu(fileName = "NewResourceCard", menuName = "Slimelands/Cards/Resource Card", order = 10)]
    public class ResourceCardData : CardData
    {
        [Tooltip("納屋にこのカードを与えたときに食料ストックへ加算される量(0なら納屋の食料には使えない)。" +
            "素材ごとに個別のレシピを用意せず、この値をレシピ側(UseIngredientFoodSupplyValue)から参照する。")]
        [SerializeField] private int foodSupplyValue = 0;

        public override CardCategory Category => CardCategory.Resource;
        public int FoodSupplyValue => foodSupplyValue;
    }
}
