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
        [Tooltip("選択候補として表示するダンジョン一覧。")]
        [SerializeField] private List<DungeonData> availableDungeons = new();

        private readonly HashSet<SlimeIndividual> selectedParty = new();
        private Vector2 scroll;

        public DungeonData SelectedDungeon { get; private set; }
        public IReadOnlyCollection<SlimeIndividual> SelectedParty => selectedParty;

        public void SelectDungeon(DungeonData dungeon)
        {
            SelectedDungeon = dungeon;
        }

        /// <summary>指定したスライムの編成状態を切り替える(選択中なら外す、未選択なら加える)。
        /// HP不足(CanJoinParty=false)の個体は追加できない。</summary>
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

            GUILayout.Label("--- ダンジョン選択 ---");
            foreach (DungeonData dungeon in availableDungeons)
            {
                if (dungeon == null)
                {
                    continue;
                }

                bool isSelected = dungeon == SelectedDungeon;
                string label = (isSelected ? "[選択中] " : string.Empty) +
                    $"{dungeon.DungeonName}(必要{dungeon.RequiredPartySize}体)";
                if (GUILayout.Button(label))
                {
                    SelectDungeon(dungeon);
                }
            }

            GUILayout.Space(8f);
            GUILayout.Label("--- 在籍スライム(タップで編成に追加/除外) ---");
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(260f));
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

                GUI.enabled = eligible || isSelected;
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
            GUILayout.Label($"選択中: {selectedParty.Count}体 / 必要: {required}体");

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
