using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Slimelands.Cards;

namespace Slimelands.Cards.Editor
{
    /// <summary>
    /// CardBoardのInspectorに、テスト用にカードを盤面へ即座に配置するデバッグ操作を追加する。
    /// VillageSlimeRosterEditorと同じ位置付け(本来のゲームプレイ経路=ガチャ・配合・合成では
    /// 使わない、テスト/デバッグ作業専用)。CardBoard.SpawnStack(既存の公開API)を呼ぶだけで、
    /// カード配置ロジック自体には一切触れない。
    /// </summary>
    [CustomEditor(typeof(CardBoard))]
    public class CardBoardEditor : UnityEditor.Editor
    {
        private Dictionary<CardCategory, List<CardData>> cardsByCategory;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var board = (CardBoard)target;
            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("デバッグ: カードを配置(テスト専用)", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Play Mode中のみ操作できます(カード生成はランタイム処理のため)。", MessageType.Info);
                return;
            }

            if (cardsByCategory == null)
            {
                LoadCards();
            }

            foreach (CardCategory category in cardsByCategory.Keys.OrderBy(c => c.ToString()))
            {
                EditorGUILayout.LabelField(category.ToString(), EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginHorizontal();
                int columnCount = 0;
                foreach (CardData card in cardsByCategory[category])
                {
                    if (columnCount > 0 && columnCount % 4 == 0)
                    {
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                    }

                    if (GUILayout.Button(card.CardName))
                    {
                        SpawnCard(board, card);
                    }

                    columnCount++;
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(4f);
            }
        }

        private void LoadCards()
        {
            cardsByCategory = new Dictionary<CardCategory, List<CardData>>();

            string[] guids = AssetDatabase.FindAssets("t:CardData");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var card = AssetDatabase.LoadAssetAtPath<CardData>(path);
                if (card == null)
                {
                    continue;
                }

                if (!cardsByCategory.TryGetValue(card.Category, out List<CardData> list))
                {
                    list = new List<CardData>();
                    cardsByCategory[card.Category] = list;
                }

                list.Add(card);
            }

            foreach (List<CardData> list in cardsByCategory.Values)
            {
                list.Sort((a, b) => string.Compare(a.CardName, b.CardName, System.StringComparison.Ordinal));
            }
        }

        /// <summary>
        /// CardBoardが参照しているカメラの現在の表示中心付近へ、少しランダムにずらしながら配置する
        /// (連続してクリックしても完全に同じ座標に重なって掴みにくくならないようにするため)。
        /// </summary>
        private static void SpawnCard(CardBoard board, CardData card)
        {
            Camera camera = board.DebugTargetCamera != null ? board.DebugTargetCamera : Camera.main;
            Vector3 center = camera != null
                ? new Vector3(camera.transform.position.x, camera.transform.position.y, 0f)
                : Vector3.zero;

            Vector2 jitter = Random.insideUnitCircle * 1.5f;
            Vector3 spawnPosition = center + new Vector3(jitter.x, jitter.y, 0f);

            board.SpawnStack(card, spawnPosition);
        }
    }
}
