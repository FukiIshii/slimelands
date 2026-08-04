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
        private GUIStyle monthBarLabelStyle;
        private Texture2D monthBarBackTexture;
        private Texture2D monthBarFillTexture;

        private const float MonthBarHeight = 16f;

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
            monthBarLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
            };
            monthBarBackTexture = MakeSolidTexture(new Color(0.15f, 0.15f, 0.15f));
            monthBarFillTexture = MakeSolidTexture(new Color(0.5f, 0.8f, 0.3f));
        }

        private static Texture2D MakeSolidTexture(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private void OnGUI()
        {
            EnsureStyles();

            int food = foodStockManager != null ? foodStockManager.CurrentFood : 0;
            int consumption = slimeRoster != null ? slimeRoster.CalculateTotalMonthlyConsumption() : 0;
            int month = moonCycleManager != null ? moonCycleManager.CurrentMonth : 1;
            bool blocked = moonCycleManager != null && moonCycleManager.IsBlocked;
            float monthProgress = moonCycleManager != null ? moonCycleManager.MonthProgress01 : 0f;

            float boxHeight = (blocked ? 132f : 104f) + MonthBarHeight + 4f;
            GUI.Box(new Rect(10f, 10f, 280f, boxHeight), string.Empty);

            GUILayout.BeginArea(new Rect(20f, 16f, 260f, boxHeight - 12f));
            GUILayout.Label($"月齢: {month}ヶ月目", labelStyle);
            DrawMonthProgressBar(monthProgress, blocked);
            GUILayout.Label($"食料ストック: {food}", labelStyle);
            GUILayout.Label($"月間消費量合計: {consumption}", labelStyle);
            if (blocked)
            {
                GUILayout.Label("食料不足! スライムを売るなどして", warningStyle);
                GUILayout.Label("消費量をストック以下に調整してください", warningStyle);
            }
            GUILayout.EndArea();
        }

        /// <summary>月末(月齢が進むタイミング)までの経過割合をバーで表示する。ブロック中は進行が
        /// 止まっている(食料不足で足踏み中)ことが分かるよう、バーの色を警告色にする。</summary>
        private void DrawMonthProgressBar(float progress01, bool blocked)
        {
            Rect barRect = GUILayoutUtility.GetRect(240f, MonthBarHeight);
            GUI.DrawTexture(barRect, monthBarBackTexture);

            Color previousColor = GUI.color;
            GUI.color = blocked ? new Color(1f, 0.35f, 0.35f) : Color.white;
            Rect fillRect = new(barRect.x, barRect.y, barRect.width * Mathf.Clamp01(progress01), barRect.height);
            GUI.DrawTexture(fillRect, monthBarFillTexture);
            GUI.color = previousColor;

            GUI.Label(barRect, blocked ? "食料不足で停止中" : $"{Mathf.RoundToInt(progress01 * 100f)}%", monthBarLabelStyle);
        }
    }
}
