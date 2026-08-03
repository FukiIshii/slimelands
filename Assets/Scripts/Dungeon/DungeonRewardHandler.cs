using UnityEngine;
using Slimelands.Cards;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// ダンジョンクリア時に、そのダンジョンのDropRewardsを村のCardBoardへ実際に出現させる橋渡し役。
    /// DungeonRunController/CardBoardどちらの内部実装にも依存せず、双方の公開APIを呼ぶだけ
    /// (CLAUDE.mdの村・ダンジョン疎結合ルールに従う。PartyFormationUIと同じ橋渡しパターン)。
    /// </summary>
    public class DungeonRewardHandler : MonoBehaviour
    {
        [SerializeField] private DungeonRunController dungeonController;
        [SerializeField] private CardBoard cardBoard;
        [Tooltip("ドロップした資源カードを配置する盤面上の座標(仮値)。")]
        [SerializeField] private Vector3 spawnPosition = new(-4f, 4f, 0f);

        private void OnEnable()
        {
            if (dungeonController != null)
            {
                dungeonController.OnDungeonCleared += HandleDungeonCleared;
            }
        }

        private void OnDisable()
        {
            if (dungeonController != null)
            {
                dungeonController.OnDungeonCleared -= HandleDungeonCleared;
            }
        }

        private void HandleDungeonCleared()
        {
            if (cardBoard == null || dungeonController == null)
            {
                return;
            }

            DungeonData cleared = dungeonController.CurrentDungeon;
            if (cleared == null)
            {
                return;
            }

            foreach (DungeonDropEntry entry in cleared.DropRewards)
            {
                if (entry == null || entry.Resource == null || entry.Amount <= 0)
                {
                    continue;
                }

                for (int i = 0; i < entry.Amount; i++)
                {
                    cardBoard.SpawnStack(entry.Resource, spawnPosition);
                }
            }
        }
    }
}
