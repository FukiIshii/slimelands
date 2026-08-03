namespace Slimelands.Dungeon
{
    /// <summary>ダンジョンツリー上での1ノード(1ダンジョン)の解放・攻略状態。</summary>
    public enum DungeonNodeState
    {
        /// <summary>親ダンジョンが未クリアで、まだ解放されていない。</summary>
        Locked,
        /// <summary>解放済みだが、まだ一度もクリアしていない。</summary>
        UnlockedNotCleared,
        /// <summary>一度以上クリア済み(周回可能なため、以後も挑戦は可能)。</summary>
        Cleared,
    }
}
