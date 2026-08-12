using System.Collections.Generic;
using UnityEngine;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// ダンジョン進行を確認するための最小限のUI(IMGUIのテキストログ+ボタン)。
    /// 見た目の作り込みは行わず、ロジックの動作確認を優先する。
    /// </summary>
    public class DungeonHudDisplay : MonoBehaviour
    {
        [SerializeField] private DungeonRunController controller;
        [SerializeField] private int maxLogLines = 12;

        private readonly List<string> logLines = new();
        private Vector2 scroll;

        private void OnEnable()
        {
            if (controller != null)
            {
                controller.OnLog += HandleLog;
            }
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.OnLog -= HandleLog;
            }
        }

        private void HandleLog(string message)
        {
            logLines.Add(message);
            while (logLines.Count > maxLogLines)
            {
                logLines.RemoveAt(0);
            }
        }

        private void OnGUI()
        {
            if (controller == null)
            {
                return;
            }

            GUI.Box(new Rect(10f, 260f, 420f, 320f), string.Empty);
            GUILayout.BeginArea(new Rect(20f, 266f, 400f, 308f));

            GUILayout.Label($"状態: {controller.State}");

            // パーティーのHP/名前/必殺技発動ボタンはDungeonPartyHud(画面上部のuGUI)に統一したため、
            // ここでは重複表示しない。クリア/失敗後の「戻る」「もう一回」もDungeonResultUIに
            // 統一したため、ここでは表示しない。

            GUILayout.Space(4f);
            GUILayout.Label("--- 敵 ---");
            foreach (BattleUnit enemy in controller.AliveEnemies)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{enemy.DisplayName} HP {enemy.CurrentHp}/{enemy.Stats.maxHp}", GUILayout.Width(280f));
                if (GUILayout.Button("ターゲット指定", GUILayout.Width(90f)))
                {
                    controller.SetTarget(enemy);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(4f);
            GUILayout.Label("--- ログ ---");
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(120f));
            foreach (string line in logLines)
            {
                GUILayout.Label(line);
            }
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }
    }
}
