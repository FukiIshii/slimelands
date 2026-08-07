using System.Collections.Generic;
using UnityEngine;
using Slimelands.Cards;

namespace Slimelands.Village
{
    /// <summary>
    /// ガチャの排出対象一覧。排出確率そのもの(重み)はSlimeSpeciesData.GachaWeightが持ち、
    /// このアセットは「今どのスライムカードが排出対象か」をデータとして管理する
    /// (コード側にハードコードしない)。
    /// </summary>
    [CreateAssetMenu(fileName = "NewGachaPool", menuName = "Slimelands/Village/Gacha Pool", order = 50)]
    public class GachaPoolData : ScriptableObject
    {
        [SerializeField] private List<SlimeCardData> entries = new();

        public IReadOnlyList<SlimeCardData> Entries => entries;

        /// <summary>
        /// SlimeSpeciesData.GachaWeightに基づく重み付き抽選で1件選ぶ。排出対象が無い、または
        /// 重みの合計が0以下の場合はnullを返す。GachaManager(UIボタン経由のガチャ)と
        /// CardRecipeData.RandomResultPool(カードを重ねて引くガチャ)の両方から共通で使う。
        /// </summary>
        public SlimeCardData PickWeighted()
        {
            int totalWeight = 0;
            foreach (SlimeCardData entry in entries)
            {
                if (entry != null && entry.Species != null)
                {
                    totalWeight += Mathf.Max(0, entry.Species.GachaWeight);
                }
            }

            if (totalWeight <= 0)
            {
                return null;
            }

            int roll = Random.Range(0, totalWeight);
            int cumulative = 0;
            foreach (SlimeCardData entry in entries)
            {
                if (entry == null || entry.Species == null)
                {
                    continue;
                }

                cumulative += Mathf.Max(0, entry.Species.GachaWeight);
                if (roll < cumulative)
                {
                    return entry;
                }
            }

            return null;
        }
    }
}
