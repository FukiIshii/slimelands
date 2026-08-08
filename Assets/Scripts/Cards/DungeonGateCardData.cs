using UnityEngine;

namespace Slimelands.Cards
{
    /// <summary>
    /// 村からダンジョン選択画面へ遷移するための入り口となる建物カード(正門)。
    /// このカードにスライムカードを重ねると、CardBoardがOnSlimePlacedOnDungeonGateを
    /// 発火し、村・ダンジョン双方の内部実装を知らない橋渡し役(VillageDungeonEntranceUI)が
    /// それを受けてGameScreenController.ShowDungeonTree()を呼ぶ(CLAUDE.mdの疎結合ルールに従う)。
    /// 乗せたスライムカード自体は消費されず、そのまま盤面(このカードの上)に残る。
    /// </summary>
    [CreateAssetMenu(fileName = "NewDungeonGateCard", menuName = "Slimelands/Cards/Dungeon Gate Card", order = 32)]
    public class DungeonGateCardData : BuildingCardData
    {
    }
}
