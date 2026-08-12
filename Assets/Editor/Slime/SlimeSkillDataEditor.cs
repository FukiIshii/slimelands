using UnityEditor;
using UnityEngine;
using Slimelands.Slime;

namespace Slimelands.Slime.Editor
{
    /// <summary>
    /// SlimeSkillDataのInspectorで必殺技の範囲(Circle/Line/Sweep/Meteorのareaサイズ関連)を
    /// 選んだ際、Scene View上にプレビュー用のハンドルを表示し、ドラッグでサイズを調整できるように
    /// する。実際の発動位置(origin)は戦闘中の対象・発動者の座標から都度計算されるため固定値として
    /// 存在せず、ここではワールド原点(0,0,0)を仮の基準点としてプレビューする(実際のダンジョンでの
    /// 見え方はUltimateHitboxGizmoDebugView(Play Mode専用)で確認する)。
    /// Play Modeでなくても(アセットをProjectウィンドウで選択するだけで)操作できる。
    /// </summary>
    [CustomEditor(typeof(SlimeSkillData))]
    public class SlimeSkillDataEditor : UnityEditor.Editor
    {
        private static readonly Vector3 PreviewOrigin = Vector3.zero;
        private static readonly Color HandleColor = new(1f, 0.25f, 0.25f, 0.9f);

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var skill = (SlimeSkillData)target;
            if (skill.IsUltimate && skill.AreaShape != UltimateAreaShape.SingleTarget)
            {
                EditorGUILayout.HelpBox(
                    "Scene View上(ワールド原点付近)に範囲プレビュー用のハンドルを表示しています。" +
                    "ドラッグしてサイズを調整できます。", MessageType.Info);
            }
        }

        private void OnSceneGUI()
        {
            var skill = (SlimeSkillData)target;
            if (!skill.IsUltimate)
            {
                return;
            }

            serializedObject.Update();
            Handles.color = HandleColor;

            switch (skill.AreaShape)
            {
                case UltimateAreaShape.Circle:
                case UltimateAreaShape.Meteor:
                    DrawRadiusHandle(PreviewOrigin, "areaRadius");
                    break;
                case UltimateAreaShape.Line:
                    DrawLineHandles();
                    break;
                case UltimateAreaShape.Sweep:
                    DrawSweepWidthHandle();
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>中心originを持つ円(Circle/Meteor共通、areaRadius)。円周上の点をXY平面内で自由に
        /// ドラッグさせ、その点までの距離を新しい半径として反映する。</summary>
        private void DrawRadiusHandle(Vector3 origin, string propertyName)
        {
            SerializedProperty prop = serializedObject.FindProperty(propertyName);
            float radius = prop.floatValue;

            Handles.DrawWireDisc(origin, Vector3.forward, radius);

            Vector3 handlePos = origin + Vector3.right * radius;
            EditorGUI.BeginChangeCheck();
            Vector3 newHandlePos = Handles.FreeMoveHandle(handlePos, HandleUtility.GetHandleSize(handlePos) * 0.1f,
                Vector3.zero, Handles.DotHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                float newRadius = Mathf.Max(0f, Vector3.Distance(origin, newHandlePos));
                prop.floatValue = newRadius;
            }

            Handles.Label(handlePos + Vector3.up * 0.2f, propertyName + " = " + prop.floatValue.ToString("F2"));
        }

        /// <summary>発動者からareaLengthだけ+X方向へ伸びる矩形(半幅areaRadius)。長さと半幅を
        /// それぞれ独立したハンドルで調整できるようにする。</summary>
        private void DrawLineHandles()
        {
            SerializedProperty lengthProp = serializedObject.FindProperty("areaLength");
            SerializedProperty radiusProp = serializedObject.FindProperty("areaRadius");
            float length = lengthProp.floatValue;
            float halfWidth = radiusProp.floatValue;

            Vector3 origin = PreviewOrigin;
            Vector3 boxCenter = origin + Vector3.right * (length * 0.5f);
            Vector3 boxSize = new(length, halfWidth * 2f, 0.1f);

            Matrix4x4 previousMatrix = Handles.matrix;
            Handles.matrix = Matrix4x4.identity;
            Handles.DrawWireCube(boxCenter, boxSize);
            Handles.matrix = previousMatrix;

            // 長さ調整ハンドル(+X方向の先端)。
            Vector3 lengthHandlePos = origin + Vector3.right * length;
            EditorGUI.BeginChangeCheck();
            Vector3 newLengthHandlePos = Handles.Slider(lengthHandlePos, Vector3.right,
                HandleUtility.GetHandleSize(lengthHandlePos) * 0.15f, Handles.ConeHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                lengthProp.floatValue = Mathf.Max(0f, newLengthHandlePos.x - origin.x);
            }

            // 半幅調整ハンドル(矩形中心から+Y方向)。
            Vector3 widthHandlePos = boxCenter + Vector3.up * halfWidth;
            EditorGUI.BeginChangeCheck();
            Vector3 newWidthHandlePos = Handles.Slider(widthHandlePos, Vector3.up,
                HandleUtility.GetHandleSize(widthHandlePos) * 0.15f, Handles.ConeHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                radiusProp.floatValue = Mathf.Max(0f, newWidthHandlePos.y - boxCenter.y);
            }

            Handles.Label(lengthHandlePos + Vector3.up * 0.2f, "areaLength = " + lengthProp.floatValue.ToString("F2"));
            Handles.Label(widthHandlePos + Vector3.right * 0.2f, "areaRadius(半幅) = " + radiusProp.floatValue.ToString("F2"));
        }

        /// <summary>Sweepの帯の半幅(areaRadius)のみ調整する。帯の高さは行動帯の高さ
        /// (DungeonRunController.movementBandHeight、シーン側の値でこのアセットには含まれない)
        /// で決まるため、プレビューは仮の高さ(3ワールド単位)で表示する。</summary>
        private void DrawSweepWidthHandle()
        {
            const float previewHeight = 3f;
            SerializedProperty radiusProp = serializedObject.FindProperty("areaRadius");
            float halfWidth = radiusProp.floatValue;

            Vector3 origin = PreviewOrigin;
            Vector3 boxSize = new(halfWidth * 2f, previewHeight, 0.1f);
            Handles.DrawWireCube(origin, boxSize);

            Vector3 widthHandlePos = origin + Vector3.right * halfWidth;
            EditorGUI.BeginChangeCheck();
            Vector3 newWidthHandlePos = Handles.Slider(widthHandlePos, Vector3.right,
                HandleUtility.GetHandleSize(widthHandlePos) * 0.15f, Handles.ConeHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                radiusProp.floatValue = Mathf.Max(0f, newWidthHandlePos.x - origin.x);
            }

            Handles.Label(widthHandlePos + Vector3.up * 0.2f, "areaRadius(半幅) = " + radiusProp.floatValue.ToString("F2")
                + "\n(高さは行動帯の設定に依存、プレビューは仮の高さ)");
        }
    }
}
