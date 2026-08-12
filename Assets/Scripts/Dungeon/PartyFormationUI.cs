using System.Collections.Generic;
using UnityEngine;
using Slimelands.Slime;
using Slimelands.Village;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// 村の在籍スライム(VillageSlimeRoster)からパーティーを編成し、ダンジョン
    /// (DungeonRunController)へ出発するための最小限のUI(IMGUI)。
    /// 村パート・ダンジョンパートどちらの内部実装にも依存せず、双方の公開APIを
    /// 呼び出すだけの橋渡し役として実装する(CLAUDE.mdの疎結合ルールに従う)。
    /// 選択状態の操作は全て公開メソッドとして切り出してあり、OnGUIのボタンからだけでなく
    /// コードからも同じ経路でテストできる。
    /// </summary>
    public class PartyFormationUI : MonoBehaviour
    {
        [SerializeField] private VillageSlimeRoster roster;
        [SerializeField] private DungeonRunController dungeonController;
        [Tooltip("全ダンジョンのマスターリスト。DungeonProgressManagerもこのリストを参照して" +
            "ツリーの解放判定を行うため、ここに登録したものが「存在する全ダンジョン」になる。")]
        [SerializeField] private List<DungeonData> availableDungeons = new();
        [Tooltip("ダンジョンの解放/クリア状態を判定する。未設定の場合は全ダンジョンを解放済み扱いにする" +
            "(ツリー画面を経由しない旧来のフローでも動作を維持するため)。")]
        [SerializeField] private DungeonProgressManager progressManager;
        [Tooltip("村に戻る/前の画面(ダンジョン詳細)に戻るための画面切り替え役。未設定でも動作する。")]
        [SerializeField] private GameScreenController screenController;

        private readonly HashSet<SlimeIndividual> selectedParty = new();
        private Vector2 scroll;

        public DungeonData SelectedDungeon { get; private set; }
        public IReadOnlyCollection<SlimeIndividual> SelectedParty => selectedParty;
        /// <summary>全ダンジョンのマスターリスト。DungeonProgressManagerが解放判定・ツリー表示に使う。</summary>
        public IReadOnlyList<DungeonData> AvailableDungeons => availableDungeons;

        public void SelectDungeon(DungeonData dungeon)
        {
            SelectedDungeon = dungeon;
        }

        /// <summary>指定したスライムの編成状態を切り替える(選択中なら外す、未選択なら加える)。
        /// HP不足(CanJoinParty=false)の個体は追加できない。また、選択中のダンジョンの
        /// RequiredPartySize(参加体数の上限)に達している場合はそれ以上追加できない。</summary>
        public bool TogglePartyMember(SlimeIndividual slime)
        {
            if (slime == null)
            {
                return false;
            }

            if (selectedParty.Remove(slime))
            {
                return true;
            }

            if (!slime.CanJoinParty)
            {
                return false;
            }

            if (SelectedDungeon != null && selectedParty.Count >= SelectedDungeon.RequiredPartySize)
            {
                return false;
            }

            selectedParty.Add(slime);
            return true;
        }

        /// <summary>選択中のダンジョン・パーティーで出発を試みる。ダンジョン未選択/0体ならfalse。</summary>
        public bool TryDepart()
        {
            if (SelectedDungeon == null || selectedParty.Count == 0 || dungeonController == null)
            {
                return false;
            }

            var party = new List<SlimeIndividual>(selectedParty);
            dungeonController.StartDungeon(SelectedDungeon, party);
            selectedParty.Clear();
            return true;
        }

        private bool IsFormationVisible()
        {
            if (dungeonController == null)
            {
                return false;
            }

            DungeonState state = dungeonController.State;
            return state == DungeonState.NotStarted || state == DungeonState.Cleared || state == DungeonState.Failed;
        }

        private void OnGUI()
        {
            if (roster == null || dungeonController == null || !IsFormationVisible())
            {
                return;
            }

            GUI.Box(new Rect(440f, 10f, 340f, 560f), string.Empty);
            GUILayout.BeginArea(new Rect(450f, 16f, 320f, 548f));

            if (screenController != null && GUILayout.Button("< ダンジョン詳細に戻る", GUILayout.Width(160f)))
            {
                screenController.ShowDungeonDetail(SelectedDungeon);
            }

            GUILayout.Label("--- 挑戦するダンジョン ---");
            if (SelectedDungeon != null)
            {
                bool cleared = progressManager != null && progressManager.IsCleared(SelectedDungeon);
                GUILayout.Label($"{SelectedDungeon.DungeonName}(必要{SelectedDungeon.RequiredPartySize}体)" +
                    (cleared ? " [クリア済]" : string.Empty));
            }
            else
            {
                GUILayout.Label("(ダンジョンが選択されていません)");
            }

            GUILayout.Space(8f);
            GUILayout.Label("--- 在籍スライム(タップで編成に追加/除外) ---");
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(260f));
            bool atCap = SelectedDungeon != null && selectedParty.Count >= SelectedDungeon.RequiredPartySize;
            foreach (SlimeIndividual slime in roster.Slimes)
            {
                bool eligible = slime.CanJoinParty;
                bool isSelected = selectedParty.Contains(slime);
                int maxHp = slime.CalculateCurrentStats().maxHp;
                string label = $"{slime.Species.SpeciesName} Lv{slime.Level} HP {slime.CurrentHp}/{maxHp}";
                if (!eligible)
                {
                    label += "(HP不足のため編成不可)";
                }
                else if (!isSelected && atCap)
                {
                    label += "(参加人数の上限のため編成不可)";
                }

                GUI.enabled = isSelected || (eligible && !atCap);
                bool toggled = GUILayout.Toggle(isSelected, label);
                GUI.enabled = true;

                if (toggled != isSelected)
                {
                    TogglePartyMember(slime);
                }
            }
            GUILayout.EndScrollView();

            GUILayout.Space(8f);
            int required = SelectedDungeon != null ? SelectedDungeon.RequiredPartySize : 0;
            GUILayout.Label($"選択中: {selectedParty.Count}体 / 参加人数: {required}体");

            GUI.enabled = SelectedDungeon != null && selectedParty.Count > 0;
            if (GUILayout.Button("出発", GUILayout.Height(32f)))
            {
                TryDepart();
            }
            GUI.enabled = true;

            GUILayout.EndArea();
        }
    }
}
