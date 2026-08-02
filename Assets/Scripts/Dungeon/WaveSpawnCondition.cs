namespace Slimelands.Dungeon
{
    /// <summary>ウェーブの出現条件。</summary>
    public enum WaveSpawnCondition
    {
        /// <summary>waitSeconds待ってから出現する。</summary>
        WaitForTime,
        /// <summary>直前のウェーブを全滅させた直後、待機time無しで即座に出現する。</summary>
        ImmediateAfterPrevious,
    }
}
