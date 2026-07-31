using UnityEngine;

namespace Slimelands.Cards
{
    /// <summary>
    /// 建物カード(採石場・伐採場など)。GDD上、施設の詳細仕様は未確定のため最小限の情報のみ持つ。
    /// </summary>
    [CreateAssetMenu(fileName = "NewBuildingCard", menuName = "Slimelands/Cards/Building Card", order = 30)]
    public class BuildingCardData : CardData
    {
        public override CardCategory Category => CardCategory.Building;
    }
}
