using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Slimelands.Dungeon
{
    /// <summary>
    /// 開発用デバッグ表示専用: 必殺技(Circle/Line/Sweep/Meteor)の当たり判定
    /// (DungeonRunController.OnUltimateHitboxCheckedで通知される実際のコライダークエリ形状)を
    /// Scene View上にGizmosで可視化する。実際のプレイ画面(Game View)やビルドには表示されない
    /// (Gizmosの仕様上、エディタのScene Viewでのみ描画される)。戦闘ロジック・本来の見た目の
    /// 演出には一切書き込まない、完全に読み取り専用の可視化レイヤー。
    /// 判定自体は一瞬(1フレーム)で終わるため、目視できるようdisplayDuration秒だけ
    /// 薄れながら表示し続ける。
    /// </summary>
    public class UltimateHitboxGizmoDebugView : MonoBehaviour
    {
        [SerializeField] private DungeonRunController controller;
        [Tooltip("当たり判定の表示を残す時間(秒)。判定自体は一瞬で終わるため、目視できるよう" +
            "この時間だけフェードしながら表示し続ける。")]
        [SerializeField] private float displayDuration = 0.6f;
        [SerializeField] private Color circleColor = new(1f, 0.2f, 0.2f, 0.8f);
        [SerializeField] private Color boxColor = new(0.2f, 0.6f, 1f, 0.8f);
        [SerializeField] private Color ellipseColor = new(1f, 0.6f, 0.1f, 0.8f);

        private readonly List<(UltimateHitboxDebugInfo info, float spawnTime)> recent = new();

        private void OnEnable()
        {
            if (controller != null)
            {
                controller.OnUltimateHitboxChecked += HandleHitboxChecked;
            }
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.OnUltimateHitboxChecked -= HandleHitboxChecked;
            }
        }

        private void HandleHitboxChecked(UltimateHitboxDebugInfo info)
        {
            recent.Add((info, Time.time));
        }

        /// <summary>
        /// Scene ViewはEditorの都合(マウスがScene View上にある時など)で再描画されるかどうかが
        /// 決まり、毎フレーム自動では再描画されない。何もしないと当たり判定が発生してから
        /// displayDuration秒の間に一度もScene Viewが再描画されず、表示されないままフェード
        /// アウトしてしまうことがある(実際に発生していた不具合)。表示中は毎フレーム明示的に
        /// 再描画を要求することで確実に見えるようにする。
        /// </summary>
        private void Update()
        {
#if UNITY_EDITOR
            if (recent.Count > 0)
            {
                SceneView.RepaintAll();
            }
#endif
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            for (int i = recent.Count - 1; i >= 0; i--)
            {
                float age = Time.time - recent[i].spawnTime;
                if (age > displayDuration)
                {
                    recent.RemoveAt(i);
                    continue;
                }

                float alpha = 1f - Mathf.Clamp01(age / displayDuration);
                UltimateHitboxDebugInfo info = recent[i].info;

                switch (info.Shape)
                {
                    case UltimateHitboxShapeKind.Circle:
                    {
                        Color color = circleColor;
                        color.a *= alpha;
                        Gizmos.color = color;
                        Gizmos.DrawWireSphere(info.Center, info.Radius);
                        break;
                    }
                    case UltimateHitboxShapeKind.Box:
                    {
                        Color color = boxColor;
                        color.a *= alpha;
                        Gizmos.color = color;

                        Matrix4x4 previousMatrix = Gizmos.matrix;
                        Gizmos.matrix = Matrix4x4.TRS(info.Center, Quaternion.Euler(0f, 0f, info.AngleDegrees), Vector3.one);
                        Gizmos.DrawWireCube(Vector3.zero, new Vector3(info.BoxSize.x, info.BoxSize.y, 0.1f));
                        Gizmos.matrix = previousMatrix;
                        break;
                    }
                    case UltimateHitboxShapeKind.Ellipse:
                    {
                        Color color = ellipseColor;
                        color.a *= alpha;
                        Gizmos.color = color;

                        Vector3[] outline = EllipseGeometry.BuildOutline(info.Center, info.BoxSize.x, info.BoxSize.y);
                        for (int p = 0; p < outline.Length - 1; p++)
                        {
                            Gizmos.DrawLine(outline[p], outline[p + 1]);
                        }
                        break;
                    }
                }
            }
        }
    }
}
