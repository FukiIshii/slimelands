using System.Collections.Generic;
using UnityEngine;
using Slimelands.Slime;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// ダンジョンツリー画面でタップされたダンジョンの詳細(名前・出現する敵など)を表示し、
    /// 「挑戦する」からパーティー編成画面へつなぐ専用画面(IMGUI)。表示対象は
    /// GameScreenController.SelectedDungeonForDetailを参照するだけで、選択状態自体は持たない。
    /// </summary>
    public class DungeonDetailUI : MonoBehaviour
    {
        [SerializeField] private GameScreenController screenController;
        [SerializeField] private PartyFormationUI partyFormationUI;
        [SerializeField] private DungeonProgressManager progressManager;

        private void OnGUI()
        {
            if (screenController == null)
            {
                return;
            }

            DungeonData dungeon = screenController.SelectedDungeonForDetail;

            GUI.Box(new Rect(10f, 10f, 420f, 460f), string.Empty);
            GUILayout.BeginArea(new Rect(20f, 16f, 400f, 448f));

            if (GUILayout.Button("< ツリーに戻る", GUILayout.Width(150f)))
            {
                screenController.ShowDungeonTree();
            }

            GUILayout.Space(8f);

            if (dungeon == null)
            {
                GUILayout.Label("ダンジョンが選択されていません。");
                GUILayout.EndArea();
                return;
            }

            DungeonNodeState state = progressManager != null
                ? progressManager.GetState(dungeon)
                : DungeonNodeState.UnlockedNotCleared;

            GUILayout.Label($"--- {dungeon.DungeonName} ---");
            GUILayout.Label($"状態: {StateLabel(state)}");
            GUILayout.Label($"必要パーティー体数: {dungeon.RequiredPartySize}");
            GUILayout.Label($"出現する敵のレベル: {dungeon.EnemyLevel}");

            GUILayout.Space(8f);
            GUILayout.Label("--- 出現する敵(ウェーブ順) ---");
            for (int i = 0; i < dungeon.Waves.Count; i++)
            {
                EncounterWaveData wave = dungeon.Waves[i];
                var names = new List<string>();
                foreach (SlimeSpeciesData species in wave.EnemySpecies)
                {
                    if (species != null)
                    {
                        names.Add(species.SpeciesName);
                    }
                }

                string enemyList = names.Count > 0 ? string.Join(" / ", names) : "(未設定)";
                GUILayout.Label($"ウェーブ{i + 1}: {enemyList}");
            }

            GUILayout.Space(16f);

            bool unlocked = state != DungeonNodeState.Locked;
            GUI.enabled = unlocked;
            if (GUILayout.Button("挑戦する", GUILayout.Height(32f)))
            {
                if (partyFormationUI != null)
                {
                    partyFormationUI.SelectDungeon(dungeon);
                }

                screenController.ShowPartyFormation();
            }

            GUI.enabled = true;

            if (!unlocked)
            {
                GUILayout.Label("(このダンジョンはまだ解放されていません)");
            }

            GUILayout.EndArea();
        }

        private static string StateLabel(DungeonNodeState state)
        {
            return state switch
            {
                DungeonNodeState.Cleared => "クリア済み",
                DungeonNodeState.UnlockedNotCleared => "解放済み(未クリア)",
                _ => "未解放",
            };
        }
    }
}
