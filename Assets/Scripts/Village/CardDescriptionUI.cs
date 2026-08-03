using UnityEngine;
using Slimelands.Cards;

namespace Slimelands.Village
{
    /// <summary>
    /// 村の盤面でカードを選択(クリック)したときに、そのカードの説明を表示する最小限のUI(IMGUI)。
    /// ロジック側の動作確認を優先するための仮UIで、見た目の作り込みは行わない。
    /// </summary>
    public class CardDescriptionUI : MonoBehaviour
    {
        [SerializeField] private CardBoard cardBoard;

        private CardData selectedCard;
        private GUIStyle nameStyle;
        private GUIStyle descriptionStyle;

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

        private void HandleCardSelected(CardData data)
        {
            selectedCard = data;
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
        }

        private void OnGUI()
        {
            if (selectedCard == null)
            {
                return;
            }

            EnsureStyles();

            GUI.Box(new Rect(10f, 270f, 260f, 140f), string.Empty);
            GUILayout.BeginArea(new Rect(20f, 276f, 240f, 128f));
            GUILayout.Label(selectedCard.CardName, nameStyle);
            string description = string.IsNullOrEmpty(selectedCard.Description) ? "(説明未設定)" : selectedCard.Description;
            GUILayout.Label(description, descriptionStyle);
            GUILayout.EndArea();
        }
    }
}
