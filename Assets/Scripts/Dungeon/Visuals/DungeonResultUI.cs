using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// ダンジョンクリア/敗退時に、結果(入手アイテム)と「戻る」「もう一回」の選択肢を表示する
    /// uGUIベースの結果画面。DungeonRunControllerのクリア/失敗イベントを購読して自動的に
    /// 表示するだけの見た目専用コンポーネントで、戦闘ロジックには一切書き込まない
    /// (「もう一回」も、パーティー編成(SourceIndividual)をそのままcontroller.StartDungeonへ
    /// 渡す既存の公開APIを呼ぶだけで、HP不足個体の除外等は既存ロジックにそのまま従う)。
    /// UI一式は(DungeonPartyHud等と同様に)実行時にコードで組み立てる。
    /// </summary>
    public class DungeonResultUI : MonoBehaviour
    {
        [SerializeField] private DungeonRunController controller;
        [Tooltip("「戻る」ボタンから村へ画面切り替えを呼ぶための参照。未設定でも動作する(ボタンが押せなくなるだけ)。")]
        [SerializeField] private GameScreenController screenController;

        [Header("レイアウト(仮値、要調整。専用アート未着手のため単色パネルで代用)")]
        [SerializeField] private Vector2 panelSize = new(880f, 680f);
        [SerializeField] private Color overlayColor = new(0f, 0f, 0f, 0.55f);
        [SerializeField] private Color panelColor = new(0.93f, 0.85f, 0.66f, 1f);
        [SerializeField] private Color clearedMessageColor = new(0.82f, 0.12f, 0.12f);
        [SerializeField] private Color failedMessageColor = new(0.3f, 0.3f, 0.3f);
        [SerializeField] private float itemIconSize = 96f;
        [SerializeField] private float itemSpacing = 28f;
        [SerializeField] private Vector2 buttonSize = new(220f, 72f);

        // アイコン下の個数表示("x N")の分の高さ。itemIconSizeとの間に隙間(ItemCountAreaHeight - Count自体の高さ30f)
        // を確保しないと、アイコンとテキストが詰まって重なって見える(要修正だった見た目のバグ)。
        private const float ItemCountAreaHeight = 40f;

        private static Font cachedFont;

        private GameObject overlayGo;
        private Text titleText;
        private Text messageText;
        private RectTransform itemRow;
        private Button backButton;
        private Button retryButton;

        private void OnEnable()
        {
            EnsureUi();
            HidePanel();
            if (controller != null)
            {
                controller.OnDungeonCleared += HandleDungeonCleared;
                controller.OnDungeonFailed += HandleDungeonFailed;
            }
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.OnDungeonCleared -= HandleDungeonCleared;
                controller.OnDungeonFailed -= HandleDungeonFailed;
            }
        }

        private void HandleDungeonCleared()
        {
            ShowPanel(true);
        }

        private void HandleDungeonFailed()
        {
            ShowPanel(false);
        }

        private void ShowPanel(bool cleared)
        {
            EnsureUi();

            string dungeonName = controller != null && controller.CurrentDungeon != null
                ? controller.CurrentDungeon.DungeonName
                : string.Empty;

            messageText.text = cleared ? $"{dungeonName} クリア!" : $"{dungeonName} 敗退…";
            messageText.color = cleared ? clearedMessageColor : failedMessageColor;

            BuildItemRow(cleared);

            overlayGo.SetActive(true);
        }

        private void HidePanel()
        {
            if (overlayGo != null)
            {
                overlayGo.SetActive(false);
            }
        }

        private void BuildItemRow(bool cleared)
        {
            for (int i = itemRow.childCount - 1; i >= 0; i--)
            {
                Destroy(itemRow.GetChild(i).gameObject);
            }

            if (!cleared || controller == null || controller.CurrentDungeon == null)
            {
                return;
            }

            foreach (DungeonDropEntry entry in controller.CurrentDungeon.DropRewards)
            {
                if (entry == null || entry.Resource == null || entry.Amount <= 0)
                {
                    continue;
                }

                CreateItemEntry(entry.Resource.Icon, entry.Amount);
            }
        }

        private void CreateItemEntry(Sprite icon, int amount)
        {
            // itemRowのHorizontalLayoutGroupはchildControlWidth/Height=falseのため、
            // LayoutElementの指定サイズは反映されない(位置合わせのみ行い、子のRectTransform
            // 自体のサイズはそのまま使われる)。そのためsizeDeltaを直接指定する。
            var itemGo = new GameObject("Item", typeof(RectTransform));
            itemGo.transform.SetParent(itemRow, false);
            RectTransform itemRect = itemGo.GetComponent<RectTransform>();
            itemRect.sizeDelta = new Vector2(itemIconSize, itemIconSize + ItemCountAreaHeight);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(itemGo.transform, false);
            RectTransform iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(itemIconSize, itemIconSize);
            Image iconImage = iconGo.GetComponent<Image>();
            iconImage.sprite = icon != null ? icon : RuntimeSpriteFactory.GetCircleSprite(new Color(0.7f, 0.7f, 0.7f));
            iconImage.preserveAspect = true;

            var countGo = new GameObject("Count", typeof(RectTransform), typeof(Text));
            countGo.transform.SetParent(itemGo.transform, false);
            RectTransform countRect = countGo.GetComponent<RectTransform>();
            countRect.anchorMin = new Vector2(0.5f, 0f);
            countRect.anchorMax = new Vector2(0.5f, 0f);
            countRect.pivot = new Vector2(0.5f, 0f);
            countRect.anchoredPosition = Vector2.zero;
            countRect.sizeDelta = new Vector2(itemIconSize + 24f, 30f);
            Text countText = countGo.GetComponent<Text>();
            countText.text = $"x {amount}";
            countText.font = GetDefaultFont();
            countText.fontSize = 24;
            countText.alignment = TextAnchor.MiddleCenter;
            countText.color = Color.black;
        }

        /// <summary>
        /// UI一式(Overlay/Panel/各テキスト・ボタン)を用意する。Edit Mode上での操作やドメイン
        /// リロード後に再度呼ばれても、既にシーンに存在する同名の子を再利用して重複生成を防ぐ。
        /// 呼ぶたびに毎回transform.Find()から辿り直す(overlayGo等のGameObject/Component参照
        /// フィールドはPlay Mode中のドメインリロードを挟んでも値が保持されてしまうため、
        /// 「overlayGo != nullなら初期化済みとみなしてスキップ」というガードを入れると、
        /// リロード後にonClickの再登録(下記)が二度と行われなくなりボタンが反応しなくなる
        /// 不具合になっていた)。
        /// </summary>
        private void EnsureUi()
        {
            Transform existingOverlay = transform.Find("DungeonResultOverlay");
            if (existingOverlay != null)
            {
                overlayGo = existingOverlay.gameObject;
                titleText = overlayGo.transform.Find("Panel/Title").GetComponent<Text>();
                messageText = overlayGo.transform.Find("Panel/Message").GetComponent<Text>();
                itemRow = (RectTransform)overlayGo.transform.Find("Panel/ItemRow");
                backButton = overlayGo.transform.Find("Panel/BackButton").GetComponent<Button>();
                retryButton = overlayGo.transform.Find("Panel/RetryButton").GetComponent<Button>();

                // Play Mode中の再コンパイル(ドメインリロード)でシーン上のGameObjectはそのまま残るが、
                // onClick.AddListenerで登録したコード側のリスナーは消えてしまうため、ここで張り直す
                // (DungeonDetailUIの同種の不具合と同じ原因、二重登録を避けるためRemoveAllListenersしてから登録)。
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(HandleBackClicked);
                retryButton.onClick.RemoveAllListeners();
                retryButton.onClick.AddListener(HandleRetryClicked);

                EnsureEventSystem();
                return;
            }

            overlayGo = new GameObject("DungeonResultOverlay",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
            overlayGo.transform.SetParent(transform, false);

            Canvas canvas = overlayGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

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

            const float titleHeight = 60f;
            float titleY = panelSize.y - innerMargin - titleHeight;
            RectTransform titleRect = CreateChild(panelGo.transform, "Title", typeof(Text));
            SetRect(titleRect, 0f, titleY, panelSize.x, titleHeight);
            titleText = titleRect.GetComponent<Text>();
            titleText.text = "結果発表";
            titleText.font = GetDefaultFont();
            titleText.fontSize = 44;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(0.25f, 0.15f, 0.05f);

            const float messageHeight = 56f;
            float messageY = titleY - 12f - messageHeight;
            RectTransform messageRect = CreateChild(panelGo.transform, "Message", typeof(Text));
            SetRect(messageRect, 0f, messageY, panelSize.x, messageHeight);
            messageText = messageRect.GetComponent<Text>();
            messageText.font = GetDefaultFont();
            messageText.fontSize = 34;
            messageText.fontStyle = FontStyle.Bold;
            messageText.alignment = TextAnchor.MiddleCenter;

            const float itemsLabelHeight = 40f;
            float itemsLabelY = messageY - 32f - itemsLabelHeight;
            RectTransform itemsLabelRect = CreateChild(panelGo.transform, "ItemsLabel", typeof(Text));
            SetRect(itemsLabelRect, 0f, itemsLabelY, panelSize.x, itemsLabelHeight);
            Text itemsLabelText = itemsLabelRect.GetComponent<Text>();
            itemsLabelText.text = "入手アイテム";
            itemsLabelText.font = GetDefaultFont();
            itemsLabelText.fontSize = 26;
            itemsLabelText.alignment = TextAnchor.MiddleCenter;
            itemsLabelText.color = new Color(0.25f, 0.15f, 0.05f);

            float itemRowHeight = itemIconSize + ItemCountAreaHeight;
            float itemRowY = itemsLabelY - 16f - itemRowHeight;
            RectTransform itemRowRect = CreateChild(panelGo.transform, "ItemRow", typeof(HorizontalLayoutGroup));
            SetRect(itemRowRect, innerMargin, itemRowY, panelSize.x - innerMargin * 2f, itemRowHeight);
            HorizontalLayoutGroup itemLayout = itemRowRect.GetComponent<HorizontalLayoutGroup>();
            itemLayout.spacing = itemSpacing;
            itemLayout.childAlignment = TextAnchor.MiddleCenter;
            itemLayout.childForceExpandWidth = false;
            itemLayout.childForceExpandHeight = false;
            itemLayout.childControlWidth = false;
            itemLayout.childControlHeight = false;
            itemRow = itemRowRect;

            float buttonGap = 40f;
            float buttonsY = innerMargin;
            float buttonsTotalWidth = buttonSize.x * 2f + buttonGap;
            float buttonsStartX = (panelSize.x - buttonsTotalWidth) * 0.5f;

            backButton = CreateButton(panelGo.transform, "BackButton", "戻る",
                buttonsStartX, buttonsY, buttonSize, new Color(0.6f, 0.6f, 0.6f));
            retryButton = CreateButton(panelGo.transform, "RetryButton", "もう一回",
                buttonsStartX + buttonSize.x + buttonGap, buttonsY, buttonSize, new Color(0.95f, 0.75f, 0.25f));

            backButton.onClick.AddListener(HandleBackClicked);
            retryButton.onClick.AddListener(HandleRetryClicked);

            EnsureEventSystem();
        }

        private Button CreateButton(Transform parent, string name, string label, float x, float y, Vector2 size, Color color)
        {
            RectTransform buttonRect = CreateChild(parent, name, typeof(Image), typeof(Button));
            SetRect(buttonRect, x, y, size.x, size.y);
            buttonRect.GetComponent<Image>().color = color;
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
            labelText.color = Color.black;

            return button;
        }

        private void HandleBackClicked()
        {
            HidePanel();
            if (screenController != null)
            {
                screenController.ReturnToVillage();
            }
        }

        private void HandleRetryClicked()
        {
            if (controller == null || controller.CurrentDungeon == null)
            {
                return;
            }

            var party = controller.PlayerUnits
                .Select(unit => unit.SourceIndividual)
                .Where(individual => individual != null)
                .ToList();

            HidePanel();
            controller.StartDungeon(controller.CurrentDungeon, party);
        }

        // uGUIのボタン等はEventSystemが無いと入力を受け取れない。このプロジェクトは新Input System
        // 専用のため、旧StandaloneInputModuleではなくInputSystemUIInputModuleを使う
        // (DungeonPartyHudと同じ対策)。
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
