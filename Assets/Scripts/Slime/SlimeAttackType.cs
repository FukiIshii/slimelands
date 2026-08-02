namespace Slimelands.Slime
{
    /// <summary>
    /// スライムの基本攻撃の方式(GDD「攻撃・必殺技の仕様」で確定済み)。
    /// 近接=突進、遠距離=スライム弾。武器は見た目の装飾要素でしかなく、
    /// この方式自体は種族ごとに固定される。
    /// </summary>
    public enum SlimeAttackType
    {
        Melee,
        Ranged,
    }
}
