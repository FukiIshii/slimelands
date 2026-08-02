using UnityEngine;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// 村画面とダンジョン(戦闘)画面を切り替える橋渡し役。村パート・ダンジョンパートどちらの
    /// 内部実装にも依存せず、各画面を構成するコンポーネント/GameObjectの表示切り替えのみを行う
    /// (CLAUDE.mdの疎結合ルールに従う)。戦闘ロジック自体には一切触れない。
    ///
    /// 画面切り替えのタイミングは2つだけ:
    /// - ダンジョンへの出発(StartDungeon)が成立した瞬間 → ダンジョン画面へ自動的に切り替える
    /// - クリア/失敗後、プレイヤーが「村に戻る」を押した瞬間 → 村画面へ切り替える
    /// (クリア/失敗直後は結果を確認できるよう、自動では村画面に戻さない)
    /// </summary>
    public class GameScreenController : MonoBehaviour
    {
        [SerializeField] private DungeonRunController dungeonController;

        [Header("村画面(ダンジョン中は非表示/停止)")]
        [Tooltip("カード盤面上の実際のカード群(見た目)。SetActiveで非表示にする。")]
        [SerializeField] private GameObject cardBoardCardsRoot;
        [Tooltip("CardBoard本体(ドラッグ操作等)。ダンジョン中は無効化して誤操作を防ぐ。")]
        [SerializeField] private Behaviour cardBoardInteraction;
        [SerializeField] private Behaviour villageHudDisplay;
        [SerializeField] private Behaviour gachaUI;
        [SerializeField] private Behaviour partyFormationUI;

        [Header("ダンジョン画面(村では非表示)")]
        [SerializeField] private Behaviour dungeonHudDisplay;
        [Tooltip("DungeonBattleViewが生成するスプライト・エフェクト一式のコンテナ。")]
        [SerializeField] private GameObject dungeonBattleVisualsRoot;

        public bool IsDungeonScreenActive { get; private set; }

        private void OnEnable()
        {
            if (dungeonController != null)
            {
                dungeonController.OnPartySpawned += HandlePartySpawned;
            }

            ApplyVisibility();
        }

        private void OnDisable()
        {
            if (dungeonController != null)
            {
                dungeonController.OnPartySpawned -= HandlePartySpawned;
            }
        }

        private void HandlePartySpawned(System.Collections.Generic.IReadOnlyList<BattleUnit> party)
        {
            ShowDungeonScreen();
        }

        public void ShowDungeonScreen()
        {
            IsDungeonScreenActive = true;
            ApplyVisibility();
        }

        /// <summary>クリア/失敗後、プレイヤーが結果を確認してから村画面へ戻る際に呼ばれる。</summary>
        public void ReturnToVillage()
        {
            IsDungeonScreenActive = false;
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            bool showVillage = !IsDungeonScreenActive;

            SetActiveSafe(cardBoardCardsRoot, showVillage);
            SetEnabledSafe(cardBoardInteraction, showVillage);
            SetEnabledSafe(villageHudDisplay, showVillage);
            SetEnabledSafe(gachaUI, showVillage);
            SetEnabledSafe(partyFormationUI, showVillage);

            SetEnabledSafe(dungeonHudDisplay, IsDungeonScreenActive);
            SetActiveSafe(dungeonBattleVisualsRoot, IsDungeonScreenActive);
        }

        private static void SetActiveSafe(GameObject go, bool active)
        {
            if (go != null)
            {
                go.SetActive(active);
            }
        }

        private static void SetEnabledSafe(Behaviour behaviour, bool enabled)
        {
            if (behaviour != null)
            {
                behaviour.enabled = enabled;
            }
        }
    }
}
