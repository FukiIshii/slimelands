using UnityEditor;
using UnityEngine;
using Slimelands.Slime;

namespace Slimelands.Slime.Editor
{
    /// <summary>
    /// SlimeSkillDataのInspectorに、必殺技の範囲(Circle/Line/Sweep/Meteorのareaサイズ)を
    /// Scene View上にプレビュー用ハンドルとして表示している旨の案内を追加する。ハンドル自体の
    /// 描画・ドラッグ操作は<see cref="SlimeSkillDataSceneViewHandles"/>が担当する。
    /// </summary>
    [CustomEditor(typeof(SlimeSkillData))]
    public class SlimeSkillDataEditor : UnityEditor.Editor
    {
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
    }

    /// <summary>
    /// SlimeSkillDataアセットがProjectウィンドウで選択されている間、Scene View上に必殺技の範囲
    /// (Circle/Line/Sweepのareaサイズ)のプレビュー用ハンドルを表示し、ドラッグでサイズを
    /// 調整できるようにする。
    /// SlimeSkillDataはScriptableObject(シーン上のComponentではない)のため、通常の
    /// Editor.OnSceneGUIは呼ばれない(Componentを対象とするEditor専用の仕組みのため、実測で
    /// 確認済み)。代わりにSceneView.duringSceneGui(選択オブジェクトの種類を問わず、Scene Viewの
    /// 描画のたびに呼ばれる)を使う。実際の発動位置(origin)は戦闘中の対象・発動者の座標から
    /// 都度計算されるため固定値として存在せず、ここではワールド原点(0,0,0)を仮の基準点として
    /// プレビューする(実際のダンジョンでの見え方はUltimateHitboxGizmoDebugView(Play Mode専用)
    /// で確認する)。Play Modeでなくてもアセットを選択するだけで操作できる。
    /// </summary>
    [InitializeOnLoad]
    public static class SlimeSkillDataSceneViewHandles
    {
        private static readonly Vector3 PreviewOrigin = Vector3.zero;
        private static readonly Color HandleColor = new(1f, 0.25f, 0.25f, 0.9f);

        static SlimeSkillDataSceneViewHandles()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (Selection.activeObject is not SlimeSkillData skill || !skill.IsUltimate)
            {
                return;
            }

            var serializedObject = new SerializedObject(skill);
            serializedObject.Update();
            Handles.color = HandleColor;

            switch (skill.AreaShape)
            {
                case UltimateAreaShape.Circle:
                case UltimateAreaShape.Meteor:
                    DrawRadiusHandle(serializedObject, PreviewOrigin, "areaRadius");
                    break;
                case UltimateAreaShape.Line:
                    DrawLineHandles(serializedObject);
                    break;
                case UltimateAreaShape.Sweep:
                    DrawSweepWidthHandle(serializedObject);
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>中心originを持つ円(Circle/Meteor共通、areaRadius)。円周上の点をXY平面内で自由に
        /// ドラッグさせ、その点までの距離を新しい半径として反映する。</summary>
        private static void DrawRadiusHandle(SerializedObject serializedObject, Vector3 origin, string propertyName)
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
        private static void DrawLineHandles(SerializedObject serializedObject)
        {
            SerializedProperty lengthProp = serializedObject.FindProperty("areaLength");
            SerializedProperty radiusProp = serializedObject.FindProperty("areaRadius");
            float length = lengthProp.floatValue;
            float halfWidth = radiusProp.floatValue;

            Vector3 origin = PreviewOrigin;
            Vector3 boxCenter = origin + Vector3.right * (length * 0.5f);
            Vector3 boxSize = new(length, halfWidth * 2f, 0.1f);

            Handles.DrawWireCube(boxCenter, boxSize);

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
        private static void DrawSweepWidthHandle(SerializedObject serializedObject)
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
