using UnityEngine;

namespace Slimelands.Village
{
    /// <summary>
    /// 食料ストック・月間消費量合計・月齢と、ブロック中の警告を表示する最小限のHUD(IMGUI)。
    /// ロジック側の動作確認を優先するための仮UIで、見た目の作り込みは行わない。
    /// </summary>
    public class VillageHudDisplay : MonoBehaviour
    {
        [SerializeField] private FoodStockManager foodStockManager;
        [SerializeField] private MoonCycleManager moonCycleManager;
        [SerializeField] private VillageSlimeRoster slimeRoster;

        private GUIStyle labelStyle;
        private GUIStyle warningStyle;

        private void EnsureStyles()
        {
            if (labelStyle != null)
            {
                return;
            }

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                normal = { textColor = Color.white },
            };
            warningStyle = new GUIStyle(labelStyle)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.35f, 0.35f) },
            };
        }

        private void OnGUI()
        {
            EnsureStyles();

            int food = foodStockManager != null ? foodStockManager.CurrentFood : 0;
            int consumption = slimeRoster != null ? slimeRoster.CalculateTotalMonthlyConsumption() : 0;
            int month = moonCycleManager != null ? moonCycleManager.CurrentMonth : 1;
            bool blocked = moonCycleManager != null && moonCycleManager.IsBlocked;

            float boxHeight = blocked ? 132f : 104f;
            GUI.Box(new Rect(10f, 10f, 280f, boxHeight), string.Empty);

            GUILayout.BeginArea(new Rect(20f, 16f, 260f, boxHeight - 12f));
            GUILayout.Label($"月齢: {month}ヶ月目", labelStyle);
            GUILayout.Label($"食料ストック: {food}", labelStyle);
            GUILayout.Label($"月間消費量合計: {consumption}", labelStyle);
            if (blocked)
            {
                GUILayout.Label("食料不足! スライムを売るなどして", warningStyle);
                GUILayout.Label("消費量をストック以下に調整してください", warningStyle);
            }
            GUILayout.EndArea();
        }
    }
}
