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
    }
}
