using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// ダンジョン戦闘中、画面上部(右上)にプレイヤー側スライムの肖像・名前・HPバー・必殺技発動ボタンを
    /// 表示するuGUIベースのHUD。DungeonRunControllerのイベントを購読して自動的に生成・更新するだけの
    /// 見た目専用コンポーネントで、戦闘ロジックには一切書き込まない(必殺技発動ボタンはCLAUDE.mdで
    /// 許可されている「必殺技発動」操作をcontroller.TryActivateUltimate経由で呼ぶのみ)。
    /// UI一式は(既存のBattleUnitView等と同様に)実行時にコードで組み立てる。
    /// </summary>
    public class DungeonPartyHud : MonoBehaviour
    {
        [SerializeField] private DungeonRunController controller;

        [Header("レイアウト(仮値、要調整)")]
        [SerializeField] private Vector2 rowSize = new(320f, 88f);
        [SerializeField] private float rowSpacing = 8f;
        [SerializeField] private Vector2 screenMargin = new(16f, 16f);
        [SerializeField] private float portraitSize = 72f;
        [SerializeField] private float ultimateButtonSize = 64f;
        [SerializeField] private float innerMargin = 8f;

        private static Font cachedFont;

        private Canvas hudCanvas;
        private RectTransform listRoot;
        private readonly Dictionary<BattleUnit, PartyHudEntry> entries = new();

        private class PartyHudEntry
        {
            public BattleUnit Unit;
            public RectTransform Root;
            public Image HpFill;
            public Text HpText;
            public Button UltimateButton;
            public Text UltimateButtonText;
        }

        private void OnEnable()
        {
            EnsureCanvasAndEventSystem();
            if (controller != null)
            {
                controller.OnPartySpawned += HandlePartySpawned;
            }
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.OnPartySpawned -= HandlePartySpawned;
            }
        }

        private void Update()
        {
            foreach (KeyValuePair<BattleUnit, PartyHudEntry> pair in entries)
            {
                RefreshEntry(pair.Value);
            }
        }

        private void HandlePartySpawned(IReadOnlyList<BattleUnit> party)
        {
            ClearEntries();
            foreach (BattleUnit unit in party)
            {
                CreateEntry(unit);
            }
        }

        private void ClearEntries()
        {
            foreach (KeyValuePair<BattleUnit, PartyHudEntry> pair in entries)
            {
                if (pair.Value.Root != null)
                {
                    Destroy(pair.Value.Root.gameObject);
                }
            }

            entries.Clear();
        }

        /// <summary>
        /// UI一式(Canvas/PartyList)を初回のみ作成する。DungeonBattleView.EnsureVisualsRootと同様、
        /// Edit Mode上での操作やドメインリロード後に再度呼ばれても、既にシーンに存在する同名の子を
        /// 再利用して重複生成を防ぐ(GameScreenControllerが表示/非表示を切り替える対象は、この
        /// Canvas自体を指す永続的な参照であるため、実行のたびに作り直さないようにする必要がある)。
        /// </summary>
        private void EnsureCanvasAndEventSystem()
        {
            if (hudCanvas == null)
            {
                Transform existingCanvas = transform.Find("DungeonPartyHudCanvas");
                GameObject canvasGo;
                if (existingCanvas != null)
                {
                    canvasGo = existingCanvas.gameObject;
                }
                else
                {
                    canvasGo = new GameObject("DungeonPartyHudCanvas",
                        typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                    canvasGo.transform.SetParent(transform, false);
                }

                hudCanvas = canvasGo.GetComponent<Canvas>();
                hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                hudCanvas.sortingOrder = 100;

                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, 1920f);
                scaler.matchWidthOrHeight = 1f;

                Transform existingList = canvasGo.transform.Find("PartyList");
                GameObject listGo;
                if (existingList != null)
                {
                    listGo = existingList.gameObject;
                }
                else
                {
                    listGo = new GameObject("PartyList", typeof(RectTransform), typeof(VerticalLayoutGroup));
                    listGo.transform.SetParent(canvasGo.transform, false);
                }

                listRoot = listGo.GetComponent<RectTransform>();
                listRoot.anchorMin = new Vector2(1f, 1f);
                listRoot.anchorMax = new Vector2(1f, 1f);
                listRoot.pivot = new Vector2(1f, 1f);
                listRoot.anchoredPosition = new Vector2(-screenMargin.x, -screenMargin.y);

                var vlg = listGo.GetComponent<VerticalLayoutGroup>();
                vlg.spacing = rowSpacing;
                vlg.childAlignment = TextAnchor.UpperRight;
                vlg.childForceExpandWidth = false;
                vlg.childForceExpandHeight = false;
                vlg.childControlWidth = false;
                vlg.childControlHeight = false;
            }

            // uGUIのボタン等はEventSystemが無いと入力を受け取れない。このプロジェクトは新Input System
            // 専用(Player Settings上でActive Input Handler=Input System Package)のため、
            // 旧StandaloneInputModuleではなくInputSystemUIInputModuleを使う。
            if (Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
            {
                var esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            }
        }

        private void CreateEntry(BattleUnit unit)
        {
            var rowGo = new GameObject($"PartyHudEntry_{unit.DisplayName}", typeof(RectTransform), typeof(Image));
            rowGo.transform.SetParent(listRoot, false);
            var rowRect = rowGo.GetComponent<RectTransform>();
            rowRect.sizeDelta = rowSize;
            rowGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);

            float portraitX = innerMargin;
            float portraitY = (rowSize.y - portraitSize) * 0.5f;
            SetRect(CreateChild(rowGo.transform, "Portrait", typeof(Image)), portraitX, portraitY, portraitSize, portraitSize);
            Image portraitImage = rowGo.transform.Find("Portrait").GetComponent<Image>();
            Sprite portraitSprite = unit.Species != null ? unit.Species.BattleSprite : null;
            if (portraitSprite == null)
            {
                portraitSprite = RuntimeSpriteFactory.GetCircleSprite(
                    RuntimeSpriteFactory.GetFallbackColor(unit.AttackType, unit.IsPlayerSide));
            }

            portraitImage.sprite = portraitSprite;
            portraitImage.preserveAspect = true;

            float infoX = innerMargin + portraitSize + innerMargin;
            float infoWidth = rowSize.x - infoX - ultimateButtonSize - innerMargin - innerMargin;

            const float nameHeight = 28f;
            float nameY = rowSize.y - innerMargin - nameHeight;
            RectTransform nameRect = CreateChild(rowGo.transform, "Name", typeof(Text));
            SetRect(nameRect, infoX, nameY, infoWidth, nameHeight);
            Text nameText = nameRect.GetComponent<Text>();
            nameText.text = unit.DisplayName;
            nameText.font = GetDefaultFont();
            nameText.fontSize = 22;
            nameText.color = Color.white;
            nameText.alignment = TextAnchor.MiddleLeft;

            const float hpBarHeight = 24f;
            float hpBarY = innerMargin;
            RectTransform hpBarRect = CreateChild(rowGo.transform, "HpBar", typeof(Image));
            SetRect(hpBarRect, infoX, hpBarY, infoWidth, hpBarHeight);
            hpBarRect.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

            var hpFillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            hpFillGo.transform.SetParent(hpBarRect, false);
            RectTransform hpFillRect = hpFillGo.GetComponent<RectTransform>();
            hpFillRect.anchorMin = Vector2.zero;
            hpFillRect.anchorMax = Vector2.one;
            hpFillRect.offsetMin = new Vector2(2f, 2f);
            hpFillRect.offsetMax = new Vector2(-2f, -2f);
            Image hpFillImage = hpFillGo.GetComponent<Image>();
            hpFillImage.type = Image.Type.Filled;
            hpFillImage.fillMethod = Image.FillMethod.Horizontal;
            hpFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            hpFillImage.color = new Color(0.3f, 0.85f, 0.95f);

            var hpTextGo = new GameObject("HpText", typeof(RectTransform), typeof(Text));
            hpTextGo.transform.SetParent(hpBarRect, false);
            RectTransform hpTextRect = hpTextGo.GetComponent<RectTransform>();
            hpTextRect.anchorMin = Vector2.zero;
            hpTextRect.anchorMax = Vector2.one;
            hpTextRect.offsetMin = Vector2.zero;
            hpTextRect.offsetMax = Vector2.zero;
            Text hpText = hpTextGo.GetComponent<Text>();
            hpText.font = GetDefaultFont();
            hpText.fontSize = 14;
            hpText.alignment = TextAnchor.MiddleCenter;
            hpText.color = Color.white;

            float buttonX = rowSize.x - innerMargin - ultimateButtonSize;
            float buttonY = (rowSize.y - ultimateButtonSize) * 0.5f;
            RectTransform buttonRect = CreateChild(rowGo.transform, "UltimateButton", typeof(Image), typeof(Button));
            SetRect(buttonRect, buttonX, buttonY, ultimateButtonSize, ultimateButtonSize);
            buttonRect.GetComponent<Image>().sprite = RuntimeSpriteFactory.GetCircleSprite(new Color(1f, 0.85f, 0.2f));
            Button ultimateButton = buttonRect.GetComponent<Button>();
            BattleUnit capturedUnit = unit;
            DungeonRunController capturedController = controller;
            ultimateButton.onClick.AddListener(() => capturedController.TryActivateUltimate(capturedUnit));

            var buttonTextGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            buttonTextGo.transform.SetParent(buttonRect, false);
            RectTransform buttonTextRect = buttonTextGo.GetComponent<RectTransform>();
            buttonTextRect.anchorMin = Vector2.zero;
            buttonTextRect.anchorMax = Vector2.one;
            buttonTextRect.offsetMin = Vector2.zero;
            buttonTextRect.offsetMax = Vector2.zero;
            Text buttonText = buttonTextGo.GetComponent<Text>();
            buttonText.text = "必殺技";
            buttonText.font = GetDefaultFont();
            buttonText.fontSize = 14;
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.color = Color.black;

            var entry = new PartyHudEntry
            {
                Unit = unit,
                Root = rowRect,
                HpFill = hpFillImage,
                HpText = hpText,
                UltimateButton = ultimateButton,
                UltimateButtonText = buttonText,
            };
            entries[unit] = entry;
            RefreshEntry(entry);
        }

        private void RefreshEntry(PartyHudEntry entry)
        {
            BattleUnit unit = entry.Unit;
            int maxHp = Mathf.Max(1, unit.Stats.maxHp);
            float ratio = Mathf.Clamp01((float)unit.CurrentHp / maxHp);
            entry.HpFill.fillAmount = ratio;
            entry.HpText.text = $"{unit.CurrentHp}/{maxHp}";

            if (!unit.HasUltimate)
            {
                entry.UltimateButton.gameObject.SetActive(false);
                return;
            }

            entry.UltimateButton.gameObject.SetActive(true);
            entry.UltimateButton.interactable = unit.CanActivateUltimate;
            entry.UltimateButtonText.text = unit.CanActivateUltimate
                ? "必殺技"
                : Mathf.CeilToInt(unit.UltimateCooldownRemaining).ToString();
        }

        /// <summary>anchorMin=anchorMax=pivot=(0,0)の子を作り、(x,y,width,height)をそのまま
        /// 親の左下原点からのローカル座標として設定する(複雑なストレッチアンカーの計算を避けるため)。</summary>
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
