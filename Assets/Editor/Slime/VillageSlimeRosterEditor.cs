using UnityEditor;
using UnityEngine;
using Slimelands.Slime;

namespace Slimelands.Village
{
    /// <summary>
    /// VillageSlimeRosterのInspectorに、在籍スライムの必殺技をテスト用に強制変更する
    /// デバッグ操作を追加する。SlimeIndividual.SetAssignedUltimateForTesting(UNITY_EDITOR限定)を
    /// 呼ぶだけで、本来のゲームプレイ仕様(生成時ランダム抽選・以後固定)は変更しない。
    /// 在籍スライムはランタイムのみのデータ(シリアライズされない)のため、Play Mode中のみ操作できる。
    /// </summary>
    [CustomEditor(typeof(VillageSlimeRoster))]
    public class VillageSlimeRosterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var roster = (VillageSlimeRoster)target;

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("デバッグ: 必殺技を強制変更(テスト専用)", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Play Mode中のみ操作できます(在籍スライムはランタイムのみのデータのため)。", MessageType.Info);
                return;
            }

            if (roster.Slimes.Count == 0)
            {
                EditorGUILayout.HelpBox("在籍スライムがいません。", MessageType.Info);
                return;
            }

            foreach (SlimeIndividual slime in roster.Slimes)
            {
                if (slime.Species == null || slime.Species.UltimateSkillCandidates == null
                    || slime.Species.UltimateSkillCandidates.Count == 0)
                {
                    continue;
                }

                string currentName = slime.AssignedUltimate != null ? slime.AssignedUltimate.SkillName : "(なし)";
                EditorGUILayout.LabelField($"{slime.Species.SpeciesName} Lv{slime.Level}  現在: {currentName}");

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(16f);
                foreach (SlimeSkillData skill in slime.Species.UltimateSkillCandidates)
                {
                    if (skill == null)
                    {
                        continue;
                    }

                    bool isCurrent = slime.AssignedUltimate == skill;
                    GUI.enabled = !isCurrent;
                    if (GUILayout.Button(skill.SkillName))
                    {
                        slime.SetAssignedUltimateForTesting(skill);
                    }

                    GUI.enabled = true;
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(4f);
            }
        }
    }
}
