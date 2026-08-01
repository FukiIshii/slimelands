namespace Slimelands.Slime
{
    /// <summary>
    /// スライムの属性(タイプ)。配合の組み合わせ判定などに使用する。
    /// GDD未確定のため種類は仮。今後の仕様確定に合わせて追加・変更する。
    /// </summary>
    public enum SlimeType
    {
        Neutral,
        Fire,
        Water,
        Earth,
        Wind,
        Light,
        Dark,
        // 既存アセットのシリアライズ値(整数)を壊さないよう、新しい属性は必ず末尾に追加すること。
        Grass,
    }
}
