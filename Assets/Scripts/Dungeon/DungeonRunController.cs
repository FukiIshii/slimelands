using System;
using System.Collections.Generic;
using UnityEngine;
using Slimelands.Slime;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// ダンジョン1回分の進行(遭遇→戦闘→クリア/失敗)を自動で駆動する。
    /// 村パートとは疎結合: 受け取るのはSlimeIndividualのリストのみで、CardBoard等の
    /// 村側の仕組みには一切依存しない。
    /// プレイヤー操作は必殺技の発動(TryActivateUltimate)とターゲット指定(SetTarget)のみ。
    /// </summary>
    public class DungeonRunController : MonoBehaviour
    {
        [SerializeField] private TypeAffinityData defaultAffinityData;
        [SerializeField] private BattleBalanceData defaultBalanceData;

        private DungeonData dungeonData;
        private TypeAffinityData affinityData;
        private BattleBalanceData balanceData;

        private readonly List<BattleUnit> playerUnits = new();
        private readonly List<BattleUnit> aliveEnemies = new();
        private Queue<EncounterWaveData> remainingWaves = new();
        private BattleUnit lockedTarget;
        private float encounterTimer;

        public DungeonState State { get; private set; } = DungeonState.NotStarted;
        public IReadOnlyList<BattleUnit> PlayerUnits => playerUnits;
        public IReadOnlyList<BattleUnit> AliveEnemies => aliveEnemies;

        /// <summary>戦闘ログ1行が発生するたびに呼ばれる。</summary>
        public event Action<string> OnLog;
        public event Action OnDungeonCleared;
        public event Action OnDungeonFailed;

        /// <summary>
        /// 以下は見た目(スプライト表示・エフェクト)側がロジックに反応するための通知イベント。
        /// ダメージ計算・ターゲティング・クールタイムなど戦闘ロジック自体には影響しない、追加の通知専用。
        /// </summary>
        /// <summary>ダンジョンへの突入が成立した瞬間に、そのダンジョン定義とともに発火する(背景差し替え等に使用)。</summary>
        public event Action<DungeonData> OnDungeonStarted;
        public event Action<IReadOnlyList<BattleUnit>> OnPartySpawned;
        public event Action<IReadOnlyList<BattleUnit>> OnEnemyWaveSpawned;
        public event Action<BattleUnit, BattleUnit, int> OnNormalAttackPerformed;
        public event Action<BattleUnit, BattleUnit, int> OnUltimateActivated;
        public event Action<BattleUnit> OnUnitFainted;

        /// <summary>
        /// ダンジョンへの突入。partyはHPが最大HPの50%以上に回復している個体(CanJoinParty)のみが
        /// 実際に参加する。affinity/balanceを省略した場合はInspectorで設定したデフォルト値を使う。
        /// </summary>
        public void StartDungeon(DungeonData dungeon, IReadOnlyList<SlimeIndividual> party,
            TypeAffinityData affinity = null, BattleBalanceData balance = null)
        {
            if (dungeon == null)
            {
                Log("ダンジョンデータが指定されていません。");
                return;
            }

            dungeonData = dungeon;
            affinityData = affinity != null ? affinity : defaultAffinityData;
            balanceData = balance != null ? balance : defaultBalanceData;

            playerUnits.Clear();
            foreach (SlimeIndividual individual in party)
            {
                if (individual == null)
                {
                    continue;
                }

                if (!individual.CanJoinParty)
                {
                    Log($"{individual.Species.SpeciesName}はHPが最大の50%未満のため編成から除外されました。");
                    continue;
                }

                SlimeSkillData ultimate = null;
                foreach (SlimeSkillData skill in individual.LearnedSkills)
                {
                    if (skill != null && skill.IsUltimate)
                    {
                        ultimate = skill;
                        break;
                    }
                }

                SlimeStats stats = individual.CalculateCurrentStats();
                playerUnits.Add(new BattleUnit(individual.Species.SpeciesName, individual.Species.Type,
                    individual.Species.AttackType, individual.Species, stats, true, individual, ultimate,
                    individual.CurrentHp));
            }

            if (playerUnits.Count == 0)
            {
                Log("参加可能なスライムがいないため、ダンジョンに突入できません。");
                State = DungeonState.NotStarted;
                return;
            }

            if (playerUnits.Count < dungeon.RequiredPartySize)
            {
                Log($"必要体数({dungeon.RequiredPartySize})に満たないパーティー({playerUnits.Count}体)ですが進行します。");
            }

            remainingWaves = new Queue<EncounterWaveData>(dungeon.Waves);
            aliveEnemies.Clear();
            lockedTarget = null;
            Log($"{dungeon.DungeonName}に突入!(パーティー{playerUnits.Count}体)");
            OnDungeonStarted?.Invoke(dungeon);
            OnPartySpawned?.Invoke(playerUnits);
            ProceedToNextWave();
        }

        /// <summary>指定した敵にターゲットを固定する。固定中は自動ターゲットに切り替わらない。</summary>
        public void SetTarget(BattleUnit enemy)
        {
            if (enemy != null && !enemy.IsFainted && aliveEnemies.Contains(enemy))
            {
                lockedTarget = enemy;
            }
        }

        /// <summary>ターゲット固定を解除し、自動照準に戻す。</summary>
        public void ClearTargetOverride()
        {
            lockedTarget = null;
        }

        /// <summary>現在の攻撃対象(固定中はその敵、未固定なら生存する敵の先頭)。いなければnull。</summary>
        public BattleUnit GetCurrentTarget()
        {
            if (lockedTarget != null && !lockedTarget.IsFainted && aliveEnemies.Contains(lockedTarget))
            {
                return lockedTarget;
            }

            lockedTarget = null;
            return aliveEnemies.Count > 0 ? aliveEnemies[0] : null;
        }

        /// <summary>必殺技の発動をプレイヤーが指示する。クールタイム中や戦闘外ならfalseを返す。</summary>
        public bool TryActivateUltimate(BattleUnit unit)
        {
            if (State != DungeonState.InBattle || unit == null || !unit.CanActivateUltimate)
            {
                return false;
            }

            BattleUnit target = GetCurrentTarget();
            if (target == null)
            {
                return false;
            }

            int damage = BattleFormulas.CalculateDamage(unit.Stats.attack, target.Stats.defense,
                unit.UltimateSkill.Power, unit.Type, target.Type, affinityData);
            target.ApplyDamage(damage);
            unit.UltimateCooldownRemaining = unit.UltimateSkill.CooldownSeconds;
            Log($"{unit.DisplayName}の必殺技「{unit.UltimateSkill.SkillName}」!{target.DisplayName}に{damage}ダメージ");
            OnUltimateActivated?.Invoke(unit, target, damage);
            HandleIfFainted(target);
            return true;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            switch (State)
            {
                case DungeonState.WaitingForEncounter:
                    encounterTimer -= dt;
                    if (encounterTimer <= 0f)
                    {
                        SpawnNextWave();
                    }
                    break;

                case DungeonState.InBattle:
                    TickBattle(dt);
                    break;
            }
        }

        /// <summary>
        /// 残りウェーブの有無と、次のウェーブの出現条件に応じて進行する。
        /// WaitForTime: 待機時間を設定してWaitingForEncounterへ。
        /// ImmediateAfterPrevious: 待機なしで即座に次のウェーブを出現させる。
        /// 残りウェーブが無ければダンジョンクリアとする。
        /// </summary>
        private void ProceedToNextWave()
        {
            if (remainingWaves.Count == 0)
            {
                CompleteDungeon(cleared: true);
                return;
            }

            EncounterWaveData nextWave = remainingWaves.Peek();
            if (nextWave.SpawnCondition == WaveSpawnCondition.ImmediateAfterPrevious)
            {
                SpawnNextWave();
            }
            else
            {
                encounterTimer = Mathf.Max(0f, nextWave.WaitSeconds);
                State = DungeonState.WaitingForEncounter;
            }
        }

        private void SpawnNextWave()
        {
            if (remainingWaves.Count == 0)
            {
                CompleteDungeon(cleared: true);
                return;
            }

            EncounterWaveData wave = remainingWaves.Dequeue();
            aliveEnemies.Clear();
            lockedTarget = null;

            var spawned = new List<BattleUnit>();
            foreach (SlimeSpeciesData species in wave.EnemySpecies)
            {
                if (species == null)
                {
                    continue;
                }

                SlimeStats stats = species.CalculateBaseStatsAtLevel(dungeonData.EnemyLevel);
                var enemyUnit = new BattleUnit(species.SpeciesName, species.Type, species.AttackType, species, stats,
                    false, null, null, stats.maxHp);
                aliveEnemies.Add(enemyUnit);
                spawned.Add(enemyUnit);
                Log($"{species.SpeciesName}が現れた!");
            }

            if (spawned.Count == 0)
            {
                // enemySpeciesが空/未設定のウェーブ(データ不備)は戦闘不能になるためスキップする。
                Log("ウェーブに有効な敵が設定されていないため、スキップします。");
                ProceedToNextWave();
                return;
            }

            State = DungeonState.InBattle;
            OnEnemyWaveSpawned?.Invoke(spawned);
        }

        private void TickBattle(float dt)
        {
            foreach (BattleUnit unit in playerUnits)
            {
                if (unit.IsFainted)
                {
                    continue;
                }

                if (unit.HasUltimate)
                {
                    unit.UltimateCooldownRemaining = Mathf.Max(0f, unit.UltimateCooldownRemaining - dt);
                }

                unit.NormalAttackTimer -= dt;
                if (unit.NormalAttackTimer <= 0f)
                {
                    unit.NormalAttackTimer = balanceData.NormalAttackInterval;
                    BattleUnit target = GetCurrentTarget();
                    if (target != null)
                    {
                        PerformNormalAttack(unit, target);
                    }
                }
            }

            for (int i = aliveEnemies.Count - 1; i >= 0; i--)
            {
                BattleUnit enemy = aliveEnemies[i];
                if (enemy.IsFainted)
                {
                    continue;
                }

                enemy.NormalAttackTimer -= dt;
                if (enemy.NormalAttackTimer <= 0f)
                {
                    enemy.NormalAttackTimer = balanceData.NormalAttackInterval;
                    BattleUnit target = PickAlivePlayerUnit();
                    if (target != null)
                    {
                        PerformNormalAttack(enemy, target);
                    }
                }
            }

            if (State != DungeonState.InBattle)
            {
                // 上記の攻撃処理中にクリア/失敗へ状態遷移した場合はこれ以上進めない。
                return;
            }

            bool allPlayersFainted = true;
            foreach (BattleUnit unit in playerUnits)
            {
                if (!unit.IsFainted)
                {
                    allPlayersFainted = false;
                    break;
                }
            }

            if (allPlayersFainted)
            {
                CompleteDungeon(cleared: false);
            }
        }

        private BattleUnit PickAlivePlayerUnit()
        {
            foreach (BattleUnit unit in playerUnits)
            {
                if (!unit.IsFainted)
                {
                    return unit;
                }
            }

            return null;
        }

        private void PerformNormalAttack(BattleUnit attacker, BattleUnit target)
        {
            int damage = BattleFormulas.CalculateDamage(attacker.Stats.attack, target.Stats.defense,
                balanceData.NormalAttackPower, attacker.Type, target.Type, affinityData);
            target.ApplyDamage(damage);
            Log($"{attacker.DisplayName}の通常攻撃!{target.DisplayName}に{damage}ダメージ");
            OnNormalAttackPerformed?.Invoke(attacker, target, damage);
            HandleIfFainted(target);
        }

        private void HandleIfFainted(BattleUnit target)
        {
            if (!target.IsFainted)
            {
                return;
            }

            Log($"{target.DisplayName}は気絶した!");
            OnUnitFainted?.Invoke(target);

            if (target == lockedTarget)
            {
                lockedTarget = null;
            }

            if (!target.IsPlayerSide)
            {
                aliveEnemies.Remove(target);
                if (aliveEnemies.Count == 0 && State == DungeonState.InBattle)
                {
                    Log("敵を倒した!");
                    ProceedToNextWave();
                }
            }
        }

        private void CompleteDungeon(bool cleared)
        {
            State = cleared ? DungeonState.Cleared : DungeonState.Failed;

            foreach (BattleUnit unit in playerUnits)
            {
                unit.SourceIndividual?.ApplyDungeonResultHp(unit.CurrentHp);
            }

            if (cleared)
            {
                Log("ダンジョンクリア!");
                OnDungeonCleared?.Invoke();
            }
            else
            {
                Log("パーティー全滅…ダンジョン失敗。");
                OnDungeonFailed?.Invoke();
            }
        }

        private void Log(string message)
        {
            OnLog?.Invoke(message);
        }
    }
}
