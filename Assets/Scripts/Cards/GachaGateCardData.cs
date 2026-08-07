using UnityEngine;

namespace Slimelands.Cards
{
    /// <summary>
    /// ガチャクリスタルを重ねてスライムを1体ランダムに呼び出すための建物カード(スライムゲート)。
    /// 実際の「ガチャクリスタルN枚消費→ランダムなスライム1体」という挙動自体は、このカードを
    /// 対応するCardRecipeData(RandomResultPool設定済み、ガチャクリスタルの素材枠をN個並べたもの)
    /// と組み合わせてCardBoard/CardRecipeBookに登録することで実現する(合成ロジック自体は
    /// 既存の汎用レシピ機構をそのまま使い、ここに個別分岐を持たない)。
    /// requiredCrystalCountはカード下部の必要枚数表示にのみ使う値のため、対応するレシピの
    /// ガチャクリスタル素材枠の数と必ず一致させること。
    /// </summary>
    [CreateAssetMenu(fileName = "NewGachaGateCard", menuName = "Slimelands/Cards/Gacha Gate Card", order = 31)]
    public class GachaGateCardData : BuildingCardData
    {
        [Tooltip("カード下部に表示する、ガチャを引くために必要なガチャクリスタルの枚数(表示用)。" +
            "実際に消費される枚数は対応するCardRecipeDataの素材枠の数で決まるため、必ず一致させること。")]
        [SerializeField] private int requiredCrystalCount = 5;

        public int RequiredCrystalCount => requiredCrystalCount;
    }
}
