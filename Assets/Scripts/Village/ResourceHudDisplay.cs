using UnityEngine;
using Slimelands.Cards;

namespace Slimelands.Village
{
    /// <summary>
    /// 食料ストック(月間消費量に対する比率)・マナクリスタル・ガチャクリスタルの所持数を、
    /// アイコン付きで画面上部に一列表示するHUD(IMGUI)。マナ/ガチャクリスタルは通貨ではなく
    /// 盤面に置かれた資源カードとして扱われているため、CardBoard.CountCardsで実際に数える。
    /// </summary>
    public class ResourceHudDisplay : MonoBehaviour
    {
        [SerializeField] private FoodStockManager foodStockManager;
        [SerializeField] private VillageSlimeRoster slimeRoster;
        [SerializeField] private CardBoard cardBoard;

        [Header("盤面上で数える資源カード")]
        [SerializeField] private ResourceCardData manaCrystalData;
        [SerializeField] private ResourceCardData gachaCrystalData;

        [Header("アイコン")]
        [SerializeField] private Texture2D foodIcon;
        [SerializeField] private Texture2D manaCrystalIcon;
        [SerializeField] private Texture2D gachaCrystalIcon;

        private GUIStyle numberStyle;
        private Texture2D panelTexture;

        private const float IconSize = 28f;
        private const float PanelHeight = 40f;
        private const float PanelPadding = 16f;
        private const float GroupGap = 28f;

        private void EnsureStyles()
        {
            if (numberStyle != null)
            {
                return;
            }

            numberStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.black },
            };

            panelTexture = MakeSolidTexture(new Color(0.96f, 0.92f, 0.80f));
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
            int manaCrystalCount = cardBoard != null ? cardBoard.CountCards(manaCrystalData) : 0;
            int gachaCrystalCount = cardBoard != null ? cardBoard.CountCards(gachaCrystalData) : 0;

            string foodText = $"{food}/{consumption}";
            string manaCrystalText = manaCrystalCount.ToString();
            string gachaCrystalText = gachaCrystalCount.ToString();

            float panelWidth = PanelPadding * 2f
                + MeasureGroupWidth(foodText)
                + GroupGap + MeasureGroupWidth(manaCrystalText)
                + GroupGap + MeasureGroupWidth(gachaCrystalText);

            Rect panelRect = new(Screen.width - panelWidth - 10f, 10f, panelWidth, PanelHeight);
            GUI.DrawTexture(panelRect, panelTexture);
            GUI.Box(panelRect, GUIContent.none);

            float x = panelRect.x + PanelPadding;
            float centerY = panelRect.y + (PanelHeight - IconSize) * 0.5f;

            x = DrawGroup(x, centerY, foodText, foodIcon) + GroupGap;
            x = DrawGroup(x, centerY, manaCrystalText, manaCrystalIcon) + GroupGap;
            DrawGroup(x, centerY, gachaCrystalText, gachaCrystalIcon);
        }

        private float MeasureGroupWidth(string text)
        {
            Vector2 textSize = numberStyle.CalcSize(new GUIContent(text));
            return textSize.x + 6f + IconSize;
        }

        /// <summary>数値ラベル+アイコンの組を描画し、描画し終えたx座標(右端)を返す。</summary>
        private float DrawGroup(float x, float y, string text, Texture2D icon)
        {
            Vector2 textSize = numberStyle.CalcSize(new GUIContent(text));
            var textRect = new Rect(x, y, textSize.x, IconSize);
            GUI.Label(textRect, text, numberStyle);
            x += textSize.x + 6f;

            if (icon != null)
            {
                var iconRect = new Rect(x, y, IconSize, IconSize);
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
            }

            return x + IconSize;
        }
    }
}
