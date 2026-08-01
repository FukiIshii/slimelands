namespace Slimelands.Dungeon
{
    /// <summary>ダンジョン進行の状態。</summary>
    public enum DungeonState
    {
        NotStarted,
        WaitingForEncounter,
        InBattle,
        Cleared,
        Failed,
    }
}
