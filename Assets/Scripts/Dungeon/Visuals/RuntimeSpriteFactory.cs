using System.Collections.Generic;
using UnityEngine;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// 専用のアート素材がまだ用意されていないスライム/エフェクト向けに、実行時に単色の
    /// 円・正方形スプライトを生成するプレースホルダー生成器。ドット絵アセットが揃い次第、
    /// 各種呼び出し元のフォールバック経路を差し替える想定。
    /// </summary>
    internal static class RuntimeSpriteFactory
    {
        private const int TextureSize = 64;
        private const float PixelsPerUnit = 64f;

        private static readonly Dictionary<Color, Sprite> CircleCache = new();
        private static readonly Dictionary<Color, Sprite> SquareCache = new();

        /// <summary>直径1ワールド単位(PixelsPerUnit基準)の円形スプライトを色ごとにキャッシュして返す。</summary>
        public static Sprite GetCircleSprite(Color color)
        {
            if (CircleCache.TryGetValue(color, out Sprite cached) && cached != null)
            {
                return cached;
            }

            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            float radius = TextureSize * 0.5f;
            Vector2 center = new(radius, radius);
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float alpha = Mathf.Clamp01(radius - dist);
                    texture.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * alpha));
                }
            }

            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize),
                new Vector2(0.5f, 0.5f), PixelsPerUnit);
            CircleCache[color] = sprite;
            return sprite;
        }

        /// <summary>1x1ワールド単位の単色正方形スプライト(HPバー等に使用)を色ごとにキャッシュして返す。</summary>
        public static Sprite GetSquareSprite(Color color)
        {
            if (SquareCache.TryGetValue(color, out Sprite cached) && cached != null)
            {
                return cached;
            }

            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color[4 * 4];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 4, 4), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            SquareCache[color] = sprite;
            return sprite;
        }

        /// <summary>種族の攻撃タイプに応じたフォールバック色(近接=暖色寄り、遠距離=寒色寄り、視覚的に区別できればよい仮値)。</summary>
        public static Color GetFallbackColor(Slimelands.Slime.SlimeAttackType attackType, bool isPlayerSide)
        {
            if (!isPlayerSide)
            {
                return new Color(0.85f, 0.35f, 0.35f);
            }

            return attackType == Slimelands.Slime.SlimeAttackType.Melee
                ? new Color(0.35f, 0.75f, 0.4f)
                : new Color(0.35f, 0.55f, 0.9f);
        }
    }
}
