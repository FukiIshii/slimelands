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
        private GUIStyle nameStyle;
        private GUIStyle descriptionStyle;
        private GUIStyle expLabelStyle;
        private Texture2D expBarBackTexture;
        private Texture2D expBarFillTexture;

        private const float PanelHeight = 180f;

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
        }

        private void HandleCardSelected(CardController card)
        {
            selectedCard = card != null ? card.Data : null;
            selectedIndividual = card != null ? card.BoundIndividual : null;
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
                return;
            }

            EnsureStyles();

            float panelHeight = selectedIndividual != null ? PanelHeight : PanelHeight - 40f;
            GUI.Box(new Rect(10f, 270f, 260f, panelHeight), string.Empty);
            GUILayout.BeginArea(new Rect(20f, 276f, 240f, panelHeight - 12f));
            GUILayout.Label(selectedCard.CardName, nameStyle);
            string description = string.IsNullOrEmpty(selectedCard.Description) ? "(説明未設定)" : selectedCard.Description;
            GUILayout.Label(description, descriptionStyle);

            if (selectedIndividual != null)
            {
                GUILayout.Label($"Lv.{selectedIndividual.Level}", nameStyle);
                DrawExpBar(selectedIndividual);
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
