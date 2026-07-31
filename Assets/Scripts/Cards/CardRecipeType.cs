namespace Slimelands.Cards
{
    /// <summary>
    /// 合成レシピの種類。素材の消費の仕方が異なる。
    /// </summary>
    public enum CardRecipeType
    {
        /// <summary>制作系: スライムは消費せず、その場に残したまま結果カードを生み出す
        /// (例: スライムの力を借りて何かを作るが、スライム自体は消費されない)。</summary>
        Production,

        /// <summary>合成系: スライムを含む全ての素材を消費して結果カードを生み出す。</summary>
        Synthesis,
    }
}
