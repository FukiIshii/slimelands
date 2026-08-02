using System;
using UnityEngine;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// 遠距離タイプの通常攻撃/必殺技で発射される「スライム弾」の見た目。
    /// コルーチンではなくUpdate()内の経過時間加算で移動させる(BattleUnitViewと同じ理由)。
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class BattleProjectile : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private Vector3 start;
        private Vector3 end;
        private float duration;
        private float elapsed;
        private Action onArrive;
        private bool launched;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        /// <summary>fromからtoへtravelDuration秒かけて移動し、到達したらonArrivalを呼んで自身を破棄する。</summary>
        public void Launch(Vector3 from, Vector3 to, float travelDuration, Color color, Action onArrival)
        {
            start = from;
            end = to;
            duration = Mathf.Max(0.05f, travelDuration);
            elapsed = 0f;
            onArrive = onArrival;
            launched = true;

            transform.position = from;
            transform.localScale = Vector3.one * 0.32f;
            spriteRenderer.sprite = RuntimeSpriteFactory.GetCircleSprite(color);
            spriteRenderer.sortingOrder = 15;
        }

        private void Update()
        {
            if (!launched)
            {
                return;
            }

            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(start, end, p);

            if (p >= 1f)
            {
                launched = false;
                Action callback = onArrive;
                onArrive = null;
                callback?.Invoke();
                Destroy(gameObject);
            }
        }
    }
}
