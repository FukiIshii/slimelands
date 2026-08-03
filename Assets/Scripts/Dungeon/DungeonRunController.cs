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

        [Header("必殺技の範囲攻撃用レイヤー(コライダーベースの当たり判定)")]
        [Tooltip("プレイヤー側ユニットのBattleUnitHurtboxに使うレイヤー名。敵の必殺技がプレイヤーだけを検出するために使う。")]
        [SerializeField] private string playerHurtboxLayerName = "BattleUnitPlayer";
        [Tooltip("敵側ユニットのBattleUnitHurtboxに使うレイヤー名。プレイヤーの必殺技が敵だけを検出するために使う。")]
        [SerializeField] private string enemyHurtboxLayerName = "BattleUnitEnemy";

        private DungeonData dungeonData;
        private TypeAffinityData affinityData;
        private BattleBalanceData balanceData;
        private int playerHurtboxLayer = -1;
        private int enemyHurtboxLayer = -1;

        private readonly List<BattleUnit> playerUnits = new();
        private readonly List<BattleUnit> aliveEnemies = new();
        private readonly List<PendingMeleeHit> pendingMeleeHits = new();
        private readonly List<ActiveSweep> activeSweeps = new();
        private Queue<EncounterWaveData> remainingWaves = new();
        private BattleUnit lockedTarget;
        private float encounterTimer;

        public DungeonState State { get; private set; } = DungeonState.NotStarted;
        public IReadOnlyList<BattleUnit> PlayerUnits => playerUnits;
        public IReadOnlyList<BattleUnit> AliveEnemies => aliveEnemies;

        /// <summary>近接攻撃の接近〜着弾の遅延(秒)。見た目側が突進モーションの尺を合わせるために参照する。</summary>
        public float MeleeHitDelay => balanceData != null ? balanceData.MeleeHitDelay : 0.18f;

        /// <summary>必殺技の範囲攻撃(コライダー)が検出対象とする、各ユニットのBattleUnitHurtbox半径。</summary>
        public float HurtboxRadius => balanceData != null ? balanceData.HurtboxRadius : 0.5f;
        /// <summary>プレイヤー側ユニットのBattleUnitHurtboxに使うレイヤー番号。見た目側(DungeonBattleView)がコライダー生成時に参照する。</summary>
        public int PlayerHurtboxLayer => EnsurePlayerHurtboxLayer();
        /// <summary>敵側ユニットのBattleUnitHurtboxに使うレイヤー番号。見た目側(DungeonBattleView)がコライダー生成時に参照する。</summary>
        public int EnemyHurtboxLayer => EnsureEnemyHurtboxLayer();

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

        /// <summary>範囲必殺技(Circle/Line)が発動し、命中した全対象へダメージが確定した瞬間に発火する。</summary>
        public event Action<BattleUnit, IReadOnlyList<BattleUnit>, Vector2> OnAreaUltimateActivated;

        /// <summary>スイープ型必殺技(左右往復)が発動した瞬間に発火する(見た目側の帯の生成に使用)。</summary>
        public event Action<BattleUnit, SlimeSkillData> OnSweepStarted;
        /// <summary>スイープの現在位置(帯の中心x座標)が更新されるたびに発火する(見た目側の帯の追従に使用)。</summary>
        public event Action<BattleUnit, float> OnSweepPositionUpdated;
        /// <summary>スイープが対象にダメージを与えた瞬間に発火する(ヒットエフェクト用)。</summary>
        public event Action<BattleUnit, BattleUnit, int> OnSweepHit;
        /// <summary>スイープの持続時間が終了し消滅した瞬間に発火する。</summary>
        public event Action<BattleUnit> OnSweepEnded;

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
            activeSweeps.Clear();
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

            SlimeSkillData skill = unit.UltimateSkill;
            unit.UltimateCooldownRemaining = skill.CooldownSeconds;

            switch (skill.AreaShape)
            {
                case UltimateAreaShape.Circle:
                    ActivateCircleUltimate(unit, target, skill);
                    break;
                case UltimateAreaShape.Line:
                    ActivateLineUltimate(unit, target, skill);
                    break;
                case UltimateAreaShape.Sweep:
                    ActivateSweepUltimate(unit, skill);
                    break;
                default:
                    ActivateSingleTargetUltimate(unit, target, skill);
                    break;
            }

            return true;
        }

        private void ActivateSingleTargetUltimate(BattleUnit unit, BattleUnit target, SlimeSkillData skill)
        {
            if (unit.AttackType == SlimeAttackType.Melee)
            {
                BeginMeleeAttack(unit, target, skill.Power, isUltimate: true, skill.SkillName);
            }
            else
            {
                int damage = BattleFormulas.CalculateDamage(unit.Stats.attack, target.Stats.defense,
                    skill.Power, unit.Type, target.Type, affinityData);
                target.ApplyDamage(damage);
                Log($"{unit.DisplayName}の必殺技「{skill.SkillName}」!{target.DisplayName}に{damage}ダメージ");
                OnUltimateActivated?.Invoke(unit, target, damage);
                HandleIfFainted(target);
            }
        }

        /// <summary>指定対象(primaryTarget)の座標を中心とした円形範囲に、範囲内の対象全員へダメージを与える(Physics2D.OverlapCircleAllで検出)。</summary>
        private void ActivateCircleUltimate(BattleUnit caster, BattleUnit primaryTarget, SlimeSkillData skill)
        {
            Vector2 origin = primaryTarget.Position;
            List<BattleUnit> hits = QueryUnitsInCircle(origin, skill.AreaRadius, caster);
            ApplyAreaDamage(caster, hits, skill, origin);
        }

        /// <summary>発動者からprimaryTarget方向へ伸びる直線状の範囲に、範囲内の対象全員へダメージを与える(Physics2D.OverlapBoxAllで検出)。</summary>
        private void ActivateLineUltimate(BattleUnit caster, BattleUnit primaryTarget, SlimeSkillData skill)
        {
            Vector2 origin = caster.Position;
            Vector2 direction = primaryTarget.Position - caster.Position;
            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Vector2 boxCenter = origin + dir * (skill.AreaLength * 0.5f);
            Vector2 boxSize = new(skill.AreaLength, skill.AreaRadius * 2f);

            List<BattleUnit> hits = QueryUnitsInBox(boxCenter, boxSize, angle, caster);
            ApplyAreaDamage(caster, hits, skill, origin);
        }

        private void ApplyAreaDamage(BattleUnit caster, List<BattleUnit> hits, SlimeSkillData skill, Vector2 origin)
        {
            if (hits.Count == 0)
            {
                Log($"{caster.DisplayName}の必殺技「{skill.SkillName}」!しかし範囲内に対象がいなかった。");
                OnAreaUltimateActivated?.Invoke(caster, hits, origin);
                return;
            }

            foreach (BattleUnit target in hits)
            {
                int damage = BattleFormulas.CalculateDamage(caster.Stats.attack, target.Stats.defense,
                    skill.Power, caster.Type, target.Type, affinityData);
                target.ApplyDamage(damage);
                Log($"{caster.DisplayName}の必殺技「{skill.SkillName}」が{target.DisplayName}にヒット!{damage}ダメージ");
            }

            OnAreaUltimateActivated?.Invoke(caster, hits, origin);

            // ダメージ適用は上のループで先に済ませ、気絶判定(aliveEnemiesの変更を伴う)は別ループで行う。
            foreach (BattleUnit target in hits)
            {
                HandleIfFainted(target);
            }
        }

        /// <summary>行動帯の左端〜右端を往復し続け、接触している間ダメージを与え続ける持続型の必殺技を開始する。</summary>
        private void ActivateSweepUltimate(BattleUnit caster, SlimeSkillData skill)
        {
            activeSweeps.Add(new ActiveSweep
            {
                Caster = caster,
                Skill = skill,
                Elapsed = 0f,
                TickTimer = 0f, // 発動直後にまず1回判定させる
            });

            Log($"{caster.DisplayName}の必殺技「{skill.SkillName}」!帯が左右に往復し始めた。");
            OnSweepStarted?.Invoke(caster, skill);
        }

        private void TickActiveSweeps(float dt)
        {
            if (activeSweeps.Count == 0)
            {
                return;
            }

            Rect bounds = GetMovementBounds();

            for (int i = activeSweeps.Count - 1; i >= 0; i--)
            {
                ActiveSweep sweep = activeSweeps[i];
                sweep.Elapsed += dt;

                if (sweep.Elapsed >= sweep.Skill.SweepDuration)
                {
                    activeSweeps.RemoveAt(i);
                    OnSweepEnded?.Invoke(sweep.Caster);
                    continue;
                }

                float oneWay = Mathf.Max(0.01f, sweep.Skill.SweepOneWayDuration);
                float x = Mathf.Lerp(bounds.xMin, bounds.xMax, Mathf.PingPong(sweep.Elapsed / oneWay, 1f));
                OnSweepPositionUpdated?.Invoke(sweep.Caster, x);

                sweep.TickTimer -= dt;
                if (sweep.TickTimer > 0f)
                {
                    continue;
                }

                sweep.TickTimer = Mathf.Max(0.01f, sweep.Skill.SweepTickInterval);

                // 帯の現在位置(x)を中心に、行動帯の高さ全体をカバーする矩形でコライダーを検出する
                // (元の実装はxのみの距離判定だったが、これと同じ「yは問わない」挙動を矩形クエリで再現する)。
                Vector2 boxCenter = new(x, bounds.center.y);
                Vector2 boxSize = new(sweep.Skill.AreaRadius * 2f, bounds.height);
                List<BattleUnit> hitThisTick = QueryUnitsInBox(boxCenter, boxSize, 0f, sweep.Caster);

                foreach (BattleUnit target in hitThisTick)
                {
                    int damage = BattleFormulas.CalculateDamage(sweep.Caster.Stats.attack, target.Stats.defense,
                        sweep.Skill.Power, sweep.Caster.Type, target.Type, affinityData);
                    target.ApplyDamage(damage);
                    Log($"{sweep.Caster.DisplayName}の必殺技「{sweep.Skill.SkillName}」が{target.DisplayName}に{damage}ダメージ!");
                    OnSweepHit?.Invoke(sweep.Caster, target, damage);
                }

                foreach (BattleUnit target in hitThisTick)
                {
                    HandleIfFainted(target);
                }
            }
        }

        /// <summary>
        /// casterの敵対勢力(プレイヤー側なら敵、敵側ならプレイヤー)が使うBattleUnitHurtboxのレイヤーマスク。
        /// Physics2DのOverlapクエリで、味方を誤検出しないよう対象を絞り込むために使う。
        /// </summary>
        private int GetOpposingHurtboxMask(BattleUnit caster)
        {
            int layer = caster.IsPlayerSide ? EnsureEnemyHurtboxLayer() : EnsurePlayerHurtboxLayer();
            return layer >= 0 ? 1 << layer : 0;
        }

        private int EnsurePlayerHurtboxLayer()
        {
            if (playerHurtboxLayer < 0)
            {
                playerHurtboxLayer = LayerMask.NameToLayer(playerHurtboxLayerName);
            }

            return playerHurtboxLayer;
        }

        private int EnsureEnemyHurtboxLayer()
        {
            if (enemyHurtboxLayer < 0)
            {
                enemyHurtboxLayer = LayerMask.NameToLayer(enemyHurtboxLayerName);
            }

            return enemyHurtboxLayer;
        }

        /// <summary>
        /// 円形範囲(Physics2D.OverlapCircleAll)で、casterの敵対勢力に属し気絶していないユニットを検出する。
        /// コライダーは「何に触れたか」の検出専用で、ダメージ計算はここでは行わない
        /// (呼び出し元がBattleFormulas.CalculateDamage等で別途計算する)。
        /// </summary>
        private List<BattleUnit> QueryUnitsInCircle(Vector2 center, float radius, BattleUnit caster)
        {
            Physics2D.SyncTransforms();
            Collider2D[] results = Physics2D.OverlapCircleAll(center, radius, GetOpposingHurtboxMask(caster));
            return ResolveHurtboxUnits(results);
        }

        /// <summary>
        /// 矩形範囲(Physics2D.OverlapBoxAll)で、casterの敵対勢力に属し気絶していないユニットを検出する。
        /// 直線状の必殺技(発動者から対象方向へ伸びる帯)に使う。
        /// </summary>
        private List<BattleUnit> QueryUnitsInBox(Vector2 center, Vector2 size, float angleDegrees, BattleUnit caster)
        {
            Physics2D.SyncTransforms();
            Collider2D[] results = Physics2D.OverlapBoxAll(center, size, angleDegrees, GetOpposingHurtboxMask(caster));
            return ResolveHurtboxUnits(results);
        }

        private static List<BattleUnit> ResolveHurtboxUnits(Collider2D[] colliders)
        {
            var hits = new List<BattleUnit>();
            foreach (Collider2D col in colliders)
            {
                BattleUnitHurtbox hurtbox = col.GetComponent<BattleUnitHurtbox>();
                if (hurtbox != null && hurtbox.Unit != null && !hurtbox.Unit.IsFainted)
                {
                    hits.Add(hurtbox.Unit);
                }
            }

            return hits;
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
                    TickActiveSweeps(dt);
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
                    BattleUnit target = GetCurrentTarget();
                    if (target == null)
                    {
                        unit.NormalAttackTimer = balanceData.NormalAttackInterval;
                    }
                    else if (IsMeleeOutOfRange(unit, target))
                    {
                        // 近接タイプが射程外: 攻撃は発動せず接近を続ける(TickMovementが追尾する)。
                        // タイマーはリセットしない=射程に入った瞬間に即座に攻撃できるようにする。
                    }
                    else
                    {
                        unit.NormalAttackTimer = balanceData.NormalAttackInterval;
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
                    BattleUnit target = PickAlivePlayerUnit();
                    if (target == null)
                    {
                        enemy.NormalAttackTimer = balanceData.NormalAttackInterval;
                        continue;
                    }

                    if (IsMeleeOutOfRange(enemy, target))
                    {
                        // 近接タイプが射程外: 攻撃は発動せず接近を続ける(TickMovementが追尾する)。
                        continue;
                    }

                    enemy.NormalAttackTimer = balanceData.NormalAttackInterval;
                    PerformNormalAttack(enemy, target);
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

        /// <summary>近接タイプの攻撃者がtargetに対して射程(MeleeAttackRange)外にいるかどうか。遠距離タイプは常にfalse。</summary>
        private bool IsMeleeOutOfRange(BattleUnit attacker, BattleUnit target)
        {
            return attacker.AttackType == SlimeAttackType.Melee
                && Vector2.Distance(attacker.Position, target.Position) > balanceData.MeleeAttackRange;
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

            BattleUnit playerAttackTarget = aliveEnemies.Count > 0 ? GetCurrentTarget() : null;
            foreach (BattleUnit unit in playerUnits)
            {
                if (!unit.IsFainted)
                {
                    TickUnitMovement(unit, playerAttackTarget, bounds, dt);
                }
            }

            BattleUnit enemyAttackTarget = PickAlivePlayerUnit();
            foreach (BattleUnit unit in aliveEnemies)
            {
                if (!unit.IsFainted)
                {
                    TickUnitMovement(unit, enemyAttackTarget, bounds, dt);
                }
            }
        }

        /// <summary>
        /// 攻撃対象(敵)がいる間、近接タイプは対象へ接近(追尾)し、遠距離タイプは対象から離れる
        /// (後退)。いずれもCombatMovementSpeed(通常の徘徊速度より速く動けるよう個別に調整可能)を
        /// 使う。近接タイプが既に射程内に入った場合や、対象がいない場合は通常の徘徊(TickWander)を行う。
        /// また、行動帯にまだ入っていないユニット(敵の登場演出中で帯の外を起点位置にしている最中)は
        /// この交戦時の移動には切り替えない。特に遠距離タイプのRetreatFromTargetは座標を帯の内側へ
        /// クランプするため、登場直後にいきなり適用すると帯の端まで一瞬で座標が飛んでしまい、
        /// 「画面外から歩いて登場する」演出が壊れてしまう。帯に入るまでは従来通りTickWander
        /// (登場時に設定した徘徊目標=帯内のランダムな点へ向かう動き)に任せる。
        /// </summary>
        private void TickUnitMovement(BattleUnit unit, BattleUnit attackTarget, Rect bounds, float dt)
        {
            if (attackTarget != null && bounds.Contains(unit.Position))
            {
                if (unit.AttackType == SlimeAttackType.Melee)
                {
                    if (IsMeleeOutOfRange(unit, attackTarget))
                    {
                        ChaseTarget(unit, attackTarget, dt);
                        return;
                    }
                }
                else
                {
                    RetreatFromTarget(unit, attackTarget, bounds, dt);
                    return;
                }
            }

            TickWander(unit, bounds, dt);
        }

        /// <summary>近接タイプが射程外の対象へまっすぐ接近する(徘徊目標を対象の位置で上書きする)。</summary>
        private void ChaseTarget(BattleUnit unit, BattleUnit target, float dt)
        {
            unit.WanderTarget = target.Position;
            unit.WanderPauseTimer = 0f;

            Vector2 toTarget = unit.WanderTarget - unit.Position;
            Vector2 step = toTarget.normalized * (balanceData.CombatMovementSpeed * dt);
            unit.Position = step.sqrMagnitude >= toTarget.sqrMagnitude ? unit.WanderTarget : unit.Position + step;
        }

        /// <summary>遠距離タイプが対象から離れる方向へ後退する(行動帯の外へは出ないようクランプする)。</summary>
        private void RetreatFromTarget(BattleUnit unit, BattleUnit target, Rect bounds, float dt)
        {
            Vector2 away = unit.Position - target.Position;
            if (away.sqrMagnitude < 0.0001f)
            {
                // 完全に重なっている場合は帯の中心から見た自分の側(なければ右方向)を仮の後退方向にする。
                away = unit.Position - bounds.center;
                if (away.sqrMagnitude < 0.0001f)
                {
                    away = Vector2.right;
                }
            }

            Vector2 step = away.normalized * (balanceData.CombatMovementSpeed * dt);
            Vector2 newPosition = unit.Position + step;
            newPosition.x = Mathf.Clamp(newPosition.x, bounds.xMin, bounds.xMax);
            newPosition.y = Mathf.Clamp(newPosition.y, bounds.yMin, bounds.yMax);
            unit.Position = newPosition;
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

        private class ActiveSweep
        {
            public BattleUnit Caster;
            public SlimeSkillData Skill;
            public float Elapsed;
            public float TickTimer;
        }
    }
}
