using UnityEngine;

namespace Slimelands.Cards
{
    /// <summary>資源カード(木材、板材など)。種類ごとに個別のアセットとして作成する(Stacklands踏襲)。</summary>
    [CreateAssetMenu(fileName = "NewResourceCard", menuName = "Slimelands/Cards/Resource Card", order = 10)]
    public class ResourceCardData : CardData
    {
        public override CardCategory Category => CardCategory.Resource;
    }
}
