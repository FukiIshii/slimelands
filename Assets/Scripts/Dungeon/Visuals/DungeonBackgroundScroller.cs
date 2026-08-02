using UnityEngine;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// ダンジョン専用画面の背景を常に一定速度でスクロールさせ、パーティーが右方向へ
    /// 進んでいく感覚を演出する(背景タイル自体は右から左へ流れ、右端から出現して
    /// 左端で消える)。タイルを複数枚並べてシームレスにループさせる方式(専用シェーダー/
    /// マテリアルは使わない、既存のRuntimeSpriteFactoryと同じくプレースホルダー優先の実装)。
    /// スクロールはDungeonRunControllerの戦闘状態(WaitingForEncounter/InBattle)を一切参照せず、
    /// このコンポーネントが有効な間は常に進行し続ける(戦闘ロジックと完全に独立)。
    /// </summary>
    public class DungeonBackgroundScroller : MonoBehaviour
    {
        private const int TileCount = 3;

        [SerializeField] private DungeonRunController controller;
        [Tooltip("スクロール速度(ワールド単位/秒)。値が大きいほどパーティーが右へ進む速度が速く見える。")]
        [SerializeField] private float scrollSpeed = 2f;
        [Tooltip("背景タイル1枚分の幅(ワールド単位)。カメラの表示幅より広めに設定する。")]
        [SerializeField] private float tileWidth = 20f;
        [Tooltip("背景タイルの高さ(ワールド単位)。")]
        [SerializeField] private float tileHeight = 12f;
        [Tooltip("DungeonDataに背景スプライトが未設定の場合に使うプレースホルダー色。")]
        [SerializeField] private Color fallbackColor = new(0.55f, 0.75f, 0.85f);

        private SpriteRenderer[] tiles;
        private Transform visualRoot;
        private float scrollOffset;

        /// <summary>
        /// タイル一式をぶら下げる専用コンテナ。画面切り替え側はこれをSetActiveするだけで
        /// 背景の表示/非表示を切り替えられる。本体(このコンポーネント)は常時有効のままにし、
        /// DungeonRunControllerのイベント購読が村画面滞在中に外れてしまわないようにする
        /// (DungeonBattleViewのVisualsRootと同じ設計)。
        /// </summary>
        public GameObject Root => EnsureVisualRoot().gameObject;

        private Transform EnsureVisualRoot()
        {
            if (visualRoot == null)
            {
                visualRoot = transform.Find("BackgroundTiles");
            }

            if (visualRoot == null)
            {
                var root = new GameObject("BackgroundTiles");
                root.transform.SetParent(transform, false);
                visualRoot = root.transform;
            }

            return visualRoot;
        }

        private void Awake()
        {
            Transform root = EnsureVisualRoot();
            tiles = new SpriteRenderer[TileCount];
            for (int i = 0; i < TileCount; i++)
            {
                var go = new GameObject($"BackgroundTile_{i}", typeof(SpriteRenderer));
                go.transform.SetParent(root, false);
                SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
                renderer.sortingOrder = -100; // 常に最背面に描画する
                tiles[i] = renderer;
            }

            SetBackground(null);
        }

        private void OnEnable()
        {
            if (controller != null)
            {
                controller.OnDungeonStarted += HandleDungeonStarted;
            }
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.OnDungeonStarted -= HandleDungeonStarted;
            }
        }

        private void HandleDungeonStarted(DungeonData dungeon)
        {
            SetBackground(dungeon != null ? dungeon.BackgroundSprite : null);
        }

        /// <summary>
        /// 背景スプライトを差し替える(ダンジョンごとに絵柄を変える想定)。未設定ならプレースホルダー
        /// (単色の四角)を使う。
        /// </summary>
        public void SetBackground(Sprite sprite)
        {
            Sprite resolved = sprite != null ? sprite : RuntimeSpriteFactory.GetSquareSprite(fallbackColor);
            bool isPlaceholder = sprite == null;

            foreach (SpriteRenderer tile in tiles)
            {
                tile.sprite = resolved;
                if (isPlaceholder)
                {
                    // GetSquareSpriteはネイティブサイズが小さい単色スプライトなので、
                    // localScaleでタイル1枚分のサイズまで引き伸ばす。
                    tile.transform.localScale = new Vector3(tileWidth, tileHeight, 1f);
                }
                else
                {
                    // 専用の背景スプライトが指定されている場合は、絵柄が歪まないよう縦横同じ倍率で
                    // タイル幅(スクロールのループ単位)に合わせて引き伸ばす。結果としてタイルの
                    // 実際の高さはtileHeightと一致しないことがあるが、カメラの表示範囲を覆っていれば
                    // 問題ないため許容する(縦は多少はみ出す分には支障がない)。
                    Vector2 nativeSize = resolved.bounds.size;
                    float uniformScale = tileWidth / Mathf.Max(0.0001f, nativeSize.x);
                    tile.transform.localScale = new Vector3(uniformScale, uniformScale, 1f);
                }
            }

            scrollOffset = 0f;
            LayoutTiles();
        }

        private void Update()
        {
            scrollOffset += scrollSpeed * Time.deltaTime;
            float totalWidth = tileWidth * TileCount;
            scrollOffset %= totalWidth;
            LayoutTiles();
        }

        private void LayoutTiles()
        {
            float totalWidth = tileWidth * TileCount;
            for (int i = 0; i < tiles.Length; i++)
            {
                // scrollOffsetが増えるほどタイルは左へ移動し、左端を過ぎたら右端側へ
                // ラップさせる(右から出現して左へ抜けていく = 右方向へ進んでいる感覚)。
                float x = -totalWidth * 0.5f + i * tileWidth - scrollOffset;
                if (x < -totalWidth * 0.5f)
                {
                    x += totalWidth;
                }

                tiles[i].transform.localPosition = new Vector3(x, 0f, 0f);
            }
        }
    }
}
