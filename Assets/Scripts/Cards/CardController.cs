using TMPro;
using UnityEngine;
using Slimelands.Slime;
using Slimelands.UI;

namespace Slimelands.Cards
{
    /// <summary>
    /// 盤面に置かれた1枚のカードの見た目を管理する。
    /// ドラッグ・重なりの管理はCardStack、入力の受付・合成判定はCardBoardが担う。
    /// 見た目は背景の「Frame」子と前景の「Icon」子の2枚のSpriteRendererで構成する(どちらも
    /// このコンポーネントが自己修復的に生成する。プレハブ自体には手を加えていない)。
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Collider2D))]
    public class CardController : MonoBehaviour
    {
        [Tooltip("frame画像の縦横比を保ったまま収める目標サイズ(ワールド単位、正方形)。" +
            "当たり判定(BoxCollider2D)からはみ出さないよう、frame画像のピクセルサイズがまちまちでも" +
            "自動的に縮小して揃える。")]
        [SerializeField] private float frameTargetSize = 1f;
        [Tooltip("icon画像を、frameの短辺に対してこの割合のサイズで中央に収める(frame未設定の場合は" +
            "frameTargetSizeを基準にする)。icon画像の実ピクセルサイズが素材ごとにまちまちでも" +
            "(例: 小さいスライム絵と大きいプレースホルダー絵が混在)、frameの絵柄の窓に対して" +
            "常に同じ相対サイズで表示されるようにするための補正。")]
        [SerializeField, Range(0.1f, 1f)] private float iconFillRatio = 0.7f;

        [SerializeField] private CardData data;
        [Tooltip("GachaGateCardDataの残りガチャクリスタル数の表示ラベルを、カード下端からどれだけ" +
            "下にずらして配置するか(ワールド単位)。")]
        [SerializeField] private float requirementLabelYOffset = -0.62f;

        [Header("装備インジケーター")]
        [Tooltip("スライムがアクセサリーを装備している間にカード下部へ表示する、小さな楕円形" +
            "インジケーターのY方向オフセット(ワールド単位)。")]
        [SerializeField] private float equipmentIndicatorYOffset = -0.62f;
        [Tooltip("装備インジケーターの表示サイズ(ワールド単位、幅×高さ)。横長にすることで楕円形に見せる。")]
        [SerializeField] private Vector2 equipmentIndicatorSize = new Vector2(0.28f, 0.16f);

        [Header("名前欄")]
        [Tooltip("frame画像上部の名前欄(上端の枠線と区切り線の間の帯)の中心Y座標。" +
            "frameの高さに対する比率(0=中心、0.5=上端)。cardframe_resources/crystals/" +
            "resourceNodes/slimes/buildingいずれも同じ帯位置で作られているため共通の値を使う。")]
        [SerializeField, Range(0f, 0.5f)] private float nameLabelCenterYRatio = 0.432f;
        [Tooltip("名前欄の高さ。frameの高さに対する比率。")]
        [SerializeField, Range(0.01f, 0.3f)] private float nameLabelHeightRatio = 0.094f;
        [Tooltip("名前欄の幅。frameの幅に対する比率(左右の枠線ぶんの余白を残すため1未満にする)。")]
        [SerializeField, Range(0.1f, 1f)] private float nameLabelWidthRatio = 0.85f;

        private SpriteRenderer frameRenderer;
        private SpriteRenderer iconRenderer;
        private TextMeshPro requirementLabel;
        private TextMeshPro nameLabel;
        // -1は未初期化を表す(EnsureGachaChargesInitializedでGachaGateCardData.RequiredCrystalCountから
        // 初期化する)。
        private int gachaChargesRemaining = -1;

        private GameObject equipmentIndicatorObject;
        private SpriteRenderer equipmentIndicatorRenderer;
        private static Sprite equipmentIndicatorSprite;

        /// <summary>
        /// このカードが「装備中アクセサリーのオーバーレイ表示」として一時的に盤面へ出現した
        /// 複製かどうかを示す。null以外なら、装備元のスライムカードを指す。
        /// CardBoard.HandleEquipmentPreviewDropがドロップ結果(装備解除/キャンセル)を判定するために使う。
        /// </summary>
        public CardController EquipmentPreviewOwner { get; private set; }

        /// <summary>
        /// (スライムカード側が持つ)現在オーバーレイ表示中のアクセサリープレビューカード。
        /// 未表示ならnull。CardBoardがインジケーターのトグル状態を判定するために使う。
        /// </summary>
        public CardController ActiveEquipmentPreview { get; private set; }

        public CardData Data => data;
        public CardStack ParentStack { get; private set; }

        /// <summary>
        /// GachaGateCardDataの場合のみ意味を持つ、次にスライムが生まれるまでの残りガチャクリスタル数。
        /// 1個消費されるたびにCardBoard側からこの値を1減らし、0になった時点でランダムなスライムを
        /// 生成してからRequiredCrystalCountへ戻す(セッターでカード下部の表示ラベルも自動更新される)。
        /// </summary>
        public int GachaChargesRemaining
        {
            get => gachaChargesRemaining;
            set
            {
                gachaChargesRemaining = value;
                RefreshRequirementLabel();
            }
        }

        /// <summary>
        /// このカードがスライムカードの場合、対応する個体(SlimeIndividual)。
        /// SlimeCardDataは種族を指すだけの共有アセットのため、カード1枚ごとの個体差
        /// (レベル・HP等)はこちらで別途保持する。スライム以外のカードでは常にnull。
        /// </summary>
        public SlimeIndividual BoundIndividual { get; private set; }

        public void BindSlimeIndividual(SlimeIndividual individual)
        {
            BoundIndividual = individual;
            RefreshEquipmentIndicator();
        }

        /// <summary>装備解除の起点となる、盤面から切り離されたアクセサリーカード自身に対して呼ぶ。</summary>
        public void MarkAsEquipmentPreview(CardController owner)
        {
            EquipmentPreviewOwner = owner;
        }

        public void ClearEquipmentPreviewOwner()
        {
            EquipmentPreviewOwner = null;
        }

        /// <summary>スライムカード側に、現在表示中のプレビューカードの参照を持たせる(トグル判定用)。</summary>
        public void SetActiveEquipmentPreview(CardController preview)
        {
            ActiveEquipmentPreview = preview;
        }

        public void ClearActiveEquipmentPreview()
        {
            ActiveEquipmentPreview = null;
        }

        /// <summary>
        /// BoundIndividualの装備状態(EquippedAccessory)に応じて、カード下部の小さな楕円形
        /// インジケーターの表示/非表示を切り替える。装備/解除のたびにCardBoard側から呼ばれる。
        /// </summary>
        public void RefreshEquipmentIndicator()
        {
            bool shouldShow = BoundIndividual != null && BoundIndividual.EquippedAccessory != null;
            if (!shouldShow)
            {
                if (equipmentIndicatorObject != null)
                {
                    equipmentIndicatorObject.SetActive(false);
                }

                return;
            }

            EnsureEquipmentIndicator();
            equipmentIndicatorObject.SetActive(true);
        }

        private void EnsureEquipmentIndicator()
        {
            if (equipmentIndicatorObject != null)
            {
                return;
            }

            Transform existing = transform.Find("EquipmentIndicator");
            GameObject go = existing != null ? existing.gameObject : new GameObject("EquipmentIndicator");
            if (existing == null)
            {
                go.transform.SetParent(transform, false);
            }

            go.transform.localPosition = new Vector3(0f, equipmentIndicatorYOffset, 0f);
            go.transform.localScale = new Vector3(equipmentIndicatorSize.x, equipmentIndicatorSize.y, 1f);

            SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = go.AddComponent<SpriteRenderer>();
            }

            renderer.sprite = EnsureIndicatorSprite();
            renderer.sortingOrder = EnsureFrameRenderer().sortingOrder + 4;

            if (go.GetComponent<Collider2D>() == null)
            {
                CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
                collider.radius = 0.5f;
            }

            if (go.GetComponent<EquipmentIndicatorMarker>() == null)
            {
                go.AddComponent<EquipmentIndicatorMarker>();
            }

            equipmentIndicatorObject = go;
            equipmentIndicatorRenderer = renderer;
        }

        /// <summary>
        /// 専用の絵素材が無いため、実行時に単色の楕円テクスチャを1枚だけ生成して全カードで共有する
        /// (CardDescriptionUI.MakeSolidTextureと同様、簡易UI要素をコードで生成するパターンを踏襲)。
        /// </summary>
        private static Sprite EnsureIndicatorSprite()
        {
            if (equipmentIndicatorSprite != null)
            {
                return equipmentIndicatorSprite;
            }

            const int width = 64;
            const int height = 64;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            Vector2 center = new Vector2((width - 1) * 0.5f, (height - 1) * 0.5f);
            var fillColor = new Color(0.95f, 0.85f, 0.3f, 0.95f);
            var clearColor = new Color(0f, 0f, 0f, 0f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = (x - center.x) / center.x;
                    float ny = (y - center.y) / center.y;
                    bool inside = nx * nx + ny * ny <= 1f;
                    texture.SetPixel(x, y, inside ? fillColor : clearColor);
                }
            }

            texture.Apply();
            equipmentIndicatorSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
            return equipmentIndicatorSprite;
        }

        /// <summary>カード背景のフレームを描画する子SpriteRenderer。他システムからはsortingOrderの
        /// 基準値としてのみ参照される(スタック内の重なり順の追跡・比較用)。</summary>
        public SpriteRenderer SpriteRenderer => EnsureFrameRenderer();

        /// <summary>
        /// エディタ上でシーンに直接配置されたカードはInitialize()を経由しないため、
        /// 起動時点でも見た目(フレーム+アイコン)が正しく反映されるようAwakeでも描画し直す
        /// (BindIndividualsToPreplacedSlimeCardsと同種の自己修復パターン)。
        /// </summary>
        private void Awake()
        {
            EnsureGachaChargesInitialized();
            RefreshVisual();
            RefreshEquipmentIndicator();
        }

        public void Initialize(CardData cardData)
        {
            data = cardData;
            name = data != null ? $"Card_{data.CardName}" : "Card";
            EnsureGachaChargesInitialized();
            RefreshVisual();
        }

        /// <summary>
        /// GachaGateCardDataの場合、残りガチャクリスタル数(gachaChargesRemaining)が未初期化(-1)の
        /// ときだけRequiredCrystalCountから初期化する。シーンに直接配置されたカード(Initializeを
        /// 経由しない)でも、Awakeで自己修復的に初期化されるようにするため。
        /// </summary>
        private void EnsureGachaChargesInitialized()
        {
            if (data is GachaGateCardData gate && gachaChargesRemaining < 0)
            {
                gachaChargesRemaining = gate.RequiredCrystalCount;
            }
        }

        /// <summary>
        /// カードの見た目を、背景のフレーム(Frame子)と前景のアイコン(Icon子)の2層で描画し直す。
        /// frame未設定のカードはフレームなし(アイコンのみ)になり、既存カードとの後方互換を保つ。
        /// </summary>
        private void RefreshVisual()
        {
            // 旧実装(このGameObject自身のSpriteRendererに直接アイコンを描画する方式)で
            // 事前に配置されたシーン上のカードは、ここでクリアしないと旧スプライトがFrame/Icon子の
            // 上や下に残ったまま二重に表示されてしまう。
            SpriteRenderer root = GetComponent<SpriteRenderer>();
            if (root != null)
            {
                root.sprite = null;
            }

            SpriteRenderer frame = EnsureFrameRenderer();
            frame.sprite = data != null ? data.Frame : null;
            float frameScale = ApplyUniformFitScale(frame, frameTargetSize);
            float frameShortSide = frame.sprite != null
                ? Mathf.Min(frame.sprite.bounds.size.x, frame.sprite.bounds.size.y) * frameScale
                : frameTargetSize;

            SpriteRenderer icon = EnsureIconRenderer();
            icon.sprite = data != null ? data.Icon : null;
            ApplyUniformFitScale(icon, frameShortSide * iconFillRatio);
            icon.sortingLayerID = frame.sortingLayerID;
            icon.sortingOrder = frame.sortingOrder + 1;

            Vector2 frameRenderedSize = frame.sprite != null
                ? (Vector2)frame.sprite.bounds.size * frameScale
                : new Vector2(frameTargetSize, frameTargetSize);
            RefreshNameLabel(frameRenderedSize);
            RefreshRequirementLabel();
        }

        /// <summary>
        /// frame画像上部の名前欄にカード名を表示する。frame未設定のカード(data.Frame == null)では
        /// 表示する場所の基準がないため非表示にする。
        /// </summary>
        private void RefreshNameLabel(Vector2 frameRenderedSize)
        {
            if (data == null || data.Frame == null)
            {
                if (nameLabel != null)
                {
                    nameLabel.gameObject.SetActive(false);
                }
                return;
            }

            TextMeshPro label = EnsureNameLabel();
            label.text = data.CardName;
            label.gameObject.SetActive(true);

            label.transform.localPosition = new Vector3(0f, frameRenderedSize.y * nameLabelCenterYRatio, 0f);
            label.rectTransform.sizeDelta = new Vector2(
                frameRenderedSize.x * nameLabelWidthRatio,
                frameRenderedSize.y * nameLabelHeightRatio);
        }

        private TextMeshPro EnsureNameLabel()
        {
            if (nameLabel != null)
            {
                return nameLabel;
            }

            Transform existing = transform.Find("NameLabel");
            GameObject go = existing != null ? existing.gameObject : new GameObject("NameLabel");
            if (existing == null)
            {
                go.transform.SetParent(transform, false);
            }

            nameLabel = go.GetComponent<TextMeshPro>();
            if (nameLabel == null)
            {
                nameLabel = go.AddComponent<TextMeshPro>();
                nameLabel.font = FontRegistry.MisakiGothic;
                nameLabel.fontStyle = FontStyles.Bold;
                nameLabel.alignment = TextAlignmentOptions.Center;
                nameLabel.color = Color.black;
                nameLabel.enableWordWrapping = false;
                nameLabel.overflowMode = TextOverflowModes.Truncate;
                nameLabel.enableAutoSizing = true;
                nameLabel.fontSizeMin = 0.3f;
                nameLabel.fontSizeMax = 2.5f;
            }

            MeshRenderer meshRenderer = go.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.sortingOrder = EnsureFrameRenderer().sortingOrder + 2;
            }

            return nameLabel;
        }

        /// <summary>
        /// GachaGateCardDataの場合のみ、カード下部に残りガチャクリスタル数(gachaChargesRemaining)を
        /// 表示する。それ以外のカードでは非表示にする(スライムゲート専用の演出のため個別分岐にしている)。
        /// </summary>
        private void RefreshRequirementLabel()
        {
            if (data is GachaGateCardData)
            {
                TextMeshPro label = EnsureRequirementLabel();
                label.text = Mathf.Max(0, gachaChargesRemaining).ToString();
                label.gameObject.SetActive(true);
            }
            else if (requirementLabel != null)
            {
                requirementLabel.gameObject.SetActive(false);
            }
        }

        private TextMeshPro EnsureRequirementLabel()
        {
            if (requirementLabel != null)
            {
                return requirementLabel;
            }

            Transform existing = transform.Find("RequirementLabel");
            GameObject go = existing != null ? existing.gameObject : new GameObject("RequirementLabel");
            if (existing == null)
            {
                go.transform.SetParent(transform, false);
            }

            go.transform.localPosition = new Vector3(0f, requirementLabelYOffset, 0f);

            // 旧実装(レガシーTextMesh)で事前に配置されたシーン上のカードは、ここで取り除かないと
            // 新しいTextMeshProと二重に表示されてしまう。
            TextMesh legacyTextMesh = go.GetComponent<TextMesh>();
            if (legacyTextMesh != null)
            {
                DestroyImmediate(legacyTextMesh);
            }

            requirementLabel = go.GetComponent<TextMeshPro>();
            if (requirementLabel == null)
            {
                requirementLabel = go.AddComponent<TextMeshPro>();
                requirementLabel.font = FontRegistry.MisakiGothic;
                requirementLabel.fontSize = 6f;
                requirementLabel.fontStyle = FontStyles.Bold;
                requirementLabel.alignment = TextAlignmentOptions.Center;
                requirementLabel.color = Color.black;
            }

            MeshRenderer meshRenderer = go.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.sortingOrder = EnsureFrameRenderer().sortingOrder + 3;
            }

            return requirementLabel;
        }

        /// <summary>
        /// スプライトの縦横比を保ったまま、targetSize四方(ワールド単位)に収まるようレンダラーの
        /// GameObjectのlocalScaleを調整し、実際に適用したスケール値を返す。frame/icon画像は
        /// 素材ごとに実ピクセルサイズがまちまちなため、当たり判定からのはみ出しや、絵柄同士の
        /// 相対サイズのばらつきを防ぐためにこの補正が必要。
        /// </summary>
        private static float ApplyUniformFitScale(SpriteRenderer renderer, float targetSize)
        {
            if (renderer.sprite == null)
            {
                renderer.transform.localScale = Vector3.one;
                return 1f;
            }

            Vector2 size = renderer.sprite.bounds.size;
            float scale = Mathf.Min(
                targetSize / Mathf.Max(size.x, 0.0001f),
                targetSize / Mathf.Max(size.y, 0.0001f));
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
            return scale;
        }

        private SpriteRenderer EnsureFrameRenderer()
        {
            if (frameRenderer == null)
            {
                frameRenderer = EnsureChildRenderer("Frame");
            }

            return frameRenderer;
        }

        private SpriteRenderer EnsureIconRenderer()
        {
            if (iconRenderer == null)
            {
                iconRenderer = EnsureChildRenderer("Icon");
            }

            return iconRenderer;
        }

        private SpriteRenderer EnsureChildRenderer(string childName)
        {
            Transform existing = transform.Find(childName);
            GameObject go = existing != null ? existing.gameObject : new GameObject(childName);
            if (existing == null)
            {
                go.transform.SetParent(transform, false);
            }

            SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = go.AddComponent<SpriteRenderer>();
            }

            return renderer;
        }

        public void SetSortingOrder(int order)
        {
            EnsureFrameRenderer().sortingOrder = order;
            EnsureIconRenderer().sortingOrder = order + 1;

            if (nameLabel != null)
            {
                MeshRenderer meshRenderer = nameLabel.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    meshRenderer.sortingOrder = order + 2;
                }
            }

            if (requirementLabel != null)
            {
                MeshRenderer meshRenderer = requirementLabel.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    meshRenderer.sortingOrder = order + 3;
                }
            }

            if (equipmentIndicatorRenderer != null)
            {
                equipmentIndicatorRenderer.sortingOrder = order + 4;
            }
        }

        public void SetParentStack(CardStack stack)
        {
            ParentStack = stack;
        }
    }
}
