using UnityEngine;
using Slimelands.Slime;

namespace Slimelands.Cards
{
    /// <summary>
    /// スライムカード。既存のSlimeSpeciesDataを参照するのみで、種族データを重複定義しない。
    /// </summary>
    [CreateAssetMenu(fileName = "NewSlimeCard", menuName = "Slimelands/Cards/Slime Card", order = 20)]
    public class SlimeCardData : CardData
    {
        [SerializeField] private SlimeSpeciesData species;

        public SlimeSpeciesData Species => species;
        public override CardCategory Category => CardCategory.Slime;
    }
}
