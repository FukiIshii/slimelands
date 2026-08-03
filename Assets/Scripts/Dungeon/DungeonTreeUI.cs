using System.Collections.Generic;
using UnityEngine;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// ダンジョンの解放ツリーを表示する専用画面(IMGUI)。起点となるダンジョンを左端(列0)とし、
    /// 親から子への深さに応じて右方向へ列を伸ばして配置する。各ノードは未解放/解放済み未クリア/
    /// クリア済みの3状態を色分けで区別し、タップするとダンジョン詳細画面へ遷移する。
    /// 見た目専用で、解放判定のロジックは一切持たずDungeonProgressManagerに問い合わせるだけ。
    /// </summary>
    public class DungeonTreeUI : MonoBehaviour
    {
        [SerializeField] private DungeonProgressManager progressManager;
        [SerializeField] private GameScreenController screenController;

        [Header("レイアウト(仮値、要調整)")]
        [SerializeField] private Vector2 origin = new(30f, 60f);
        [SerializeField] private float columnWidth = 170f;
        [SerializeField] private float rowHeight = 74f;
        [SerializeField] private Vector2 nodeSize = new(150f, 56f);

        private void OnGUI()
        {
            if (progressManager == null || screenController == null)
            {
                return;
            }

            GUI.Box(new Rect(10f, 10f, 780f, 560f), string.Empty);
            GUILayout.BeginArea(new Rect(20f, 16f, 300f, 30f));
            GUILayout.Label("--- ダンジョンツリー ---");
            GUILayout.EndArea();

            if (GUI.Button(new Rect(620f, 20f, 150f, 28f), "< 村に戻る"))
            {
                screenController.ReturnToVillage();
            }

            var columns = new Dictionary<int, List<DungeonData>>();
            int maxColumn = 0;
            foreach (DungeonData dungeon in progressManager.AllDungeons)
            {
                if (dungeon == null)
                {
                    continue;
                }

                int depth = progressManager.GetDepth(dungeon);
                if (!columns.TryGetValue(depth, out List<DungeonData> list))
                {
                    list = new List<DungeonData>();
                    columns[depth] = list;
                }

                list.Add(dungeon);
                if (depth > maxColumn)
                {
                    maxColumn = depth;
                }
            }

            for (int col = 0; col <= maxColumn; col++)
            {
                if (!columns.TryGetValue(col, out List<DungeonData> list))
                {
                    continue;
                }

                for (int row = 0; row < list.Count; row++)
                {
                    DrawNode(list[row], col, row);
                }
            }
        }

        private void DrawNode(DungeonData dungeon, int column, int row)
        {
            var rect = new Rect(
                origin.x + column * columnWidth,
                origin.y + row * rowHeight,
                nodeSize.x, nodeSize.y);

            DungeonNodeState state = progressManager.GetState(dungeon);

            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = StateColor(state);
            GUI.enabled = state != DungeonNodeState.Locked;

            if (GUI.Button(rect, $"{dungeon.DungeonName}\n[{StateLabel(state)}]"))
            {
                screenController.ShowDungeonDetail(dungeon);
            }

            GUI.enabled = true;
            GUI.backgroundColor = prevColor;
        }

        private static Color StateColor(DungeonNodeState state)
        {
            return state switch
            {
                DungeonNodeState.Cleared => new Color(0.45f, 0.85f, 0.45f),
                DungeonNodeState.UnlockedNotCleared => Color.white,
                _ => new Color(0.4f, 0.4f, 0.4f),
            };
        }

        private static string StateLabel(DungeonNodeState state)
        {
            return state switch
            {
                DungeonNodeState.Cleared => "クリア済み",
                DungeonNodeState.UnlockedNotCleared => "解放済み",
                _ => "未解放",
            };
        }
    }
}
