using System.Collections.Generic;
using UnityEngine;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// メテオ型必殺技の着地エフェクト(コマ送り+フェードアウト)専用の見た目。BattleProjectileと
    /// 同様、特定のBattleUnitViewの子にはせず独立したGameObjectとして着地地点(ワールド座標)に
    /// 生成する。以前はキャスターのtransformの子として実装しており、着地後にキャスターが移動を
    /// 再開すると引きずられてしまう不具合があった(子の場合、毎フレーム位置を着地地点へ固定し
    /// 直す対処療法が必要だった)。独立したGameObjectにすることで、そもそもキャスターの移動の
    /// 影響を受けない構造にする。全コマ再生+フェードアウトが終わったら自動で自身を破棄する。
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class MeteorImpactEffectView : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private IReadOnlyList<Sprite> frames;
        private float frameDuration;
        private float fadeDuration;
        private int frameIndex;
        private float frameTimer;
        private bool fading;
        private float fadeTimer;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        /// <summary>指定位置に着地エフェクトを再生開始する。framesは1枚以上必須。</summary>
        public void Play(Vector3 position, IReadOnlyList<Sprite> effectFrames, float perFrameDuration,
            float effectFadeDuration, float scale, int sortingOrder)
        {
            frames = effectFrames;
            frameDuration = Mathf.Max(0.01f, perFrameDuration);
            fadeDuration = Mathf.Max(0.01f, effectFadeDuration);
            frameIndex = 0;
            frameTimer = 0f;
            fading = false;
            fadeTimer = 0f;

            transform.position = position;
            transform.localScale = Vector3.one * scale;
            spriteRenderer.sprite = frames[0];
            spriteRenderer.color = Color.white;
            spriteRenderer.sortingOrder = sortingOrder;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void Tick(float dt)
        {
            if (frames == null)
            {
                return;
            }

            if (fading)
            {
                fadeTimer += dt;
                float alpha = 1f - Mathf.Clamp01(fadeTimer / fadeDuration);
                Color color = spriteRenderer.color;
                color.a = alpha;
                spriteRenderer.color = color;

                if (fadeTimer >= fadeDuration)
                {
                    frames = null;
                    Destroy(gameObject);
                }

                return;
            }

            frameTimer += dt;
            if (frameTimer < frameDuration)
            {
                return;
            }

            frameTimer -= frameDuration;
            frameIndex++;
            if (frameIndex >= frames.Count)
            {
                frameIndex = frames.Count - 1;
                fading = true;
                fadeTimer = 0f;
                return;
            }

            spriteRenderer.sprite = frames[frameIndex];
        }

#if UNITY_EDITOR
        /// <summary>テスト専用: このツール実行環境ではPlay Mode中にTime.deltaTimeが進まないことが
        /// あるため、明示的なdtを与えて再生を手動で進行させ、動作確認できるようにする。</summary>
        public void DebugAdvance(float dt)
        {
            Tick(dt);
        }

        /// <summary>テスト専用: 現在表示中のスプライトを外部から確認するためのデバッグ用アクセサ。</summary>
        public Sprite DebugCurrentSprite => spriteRenderer != null ? spriteRenderer.sprite : null;
#endif
    }
}
