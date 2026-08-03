using UnityEngine;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// 村画面からダンジョンツリー画面へ遷移するための最小限の入り口ボタン(IMGUI)。
    /// 村パートの内部実装には依存せず、GameScreenControllerの公開APIを呼ぶだけの橋渡し役。
    /// </summary>
    public class VillageDungeonEntranceUI : MonoBehaviour
    {
        [SerializeField] private GameScreenController screenController;

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
