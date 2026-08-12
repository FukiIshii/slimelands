using System;
using System.Collections.Generic;
using UnityEngine;
using Slimelands.Cards;
using Slimelands.Slime;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// ダンジョンクリア時にドロップする資源カード1種類分の定義(カード種類×個数)。
    /// </summary>
    [Serializable]
    public class DungeonDropEntry
    {
        [SerializeField] private ResourceCardData resource;
        [Tooltip("クリア時に確定でドロップする枚数。")]
        [SerializeField] private int amount = 1;

        public ResourceCardData Resource => resource;
        public int Amount => amount;
    }

    /// <summary>
    /// 1回分のウェーブ(遭遇)の定義。複数体の敵が同時に出現するウェーブも表現できる。
    /// </summary>
    [Serializable]
    public class EncounterWaveData
    {
        [Tooltip("このウェーブで同時に出現する敵の種族(複数体を指定すると同時出現になる)。")]
        [SerializeField] private List<SlimeSpeciesData> enemySpecies = new();
        [Tooltip("出現条件。WaitForTime=waitSeconds待ってから出現、" +
            "ImmediateAfterPrevious=直前のウェーブを全滅させた直後に待機なしで即座に出現。")]
        [SerializeField] private WaveSpawnCondition spawnCondition = WaveSpawnCondition.WaitForTime;
        [Tooltip("spawnConditionがWaitForTimeの場合のみ使用する待機時間(秒)。")]
        [SerializeField] private float waitSeconds = 3f;

        public IReadOnlyList<SlimeSpeciesData> EnemySpecies => enemySpecies;
        public WaveSpawnCondition SpawnCondition => spawnCondition;
        public float WaitSeconds => waitSeconds;
    }

    /// <summary>
    /// ダンジョン1種類の定義。敵キャラは新しいデータ構造を作らず、既存のSlimeSpeciesDataを
    /// そのまま流用する(プレイヤーが入手できるのと同じ種族データ)。
    /// 出現はウェーブ単位(EncounterWaveData)で管理し、ウェーブごとに複数体の同時出現・
    /// 出現条件(時間待ち/前ウェーブ全滅直後)を個別に指定できる。
    /// </summary>
    [CreateAssetMenu(fileName = "NewDungeon", menuName = "Slimelands/Dungeon/Dungeon Data", order = 61)]
    public class DungeonData : ScriptableObject
    {
        [SerializeField] private string dungeonName;
        [Tooltip("ダンジョン詳細画面に表示するフレーバーテキスト。")]
        [SerializeField, TextArea(2, 4)] private string description;
        [Tooltip("ダンジョン詳細画面に表示するアートワーク。未設定ならプレースホルダー(単色の円)を使う。")]
        [SerializeField] private Sprite icon;
        [Tooltip("このダンジョンに挑めるパーティーの体数の上限。パーティー編成画面ではこれを超えて" +
            "選択できない(参加人数はこの体数以下であればよい)。")]
        [SerializeField] private int requiredPartySize = 3;
        [Tooltip("出現する敵のレベル(このダンジョンでは一律)。ダンジョン詳細画面には「推奨レベル」として表示する。仮値、要調整。")]
        [SerializeField] private int enemyLevel = 1;
        [Tooltip("出現するウェーブのリスト。先頭から順に進行する。")]
        [SerializeField] private List<EncounterWaveData> waves = new();
        [Tooltip("ダンジョン専用画面の背景スクロールに使うスプライト。未設定ならプレースホルダー(単色)を使う。")]
        [SerializeField] private Sprite backgroundSprite;

        [Tooltip("このダンジョンを解放するために必要な親ダンジョン。複数指定すると、全ての親を" +
            "クリアして初めて解放される合流ノードになる。空(未設定)の場合は最初から解放済みとして扱う。")]
        [SerializeField] private DungeonData[] parentDungeons;

        [Tooltip("ダンジョンクリア時に確定でドロップする資源カード(種類と個数)。マナクリスタルを除く" +
            "配合・進化・制作用の素材はここで定義する想定(GDD「ダンジョン解放・進行システム」参照)。")]
        [SerializeField] private List<DungeonDropEntry> dropRewards = new();

        public string DungeonName => dungeonName;
        public string Description => description;
        public Sprite Icon => icon;
        public int RequiredPartySize => requiredPartySize;
        public int EnemyLevel => enemyLevel;
        public IReadOnlyList<EncounterWaveData> Waves => waves;
        public Sprite BackgroundSprite => backgroundSprite;
        public IReadOnlyList<DungeonData> ParentDungeons => parentDungeons;
        public IReadOnlyList<DungeonDropEntry> DropRewards => dropRewards;
    }
}
