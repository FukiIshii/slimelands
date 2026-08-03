using UnityEngine;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// ダメージを受けた瞬間、対象の周辺に数字を表示し、上昇しながらフェードアウトして消える
    /// 簡易ポップアップ。TextMesh(ワールド空間の軽量テキスト)を使い、専用のフォント/UI素材は
    /// 追加しない。生成はSpawnからのみ行う想定で、寿命が尽きると自身でDestroyする。
    /// </summary>
    public class DamageNumberView : MonoBehaviour
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
        public static void Spawn(Vector3 targetPosition, int damage, Color color)
        {
            var go = new GameObject("DamageNumber");
            Vector3 spawnPosition = targetPosition +
                new Vector3(Random.Range(-0.3f, 0.3f), 0.5f, 0f);
            go.transform.position = spawnPosition;

            DamageNumberView view = go.AddComponent<DamageNumberView>();
            view.Initialize(damage, color);
        }

        private void Initialize(int damage, Color color)
        {
            textMesh = gameObject.AddComponent<TextMesh>();
            textMesh.text = damage.ToString();
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
