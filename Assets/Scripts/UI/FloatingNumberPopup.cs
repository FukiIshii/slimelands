using UnityEngine;

namespace Slimelands.UI
{
    /// <summary>
    /// ワールド空間の指定位置の周辺に数値を表示し、上昇しながらフェードアウトして消える
    /// 簡易ポップアップ。TextMesh(軽量テキスト)を使い、専用のフォント/UI素材は追加しない。
    /// ダメージ量・経験値付与量など、村・ダンジョン双方の「何かの数値が増減したことを示す」
    /// 演出で共通して使う想定の汎用コンポーネント(特定パートの内部実装には依存しない)。
    /// 生成はSpawnからのみ行う想定で、寿命が尽きると自身でDestroyする。
    /// </summary>
    public class FloatingNumberPopup : MonoBehaviour
    {
        [Tooltip("表示され続ける時間(秒)。この間、上昇しながら徐々にフェードアウトして消える。")]
        [SerializeField] private float lifetime = 0.8f;
        [Tooltip("上昇する速度(ワールド単位/秒)。")]
        [SerializeField] private float riseSpeed = 1.0f;
        [Tooltip("フェードアウトを開始するタイミング(lifetimeに対する割合。0=表示直後からフェード、" +
            "1=最後の瞬間だけフェード)。")]
        [SerializeField, Range(0f, 1f)] private float fadeStartRatio = 0.35f;

        private TextMesh textMesh;
        private Color baseColor;
        private float elapsed;

        /// <summary>対象位置の少し上、かつ複数同時発生時に重ならないようX方向へ軽くばらけさせて生成する。</summary>
        public static void Spawn(Vector3 targetPosition, int value, Color color)
        {
            var go = new GameObject("FloatingNumberPopup");
            Vector3 spawnPosition = targetPosition +
                new Vector3(Random.Range(-0.3f, 0.3f), 0.5f, 0f);
            go.transform.position = spawnPosition;

            FloatingNumberPopup view = go.AddComponent<FloatingNumberPopup>();
            view.Initialize(value, color);
        }

        private void Initialize(int value, Color color)
        {
            textMesh = gameObject.AddComponent<TextMesh>();
            textMesh.text = value.ToString();
            textMesh.characterSize = 0.12f;
            textMesh.fontSize = 48;
            textMesh.fontStyle = FontStyle.Bold;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = color;

            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.sortingOrder = 30;

            baseColor = color;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            transform.position += Vector3.up * (riseSpeed * Time.deltaTime);

            float fadeStartTime = lifetime * fadeStartRatio;
            if (elapsed >= fadeStartTime)
            {
                float fadeProgress = Mathf.Clamp01((elapsed - fadeStartTime) / Mathf.Max(0.01f, lifetime - fadeStartTime));
                Color faded = baseColor;
                faded.a = Mathf.Lerp(baseColor.a, 0f, fadeProgress);
                textMesh.color = faded;
            }

            if (elapsed >= lifetime)
            {
                Destroy(gameObject);
            }
        }
    }
}
