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
    /// 行動帯での実座標(BattleUnit.Position)もここで一元管理し、見た目側(Visuals以下)はそれを
    /// 描画するだけの反応的なレイヤーに徹する(ロジックがViewに依存しないようにするため)。
    /// </summary>
    public class DungeonRunController : MonoBehaviour
    {
        [SerializeField] private TypeAffinityData defaultAffinityData;
        [SerializeField] private BattleBalanceData defaultBalanceData;

        [Header("行動帯(座標シミュレーション)")]
        [Tooltip("プレイヤー・敵とも自由に動き回る帯の幅(ワールド単位)。")]
        [SerializeField] private float movementBandWidth = 11f;
        [Tooltip("帯の高さ(ワールド単位)。このコンポーネントのtransform.position.y(背景画像の水平中央線)から下方向へこの高さ分だけ帯を確保する。")]
        [SerializeField] private float movementBandHeight = 3f;

        [Header("敵の登場演出")]
        [Tooltip("敵が行動帯の外側のどちら側から歩いて登場するか。")]
        [SerializeField] private EnemyEntranceSide enemyEntranceSide = EnemyEntranceSide.Right;
        [Tooltip("登場開始位置を行動帯の端からどれだけ外側(画面外方向)に離すか(ワールド単位)。")]
        [SerializeField] private float enemyEntranceMargin = 5f;

        private DungeonData dungeonData;
        private TypeAffinityData affinityData;
        private BattleBalanceData balanceData;

        private readonly List<BattleUnit> playerUnits = new();
        private readonly List<BattleUnit> aliveEnemies = new();
        private readonly List<PendingMeleeHit> pendingMeleeHits = new();
        private Queue<EncounterWaveData> remainingWaves = new();
        private BattleUnit lockedTarget;
        private float encounterTimer;

        public DungeonState State { get; private set; } = DungeonState.NotStarted;
        public IReadOnlyList<BattleUnit> PlayerUnits => playerUnits;
        public IReadOnlyList<BattleUnit> AliveEnemies => aliveEnemies;

        /// <summary>近接攻撃の接近〜着弾の遅延(秒)。見た目側が突進モーションの尺を合わせるために参照する。</summary>
        public float MeleeHitDelay => balanceData != null ? balanceData.MeleeHitDelay : 0.18f;

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

        /// <summary>攻撃(接近/照準)を開始した瞬間に発火する。近接は接近モーション、遠距離は発射モーションの合図。</summary>
        public event Action<BattleUnit, BattleUnit, int> OnNormalAttackPerformed;
        public event Action<BattleUnit, BattleUnit, int> OnUltimateActivated;

        /// <summary>
        /// 近接攻撃が実際に決着した瞬間(命中/ミスが確定しダメージが反映された瞬間)に発火する。
        /// damageが0の場合はミス(距離が届かなかった)を表す。遠距離攻撃はOnNormalAttackPerformed/
        /// OnUltimateActivatedと同時に即決着するため、これらのResolvedイベントは発火しない。
        /// </summary>
        public event Action<BattleUnit, BattleUnit, int> OnNormalAttackResolved;
        public event Action<BattleUnit, BattleUnit, int> OnUltimateResolved;

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
            pendingMeleeHits.Clear();
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

            Rect bounds = GetMovementBounds();
            foreach (BattleUnit unit in playerUnits)
            {
                unit.Position = PickRandomPointInBounds(bounds);
                unit.WanderTarget = unit.Position;
                unit.WanderPauseTimer = UnityEngine.Random.Range(balanceData.WanderPauseMin, balanceData.WanderPauseMax);
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

            unit.UltimateCooldownRemaining = unit.UltimateSkill.CooldownSeconds;

            if (unit.AttackType == SlimeAttackType.Melee)
            {
                BeginMeleeAttack(unit, target, unit.UltimateSkill.Power, isUltimate: true, unit.UltimateSkill.SkillName);
            }
            else
            {
                int damage = BattleFormulas.CalculateDamage(unit.Stats.attack, target.Stats.defense,
                    unit.UltimateSkill.Power, unit.Type, target.Type, affinityData);
                target.ApplyDamage(damage);
                Log($"{unit.DisplayName}の必殺技「{unit.UltimateSkill.SkillName}」!{target.DisplayName}に{damage}ダメージ");
                OnUltimateActivated?.Invoke(unit, target, damage);
                HandleIfFainted(target);
            }

            return true;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            switch (State)
            {
                case DungeonState.WaitingForEncounter:
                    TickMovement(dt);
                    encounterTimer -= dt;
                    if (encounterTimer <= 0f)
                    {
                        SpawnNextWave();
                    }
                    break;

                case DungeonState.InBattle:
                    TickMovement(dt);
                    TickPendingMeleeHits(dt);
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

            Rect bounds = GetMovementBounds();
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

                // 敵は行動帯の外側(enemyEntranceSide側)を起点位置とし、最初の徘徊目標を帯内に
                // 設定する。これによりTickWanderが毎フレーム自然に「歩いて登場する」動きを生む。
                enemyUnit.Position = GetEnemyEntrancePosition(bounds);
                enemyUnit.WanderTarget = PickRandomPointInBounds(bounds);
                enemyUnit.WanderPauseTimer = 0f;

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
            if (attacker.AttackType == SlimeAttackType.Melee)
            {
                BeginMeleeAttack(attacker, target, balanceData.NormalAttackPower, isUltimate: false, skillName: null);
                return;
            }

            int damage = BattleFormulas.CalculateDamage(attacker.Stats.attack, target.Stats.defense,
                balanceData.NormalAttackPower, attacker.Type, target.Type, affinityData);
            target.ApplyDamage(damage);
            Log($"{attacker.DisplayName}の通常攻撃!{target.DisplayName}に{damage}ダメージ");
            OnNormalAttackPerformed?.Invoke(attacker, target, damage);
            HandleIfFainted(target);
        }

        /// <summary>
        /// 近接攻撃(通常攻撃・必殺技共通)。attacker/targetの実座標の距離がMeleeAttackRangeを
        /// 超える場合は「接近が間に合わず攻撃が届かない」ものとしてミス(ダメージ0)にする。
        /// 命中/ミスいずれの場合も、実際の決着(ダメージ反映)はMeleeHitDelay秒後まで遅延させ、
        /// 見た目側の接近モーションが完了するタイミングと同期させる。
        /// </summary>
        private void BeginMeleeAttack(BattleUnit attacker, BattleUnit target, int power, bool isUltimate, string skillName)
        {
            float distance = Vector2.Distance(attacker.Position, target.Position);
            bool willHit = distance <= balanceData.MeleeAttackRange;
            int damage = willHit
                ? BattleFormulas.CalculateDamage(attacker.Stats.attack, target.Stats.defense, power,
                    attacker.Type, target.Type, affinityData)
                : 0;

            if (isUltimate)
            {
                OnUltimateActivated?.Invoke(attacker, target, damage);
            }
            else
            {
                OnNormalAttackPerformed?.Invoke(attacker, target, damage);
            }

            pendingMeleeHits.Add(new PendingMeleeHit
            {
                Attacker = attacker,
                Target = target,
                Damage = damage,
                IsUltimate = isUltimate,
                SkillName = skillName,
                RemainingTime = balanceData.MeleeHitDelay,
            });
        }

        private void TickPendingMeleeHits(float dt)
        {
            for (int i = pendingMeleeHits.Count - 1; i >= 0; i--)
            {
                PendingMeleeHit pending = pendingMeleeHits[i];
                pending.RemainingTime -= dt;
                if (pending.RemainingTime > 0f)
                {
                    continue;
                }

                pendingMeleeHits.RemoveAt(i);
                ResolveMeleeHit(pending);
            }
        }

        private void ResolveMeleeHit(PendingMeleeHit pending)
        {
            BattleUnit attacker = pending.Attacker;
            BattleUnit target = pending.Target;

            if (target.IsFainted)
            {
                // 決着前に別の攻撃で既に気絶していた場合は何もしない(二重処理防止)。
                return;
            }

            string skillLabel = pending.IsUltimate ? $"必殺技「{pending.SkillName}」" : "通常攻撃";

            if (pending.Damage > 0)
            {
                target.ApplyDamage(pending.Damage);
                Log($"{attacker.DisplayName}の{skillLabel}が{target.DisplayName}にヒット!{pending.Damage}ダメージ");
            }
            else
            {
                Log($"{attacker.DisplayName}の{skillLabel}!しかし{target.DisplayName}との距離が離れすぎていて届かなかった。");
            }

            if (pending.IsUltimate)
            {
                OnUltimateResolved?.Invoke(attacker, target, pending.Damage);
            }
            else
            {
                OnNormalAttackResolved?.Invoke(attacker, target, pending.Damage);
            }

            if (pending.Damage > 0)
            {
                HandleIfFainted(target);
            }
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

        /// <summary>
        /// 行動帯(プレイヤー・敵とも自由に動き回れる範囲)。このコンポーネントのtransform.position.yを
        /// 上端(=背景画像の水平中央線、床と奥の境界)とし、そこから下方向へmovementBandHeight分だけ
        /// 確保する(床の部分のみで行動させるため)。
        /// </summary>
        private Rect GetMovementBounds()
        {
            float floorTopY = transform.position.y;
            return new Rect(
                transform.position.x - movementBandWidth * 0.5f,
                floorTopY - movementBandHeight,
                movementBandWidth,
                movementBandHeight);
        }

        private static Vector2 PickRandomPointInBounds(Rect bounds)
        {
            float x = UnityEngine.Random.Range(bounds.xMin, bounds.xMax);
            float y = UnityEngine.Random.Range(bounds.yMin, bounds.yMax);
            return new Vector2(x, y);
        }

        /// <summary>行動帯の右端または左端(enemyEntranceSide)から、外側にenemyEntranceMargin分離れた地点。</summary>
        private Vector2 GetEnemyEntrancePosition(Rect bounds)
        {
            float x = enemyEntranceSide == EnemyEntranceSide.Right
                ? bounds.xMax + enemyEntranceMargin
                : bounds.xMin - enemyEntranceMargin;
            float y = UnityEngine.Random.Range(bounds.yMin, bounds.yMax);
            return new Vector2(x, y);
        }

        /// <summary>
        /// 行動帯の中を、各ユニットが持つ徘徊目標に向かって歩き続けさせる。到達すると少し静止してから
        /// 次の目標を選び直す。見た目側(BattleUnitView)は同期を取らずBattleUnit.Positionを毎フレーム
        /// そのまま参照するだけでよい。
        /// </summary>
        private void TickMovement(float dt)
        {
            Rect bounds = GetMovementBounds();
            foreach (BattleUnit unit in playerUnits)
            {
                if (!unit.IsFainted)
                {
                    TickWander(unit, bounds, dt);
                }
            }

            foreach (BattleUnit unit in aliveEnemies)
            {
                if (!unit.IsFainted)
                {
                    TickWander(unit, bounds, dt);
                }
            }
        }

        private void TickWander(BattleUnit unit, Rect bounds, float dt)
        {
            Vector2 toTarget = unit.WanderTarget - unit.Position;
            float distance = toTarget.magnitude;

            if (distance <= balanceData.WanderArriveThreshold)
            {
                unit.WanderPauseTimer -= dt;
                if (unit.WanderPauseTimer <= 0f)
                {
                    unit.WanderTarget = PickRandomPointInBounds(bounds);
                    unit.WanderPauseTimer = UnityEngine.Random.Range(balanceData.WanderPauseMin, balanceData.WanderPauseMax);
                }

                return;
            }

            Vector2 step = toTarget.normalized * (balanceData.WanderSpeed * dt);
            unit.Position = step.sqrMagnitude >= toTarget.sqrMagnitude ? unit.WanderTarget : unit.Position + step;
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

        private class PendingMeleeHit
        {
            public BattleUnit Attacker;
            public BattleUnit Target;
            public int Damage;
            public bool IsUltimate;
            public string SkillName;
            public float RemainingTime;
        }
    }
}
