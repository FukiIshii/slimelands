using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// ダンジョンツリー画面でタップされたダンジョンの詳細(名前・アートワーク・説明文・
    /// 推奨レベル・参加体数・クリア状況)をパーチメント風のパネルで表示し、「ここに行く」から
    /// パーティー編成画面へつなぐ画面。表示対象はGameScreenController.SelectedDungeonForDetailを
    /// 参照するだけで、選択状態自体は持たない。DungeonPartyHud/DungeonResultUIと同様、
    /// uGUI一式を実行時にコードで組み立てる見た目専用コンポーネント。
    /// </summary>
    public class DungeonDetailUI : MonoBehaviour
    {
        [SerializeField] private GameScreenController screenController;
        [SerializeField] private PartyFormationUI partyFormationUI;
        [SerializeField] private DungeonProgressManager progressManager;

        [Header("レイアウト(仮値、要調整。専用アート未着手のため単色パネルで代用)")]
        [SerializeField] private Vector2 panelSize = new(900f, 780f);
        [SerializeField] private Color overlayColor = new(0f, 0f, 0f, 0.55f);
        [SerializeField] private Color panelColor = new(0.93f, 0.85f, 0.66f, 1f);
        [SerializeField] private Color clearedTextColor = new(0.25f, 0.5f, 0.9f);
        [SerializeField] private Color goEnabledColor = new(0.75f, 0.1f, 0.1f);
        [SerializeField] private Color goDisabledColor = new(0.55f, 0.55f, 0.55f);
        [SerializeField] private float iconSize = 220f;
        [SerializeField] private Vector2 buttonSize = new(240f, 72f);

        private static Font cachedFont;

        private GameObject overlayGo;
        private Text titleText;
        private Image iconImage;
        private Text descriptionText;
        private Text statsText;
        private Text clearStatusText;
        private Button goButton;
        private Text goButtonLabel;

        private void OnEnable()
        {
            EnsureUi();
            Refresh();
        }

        private void OnDisable()
        {
            if (overlayGo != null)
            {
                overlayGo.SetActive(false);
            }
        }

        private void Refresh()
        {
            DungeonData dungeon = screenController != null ? screenController.SelectedDungeonForDetail : null;
            if (dungeon == null)
            {
                overlayGo.SetActive(false);
                return;
            }

            overlayGo.SetActive(true);

            DungeonNodeState state = progressManager != null
                ? progressManager.GetState(dungeon)
                : DungeonNodeState.UnlockedNotCleared;
            bool unlocked = state != DungeonNodeState.Locked;

            titleText.text = dungeon.DungeonName;
            iconImage.sprite = dungeon.Icon != null
                ? dungeon.Icon
                : RuntimeSpriteFactory.GetCircleSprite(new Color(0.5f, 0.42f, 0.3f));

            descriptionText.text = unlocked
                ? dungeon.Description
                : "(このダンジョンはまだ解放されていません)";

            statsText.text = $"推奨レベル:{dungeon.EnemyLevel}    参加:{dungeon.RequiredPartySize}体";
            statsText.gameObject.SetActive(unlocked);

            clearStatusText.gameObject.SetActive(unlocked && state == DungeonNodeState.Cleared);

            goButton.interactable = unlocked;
            goButtonLabel.color = unlocked ? goEnabledColor : goDisabledColor;
        }

        private void HandleGoClicked()
        {
            if (screenController == null)
            {
                return;
            }

            DungeonData dungeon = screenController.SelectedDungeonForDetail;
            if (dungeon == null)
            {
                return;
            }

            if (partyFormationUI != null)
            {
                partyFormationUI.SelectDungeon(dungeon);
            }

            screenController.ShowPartyFormation();
        }

        private void HandleCancelClicked()
        {
            if (screenController != null)
            {
                screenController.ShowDungeonTree();
            }
        }

        /// <summary>
        /// UI一式(Overlay/Panel/各テキスト・ボタン)を用意する。DungeonResultUIと同様、
        /// 既にシーンに存在する同名の子を再利用して重複生成を防ぐ。
        /// 呼ぶたびに毎回transform.Find()から辿り直す(overlayGo等のGameObject/Component参照
        /// フィールドはPlay Mode中のドメインリロード(スクリプト再コンパイル)を挟んでも値が
        /// 保持されてしまうため、「overlayGo != nullなら初期化済みとみなしてスキップ」という
        /// ガードを入れると、リロード後にonClickの再登録(下記)が二度と行われなくなり
        /// ボタンが反応しなくなる不具合になっていた)。
        /// </summary>
        private void EnsureUi()
        {
            Transform existingOverlay = transform.Find("DungeonDetailOverlay");
            if (existingOverlay != null)
            {
                overlayGo = existingOverlay.gameObject;
                titleText = overlayGo.transform.Find("Panel/Title").GetComponent<Text>();
                iconImage = overlayGo.transform.Find("Panel/Icon").GetComponent<Image>();
                descriptionText = overlayGo.transform.Find("Panel/Description").GetComponent<Text>();
                statsText = overlayGo.transform.Find("Panel/Stats").GetComponent<Text>();
                clearStatusText = overlayGo.transform.Find("Panel/ClearStatus").GetComponent<Text>();
                goButton = overlayGo.transform.Find("Panel/GoButton").GetComponent<Button>();
                goButtonLabel = goButton.transform.Find("Label").GetComponent<Text>();

                // Play Mode中の再コンパイル(ドメインリロード)でシーン上のGameObjectはそのまま残るが、
                // onClick.AddListenerで登録したコード側のリスナーは消えてしまう(overlayGo等の
                // 非シリアライズフィールドがnullに戻ってこの再利用分岐に来る=リロード後の1回目という
                // ことなので、ここで確実に張り直す。二重登録を避けるためRemoveAllListenersしてから登録)。
                Button existingCancelButton = overlayGo.transform.Find("Panel/CancelButton").GetComponent<Button>();
                existingCancelButton.onClick.RemoveAllListeners();
                existingCancelButton.onClick.AddListener(HandleCancelClicked);
                goButton.onClick.RemoveAllListeners();
                goButton.onClick.AddListener(HandleGoClicked);

                EnsureEventSystem();
                return;
            }

            overlayGo = new GameObject("DungeonDetailOverlay",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
            overlayGo.transform.SetParent(transform, false);

            Canvas canvas = overlayGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;

            CanvasScaler scaler = overlayGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 1f;

            RectTransform overlayRect = overlayGo.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            overlayGo.GetComponent<Image>().color = overlayColor;

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(overlayGo.transform, false);
            RectTransform panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = panelSize;
            panelGo.GetComponent<Image>().color = panelColor;

            const float innerMargin = 32f;

            const float titleHeight = 56f;
            float titleY = panelSize.y - innerMargin - titleHeight;
            RectTransform titleRect = CreateChild(panelGo.transform, "Title", typeof(Text));
            SetRect(titleRect, 0f, titleY, panelSize.x, titleHeight);
            titleText = titleRect.GetComponent<Text>();
            titleText.font = GetDefaultFont();
            titleText.fontSize = 40;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(0.25f, 0.15f, 0.05f);

            float iconY = titleY - 16f - iconSize;
            RectTransform iconRect = CreateChild(panelGo.transform, "Icon", typeof(Image));
            SetRect(iconRect, (panelSize.x - iconSize) * 0.5f, iconY, iconSize, iconSize);
            iconImage = iconRect.GetComponent<Image>();
            iconImage.preserveAspect = true;

            const float descriptionHeight = 110f;
            float descriptionY = iconY - 16f - descriptionHeight;
            RectTransform descriptionRect = CreateChild(panelGo.transform, "Description", typeof(Text));
            SetRect(descriptionRect, innerMargin, descriptionY, panelSize.x - innerMargin * 2f, descriptionHeight);
            descriptionText = descriptionRect.GetComponent<Text>();
            descriptionText.font = GetDefaultFont();
            descriptionText.fontSize = 24;
            descriptionText.alignment = TextAnchor.UpperCenter;
            descriptionText.color = new Color(0.2f, 0.15f, 0.1f);
            descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descriptionText.verticalOverflow = VerticalWrapMode.Overflow;

            const float statsHeight = 40f;
            float statsY = descriptionY - 12f - statsHeight;
            RectTransform statsRect = CreateChild(panelGo.transform, "Stats", typeof(Text));
            SetRect(statsRect, 0f, statsY, panelSize.x, statsHeight);
            statsText = statsRect.GetComponent<Text>();
            statsText.font = GetDefaultFont();
            statsText.fontSize = 26;
            statsText.alignment = TextAnchor.MiddleCenter;
            statsText.color = new Color(0.25f, 0.15f, 0.05f);

            const float clearStatusHeight = 36f;
            float clearStatusY = statsY - 8f - clearStatusHeight;
            RectTransform clearStatusRect = CreateChild(panelGo.transform, "ClearStatus", typeof(Text));
            SetRect(clearStatusRect, 0f, clearStatusY, panelSize.x, clearStatusHeight);
            clearStatusText = clearStatusRect.GetComponent<Text>();
            clearStatusText.text = "クリア済み";
            clearStatusText.font = GetDefaultFont();
            clearStatusText.fontSize = 24;
            clearStatusText.alignment = TextAnchor.MiddleCenter;
            clearStatusText.color = clearedTextColor;

            float buttonGap = 40f;
            float buttonsY = innerMargin;
            float buttonsTotalWidth = buttonSize.x * 2f + buttonGap;
            float buttonsStartX = (panelSize.x - buttonsTotalWidth) * 0.5f;

            Button cancelButton = CreateButton(panelGo.transform, "CancelButton", "やめる",
                buttonsStartX, buttonsY, buttonSize, new Color(0.6f, 0.6f, 0.6f), Color.black);
            goButton = CreateButton(panelGo.transform, "GoButton", "ここに行く",
                buttonsStartX + buttonSize.x + buttonGap, buttonsY, buttonSize, panelColor, goEnabledColor);
            goButtonLabel = goButton.transform.Find("Label").GetComponent<Text>();
            goButtonLabel.fontStyle = FontStyle.Bold;

            cancelButton.onClick.AddListener(HandleCancelClicked);
            goButton.onClick.AddListener(HandleGoClicked);

            EnsureEventSystem();
        }

        private Button CreateButton(Transform parent, string name, string label, float x, float y, Vector2 size,
            Color backgroundColor, Color labelColor)
        {
            RectTransform buttonRect = CreateChild(parent, name, typeof(Image), typeof(Button));
            SetRect(buttonRect, x, y, size.x, size.y);
            buttonRect.GetComponent<Image>().color = backgroundColor;
            Button button = buttonRect.GetComponent<Button>();

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(buttonRect, false);
            RectTransform labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            Text labelText = labelGo.GetComponent<Text>();
            labelText.text = label;
            labelText.font = GetDefaultFont();
            labelText.fontSize = 26;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = labelColor;

            return button;
        }

        // uGUIのボタン等はEventSystemが無いと入力を受け取れない。このプロジェクトは新Input System
        // 専用のため、旧StandaloneInputModuleではなくInputSystemUIInputModuleを使う
        // (DungeonPartyHud/DungeonResultUIと同じ対策)。
        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            }
        }

        private static void SetRect(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static RectTransform CreateChild(Transform parent, string name, params System.Type[] extraComponents)
        {
            var types = new System.Type[extraComponents.Length + 1];
            types[0] = typeof(RectTransform);
            for (int i = 0; i < extraComponents.Length; i++)
            {
                types[i + 1] = extraComponents[i];
            }

            var go = new GameObject(name, types);
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static Font GetDefaultFont()
        {
            if (cachedFont == null)
            {
                cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return cachedFont;
        }
    }
}
