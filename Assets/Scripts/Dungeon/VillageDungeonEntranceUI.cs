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

            if (screenController != null)
            {
                screenController.OnReturnedToVillage += HandleReturnedToVillage;
            }
        }

        private void OnDisable()
        {
            if (cardBoard != null)
            {
                cardBoard.OnSlimePlacedOnDungeonGate -= HandleSlimePlacedOnDungeonGate;
            }

            if (screenController != null)
            {
                screenController.OnReturnedToVillage -= HandleReturnedToVillage;
            }
        }

        private void HandleSlimePlacedOnDungeonGate()
        {
            if (screenController != null)
            {
                screenController.ShowDungeonTree();
            }
        }

        /// <summary>
        /// 村画面へ戻ったとき、正門に乗ったままのカードを退避させる(ダンジョン選択画面へ
        /// 遷移する間ずっと正門の上に乗った見た目のままになっているのを解消するため)。
        /// </summary>
        private void HandleReturnedToVillage()
        {
            if (cardBoard != null)
            {
                cardBoard.ReleaseCardsFromDungeonGates();
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
