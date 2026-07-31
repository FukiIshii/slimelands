using System.IO;
using UnityEditor;
using UnityEngine;
using Slimelands.Cards;

namespace Slimelands.Cards.Editor
{
    /// <summary>
    /// アートが未整備なカードに、カテゴリ(資源/スライム/建物)ごとに色分けした
    /// 単色四角形のプレースホルダースプライトを自動生成・割り当てるエディタ専用ツール。
    /// 同じカテゴリ内でもカード名から決定的にずらした明度を使い、個々のカードも見分けられるようにする。
    /// </summary>
    public static class CardPlaceholderIconGenerator
    {
        private const string OutputFolder = "Assets/Art/Cards/Placeholders";
        private const int TextureSize = 64;
        private const int BorderThickness = 4;
        private const float BorderShade = 0.45f;

        [MenuItem("Slimelands/Cards/Assign Placeholder Icons To All Cards")]
        private static void AssignToAllCardsMenuItem()
        {
            AssignToAllCards();
        }

        public static int AssignToAllCards()
        {
            string[] guids = AssetDatabase.FindAssets("t:CardData");
            int count = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var card = AssetDatabase.LoadAssetAtPath<CardData>(path);
                if (card == null)
                {
                    continue;
                }

                AssignPlaceholderIcon(card);
                count++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"CardPlaceholderIconGenerator: {count}件のカードにプレースホルダーアイコンを割り当てました。");
            return count;
        }

        public static void AssignPlaceholderIcon(CardData card)
        {
            Color color = GetCardColor(card);
            Sprite sprite = CreateOrUpdateSprite(card.Category, card.name, color);

            var so = new SerializedObject(card);
            so.FindProperty("icon").objectReferenceValue = sprite;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(card);
        }

        private static Color GetCategoryBaseColor(CardCategory category)
        {
            switch (category)
            {
                case CardCategory.Resource:
                    return new Color32(76, 153, 64, 255); // 緑系
                case CardCategory.Slime:
                    return new Color32(142, 68, 173, 255); // 紫系
                case CardCategory.Building:
                    return new Color32(211, 129, 42, 255); // 橙系
                default:
                    return Color.gray;
            }
        }

        private static Color GetCardColor(CardData card)
        {
            Color baseColor = GetCategoryBaseColor(card.Category);
            Color.RGBToHSV(baseColor, out float h, out float s, out float v);

            int hash = card.name.GetHashCode() & 0x7FFFFFFF;
            float normalized = (hash % 1000) / 1000f; // 0..1
            float variation = (normalized * 0.4f) - 0.2f; // -0.2..+0.2
            v = Mathf.Clamp01(v + variation);

            return Color.HSVToRGB(h, s, v);
        }

        private static Sprite CreateOrUpdateSprite(CardCategory category, string cardAssetName, Color color)
        {
            string folder = $"{OutputFolder}/{category}";
            EnsureFolder(folder);
            string path = $"{folder}/{cardAssetName}.png";

            // 重なったときに1枚ずつの境界が分かるよう、外周を少し暗い色で縁取りする。
            Color32 fillColor = color;
            Color32 borderColor = new Color(color.r * BorderShade, color.g * BorderShade, color.b * BorderShade, color.a);

            var tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            var pixels = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                bool isBorderRow = y < BorderThickness || y >= TextureSize - BorderThickness;
                for (int x = 0; x < TextureSize; x++)
                {
                    bool isBorderCol = x < BorderThickness || x >= TextureSize - BorderThickness;
                    pixels[(y * TextureSize) + x] = (isBorderRow || isBorderCol) ? borderColor : fillColor;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = TextureSize;
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}
