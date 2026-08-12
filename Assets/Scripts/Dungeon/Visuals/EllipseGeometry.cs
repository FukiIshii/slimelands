using UnityEngine;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// 楕円の輪郭を線分の集合として近似計算する。UltimateHitboxGizmoDebugView(Play Mode中の
    /// Gizmos描画)とSlimeSkillDataEditor(Edit Mode中のScene Viewプレビュー、Handles描画)の
    /// 両方から参照される、描画用の純粋なジオメトリ計算(判定ロジックには関与しない)。
    /// </summary>
    public static class EllipseGeometry
    {
        public static Vector3[] BuildOutline(Vector3 center, float radiusX, float radiusY, int segments = 48)
        {
            var points = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments * Mathf.PI * 2f;
                points[i] = center + new Vector3(Mathf.Cos(t) * radiusX, Mathf.Sin(t) * radiusY, 0f);
            }

            return points;
        }
    }
}
