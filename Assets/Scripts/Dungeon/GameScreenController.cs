using UnityEngine;

namespace Slimelands.Dungeon
{
    /// <summary>画面遷移の状態。村→(ダンジョンツリー→ダンジョン詳細→パーティー編成)→ダンジョン戦闘、の一本道。</summary>
    public enum GameScreen
    {
        Village,
        DungeonTree,
        DungeonDetail,
        PartyFormation,
        DungeonBattle,
    }

    /// <summary>
    /// 村画面・ダンジョンツリー画面・ダンジョン詳細画面・パーティー編成画面・ダンジョン(戦闘)画面を
    /// 切り替える橋渡し役。各パートの内部実装には依存せず、各画面を構成するコンポーネント/GameObjectの
    /// 表示切り替えのみを行う(CLAUDE.mdの疎結合ルールに従う)。新しいUnityシーンには分離せず、
    /// 同一シーン内でGameObjectの表示/非表示を切り替える方式を踏襲する。戦闘ロジック自体には一切触れない。
    ///
    /// 画面遷移の流れ:
    /// 村 → (「ダンジョンへ」) → ダンジョンツリー → (ノードをタップ) → ダンジョン詳細
    ///   → (「挑戦する」) → パーティー編成 → (「出発」、StartDungeonが成立) → ダンジョン戦闘
    ///   → (クリア/失敗後「村に戻る」) → 村
    /// (クリア/失敗直後は結果を確認できるよう、自動では村画面に戻さない)
    /// </summary>
    public class GameScreenController : MonoBehaviour
    {
        [SerializeField] private DungeonRunController dungeonController;

        [Header("村画面")]
        [Tooltip("カード盤面上の実際のカード群(見た目)。SetActiveで非表示にする。")]
        [SerializeField] private GameObject cardBoardCardsRoot;
        [Tooltip("CardBoard本体(ドラッグ操作等)。村画面以外では無効化して誤操作を防ぐ。")]
        [SerializeField] private Behaviour cardBoardInteraction;
        [SerializeField] private Behaviour villageHudDisplay;
        [SerializeField] private Behaviour gachaUI;
        [Tooltip("村画面からダンジョンツリー画面を開くための入り口ボタン(VillageDungeonEntranceUI)。")]
        [SerializeField] private Behaviour dungeonEntranceUI;
        [Tooltip("盤面でカードを選択した際にその説明を表示するUI(CardDescriptionUI)。")]
        [SerializeField] private Behaviour cardDescriptionUI;
        [Tooltip("マウスホイールでのカメラ拡大縮小(CameraZoomController)。村画面以外では無効化する。")]
        [SerializeField] private Behaviour cameraZoomController;

        [Header("ダンジョンツリー/詳細/パーティー編成画面")]
        [SerializeField] private Behaviour dungeonTreeUI;
        [SerializeField] private Behaviour dungeonDetailUI;
        [SerializeField] private Behaviour partyFormationUI;

        [Header("ダンジョン戦闘画面")]
        [SerializeField] private Behaviour dungeonHudDisplay;
        [Tooltip("DungeonBattleViewが生成するスプライト・エフェクト一式のコンテナ。")]
        [SerializeField] private GameObject dungeonBattleVisualsRoot;
        [Tooltip("DungeonBackgroundScrollerが生成する背景タイル一式のコンテナ。")]
        [SerializeField] private GameObject dungeonBackgroundRoot;
        [Tooltip("DungeonPartyHud(画面上部のHP/名前/必殺技発動ボタン)がついたGameObject。SetActiveで" +
            "子のCanvasごと表示/非表示を切り替える必要があるため、他のBehaviour系フィールドと異なりGameObjectで持つ。")]
        [SerializeField] private GameObject dungeonPartyHudRoot;

        public GameScreen CurrentScreen { get; private set; } = GameScreen.Village;

        /// <summary>後方互換用: ダンジョン戦闘画面が表示中かどうか。</summary>
        public bool IsDungeonScreenActive => CurrentScreen == GameScreen.DungeonBattle;

        /// <summary>ダンジョン詳細画面で選択中のダンジョン(タップされたツリーノード)。</summary>
        public DungeonData SelectedDungeonForDetail { get; private set; }

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
            SetScreen(GameScreen.DungeonBattle);
        }

        /// <summary>村画面から、ダンジョンツリー画面を開く。</summary>
        public void ShowDungeonTree()
        {
            SetScreen(GameScreen.DungeonTree);
        }

        /// <summary>ツリー画面でノードをタップし、そのダンジョンの詳細画面を開く。</summary>
        public void ShowDungeonDetail(DungeonData dungeon)
        {
            SelectedDungeonForDetail = dungeon;
            SetScreen(GameScreen.DungeonDetail);
        }

        /// <summary>詳細画面で「挑戦する」を選び、パーティー編成画面を開く。</summary>
        public void ShowPartyFormation()
        {
            SetScreen(GameScreen.PartyFormation);
        }

        /// <summary>後方互換用(HandlePartySpawnedと同じダンジョン戦闘画面への遷移)。</summary>
        public void ShowDungeonScreen()
        {
            SetScreen(GameScreen.DungeonBattle);
        }

        /// <summary>クリア/失敗後、プレイヤーが結果を確認してから村画面へ戻る際に呼ばれる。</summary>
        public void ReturnToVillage()
        {
            SelectedDungeonForDetail = null;
            SetScreen(GameScreen.Village);
        }

        private void SetScreen(GameScreen screen)
        {
            CurrentScreen = screen;
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            bool showVillage = CurrentScreen == GameScreen.Village;
            bool showTree = CurrentScreen == GameScreen.DungeonTree;
            bool showDetail = CurrentScreen == GameScreen.DungeonDetail;
            bool showFormation = CurrentScreen == GameScreen.PartyFormation;
            bool showBattle = CurrentScreen == GameScreen.DungeonBattle;

            SetActiveSafe(cardBoardCardsRoot, showVillage);
            SetEnabledSafe(cardBoardInteraction, showVillage);
            SetEnabledSafe(villageHudDisplay, showVillage);
            SetEnabledSafe(gachaUI, showVillage);
            SetEnabledSafe(dungeonEntranceUI, showVillage);
            SetEnabledSafe(cardDescriptionUI, showVillage);
            SetEnabledSafe(cameraZoomController, showVillage);

            SetEnabledSafe(dungeonTreeUI, showTree);
            SetEnabledSafe(dungeonDetailUI, showDetail);
            SetEnabledSafe(partyFormationUI, showFormation);

            SetEnabledSafe(dungeonHudDisplay, showBattle);
            SetActiveSafe(dungeonBattleVisualsRoot, showBattle);
            SetActiveSafe(dungeonBackgroundRoot, showBattle);
            SetActiveSafe(dungeonPartyHudRoot, showBattle);
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
