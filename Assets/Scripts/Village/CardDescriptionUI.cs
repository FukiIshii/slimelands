using UnityEngine;
using Slimelands.Cards;
using Slimelands.Slime;

namespace Slimelands.Village
{
    /// <summary>
    /// 村の盤面でカードを選択(クリック)したときに、そのカードの説明を表示する最小限のUI(IMGUI)。
    /// スライムカードの場合はレベルと経験値バー(NOW/MAX)も併せて表示する。
    /// ロジック側の動作確認を優先するための仮UIで、見た目の作り込みは行わない。
    /// </summary>
    public class CardDescriptionUI : MonoBehaviour
    {
        [SerializeField] private CardBoard cardBoard;

        private CardData selectedCard;
        private SlimeIndividual selectedIndividual;
        private bool showDetails;
        private readonly object detailsOverlayKey = new();
        private GUIStyle nameStyle;
        private GUIStyle descriptionStyle;
        private GUIStyle expLabelStyle;
        private Texture2D expBarBackTexture;
        private Texture2D expBarFillTexture;

        private const float BasePanelHeight = 130f;
        private const float SlimeSummaryHeight = 60f;
        private const float DetailsOverlayWidth = 380f;
        private const float DetailsOverlayHeight = 300f;

        private void OnEnable()
        {
            if (cardBoard != null)
            {
                cardBoard.OnCardSelected += HandleCardSelected;
            }
        }

        private void OnDisable()
        {
            if (cardBoard != null)
            {
                cardBoard.OnCardSelected -= HandleCardSelected;
            }

            ScreenUIPanelRegistry.SetPanelRect(this, null);
            ScreenUIPanelRegistry.SetPanelRect(detailsOverlayKey, null);
        }

        private void HandleCardSelected(CardController card)
        {
            selectedCard = card != null ? card.Data : null;
            selectedIndividual = card != null ? card.BoundIndividual : null;
            showDetails = false;
        }

        private void EnsureStyles()
        {
            if (nameStyle != null)
            {
                return;
            }

            nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
            };
            descriptionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = true,
                normal = { textColor = Color.white },
            };
            expLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
            };
            expBarBackTexture = MakeSolidTexture(new Color(0.15f, 0.15f, 0.15f));
            expBarFillTexture = MakeSolidTexture(new Color(0.3f, 0.65f, 1f));
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
            if (selectedCard == null)
            {
                ScreenUIPanelRegistry.SetPanelRect(this, null);
                ScreenUIPanelRegistry.SetPanelRect(detailsOverlayKey, null);
                return;
            }

            EnsureStyles();

            float panelHeight = BasePanelHeight;
            if (selectedIndividual != null)
            {
                panelHeight += SlimeSummaryHeight;
            }

            Rect panelRect = new(10f, 270f, 260f, panelHeight);
            ScreenUIPanelRegistry.SetPanelRect(this, panelRect);
            GUI.Box(panelRect, string.Empty);
            GUILayout.BeginArea(new Rect(20f, 276f, 240f, panelHeight - 12f));
            GUILayout.Label(selectedCard.CardName, nameStyle);
            string description = string.IsNullOrEmpty(selectedCard.Description) ? "(説明未設定)" : selectedCard.Description;
            GUILayout.Label(description, descriptionStyle);

            if (selectedIndividual != null)
            {
                GUILayout.Label($"Lv.{selectedIndividual.Level}", nameStyle);
                DrawExpBar(selectedIndividual);

                if (GUILayout.Button(showDetails ? "詳細を閉じる" : "詳細"))
                {
                    showDetails = !showDetails;
                }
            }

            GUILayout.EndArea();

            if (selectedIndividual != null && showDetails)
            {
                DrawDetailsOverlay(selectedIndividual);
            }
            else
            {
                ScreenUIPanelRegistry.SetPanelRect(detailsOverlayKey, null);
            }
        }

        /// <summary>
        /// 詳細ボタンで開く、ステータス(攻撃力等)と必殺技の内訳表示。他のUI(ダンジョンへボタン等)と
        /// 重ならないよう、盤面上のパネルとは独立に画面中央へオーバーレイ表示する。
        /// </summary>
        private void DrawDetailsOverlay(SlimeIndividual individual)
        {
            Rect overlayRect = new((Screen.width - DetailsOverlayWidth) * 0.5f, (Screen.height - DetailsOverlayHeight) * 0.5f,
                DetailsOverlayWidth, DetailsOverlayHeight);
            ScreenUIPanelRegistry.SetPanelRect(detailsOverlayKey, overlayRect);

            GUI.Box(overlayRect, string.Empty);
            GUILayout.BeginArea(new Rect(overlayRect.x + 10f, overlayRect.y + 6f, overlayRect.width - 20f, overlayRect.height - 12f));

            GUILayout.Label($"{selectedCard.CardName} の詳細", nameStyle);

            SlimeStats stats = individual.CalculateCurrentStats();
            GUILayout.Label("ステータス", nameStyle);
            GUILayout.Label($"HP: {stats.maxHp}    攻撃: {stats.attack}", descriptionStyle);
            GUILayout.Label($"防御: {stats.defense}    素早さ: {stats.speed}", descriptionStyle);

            GUILayout.Label("必殺技", nameStyle);
            SlimeSkillData ultimate = individual.AssignedUltimate;
            if (ultimate != null)
            {
                GUILayout.Label(ultimate.SkillName, descriptionStyle);
                string skillDescription = string.IsNullOrEmpty(ultimate.Description) ? "(説明未設定)" : ultimate.Description;
                GUILayout.Label(skillDescription, descriptionStyle);
            }
            else
            {
                GUILayout.Label("なし", descriptionStyle);
            }

            if (GUILayout.Button("閉じる"))
            {
                showDetails = false;
            }

            GUILayout.EndArea();
        }

        private void DrawExpBar(SlimeIndividual individual)
        {
            int required = individual.Species != null ? individual.Species.ExpPerLevel : 0;
            int current = individual.Exp;
            float ratio = required > 0 ? Mathf.Clamp01((float)current / required) : 0f;

            Rect barRect = GUILayoutUtility.GetRect(220f, 18f);
            GUI.DrawTexture(barRect, expBarBackTexture);
            Rect fillRect = new Rect(barRect.x, barRect.y, barRect.width * ratio, barRect.height);
            GUI.DrawTexture(fillRect, expBarFillTexture);
            GUI.Label(barRect, $"EXP {current}/{required}", expLabelStyle);
        }
    }
}
