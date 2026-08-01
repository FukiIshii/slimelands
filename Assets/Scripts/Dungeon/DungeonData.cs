using System.Collections.Generic;
using UnityEngine;
using Slimelands.Slime;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// ダンジョン1種類の定義。敵キャラは新しいデータ構造を作らず、既存のSlimeSpeciesDataを
    /// そのまま流用する(プレイヤーが入手できるのと同じ種族データ)。
    /// </summary>
    [CreateAssetMenu(fileName = "NewDungeon", menuName = "Slimelands/Dungeon/Dungeon Data", order = 61)]
    public class DungeonData : ScriptableObject
    {
        [SerializeField] private string dungeonName;
        [Tooltip("このダンジョンに挑むために必要なパーティーの体数。")]
        [SerializeField] private int requiredPartySize = 3;
        [Tooltip("出現する敵の種族。リストの順番通りに1体ずつ遭遇する(仮の進行ロジック)。")]
        [SerializeField] private List<SlimeSpeciesData> enemySpecies = new();
        [Tooltip("出現する敵のレベル(このダンジョンでは一律)。仮値、要調整。")]
        [SerializeField] private int enemyLevel = 1;
        [Tooltip("次の敵と遭遇するまでの間隔(秒)。マップ移動の見た目を簡略化した仮のロジック。")]
        [SerializeField] private float encounterInterval = 3f;

        public string DungeonName => dungeonName;
        public int RequiredPartySize => requiredPartySize;
        public IReadOnlyList<SlimeSpeciesData> EnemySpecies => enemySpecies;
        public int EnemyLevel => enemyLevel;
        public float EncounterInterval => encounterInterval;
    }
}
