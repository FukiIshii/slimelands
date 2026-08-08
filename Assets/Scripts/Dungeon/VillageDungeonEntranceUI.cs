using UnityEngine;
using Slimelands.Cards;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// 村画面からダンジョンツリー画面へ遷移するための入り口(IMGUIボタン、および正門
    /// (DungeonGateCardData)にスライムカードを乗せる操作)。村パートの内部実装には
    /// 依存せず、CardBoardのイベントを購読してGameScreenControllerの公開APIを呼ぶだけの
    /// 橋渡し役(CLAUDE.mdの疎結合ルールに従う。DungeonRewardHandlerと同じパターン)。
    /// </summary>
    public class VillageDungeonEntranceUI : MonoBehaviour
    {
        [SerializeField] private GameScreenController screenController;
        [Tooltip("正門にスライムを乗せたときの遷移用に購読するCardBoard(未設定ならボタンのみ動作)。")]
        [SerializeField] private CardBoard cardBoard;

        private void OnEnable()
        {
            if (cardBoard != null)
            {
                cardBoard.OnSlimePlacedOnDungeonGate += HandleSlimePlacedOnDungeonGate;
            }
        }

        private void OnDisable()
        {
            if (cardBoard != null)
            {
                cardBoard.OnSlimePlacedOnDungeonGate -= HandleSlimePlacedOnDungeonGate;
            }
        }

        private void HandleSlimePlacedOnDungeonGate()
        {
            if (screenController != null)
            {
                screenController.ShowDungeonTree();
            }
        }

        private void OnGUI()
        {
            if (screenController == null)
            {
                return;
            }

            if (GUI.Button(new Rect(10f, 580f, 140f, 32f), "ダンジョンへ"))
            {
                screenController.ShowDungeonTree();
            }
        }
    }
}
