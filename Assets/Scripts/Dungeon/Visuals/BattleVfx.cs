using UnityEngine;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// 突進の砂煙・弾の着弾ヒットなど、簡易パーティクル演出をまとめて生成するヘルパー。
    /// 専用のマテリアル/テクスチャはまだ用意していないため、URPの標準パーティクルシェーダーに
    /// フォールバックした簡易構成になっている(見た目の作り込みは今後の課題)。
    /// </summary>
    internal static class BattleVfx
    {
        private static Material cachedMaterial;

        /// <summary>指定位置に短時間だけ弾けるパーティクルの塊を生成する(自動で消滅する)。</summary>
        public static void SpawnBurst(Vector3 position, Color color, int particleCount = 8, float size = 0.16f)
        {
            var go = new GameObject("BattleVfxBurst");
            go.transform.position = position;

            var particleSystem = go.AddComponent<ParticleSystem>();
            // ParticleSystemはAddComponent直後にplayOnAwake(既定true)で自動再生を始めており、
            // 再生中はduration等の変更ができずエラーになるため、設定前に一旦停止させる。
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particleSystem.main;
            main.playOnAwake = false;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = 0.35f;
            main.startSpeed = 2.2f;
            main.startSize = size;
            main.startColor = color;
            main.gravityModifier = 0.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)particleCount) });

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.08f;

            var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = GetParticleMaterial();
            renderer.sortingOrder = 25;

            particleSystem.Play();
            Object.Destroy(go, main.duration + main.startLifetime.constantMax + 0.1f);
        }

        private static Material GetParticleMaterial()
        {
            if (cachedMaterial != null)
            {
                return cachedMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            cachedMaterial = new Material(shader);
            return cachedMaterial;
        }
    }
}
