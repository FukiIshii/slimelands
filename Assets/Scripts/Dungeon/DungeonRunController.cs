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

        [Header("敵不在時のプレイヤー待機(仮値、要調整)")]
        [Tooltip("攻撃対象となる敵が1体もいない間(遭遇待ち等)、プレイヤー側スライムが徘徊目標を選ぶ" +
                 "範囲を行動帯の中央付近に絞る幅(ワールド単位)。行動帯全体の幅より大きい値を指定した" +
                 "場合は行動帯全体を使う(制限なし)。敵側の徘徊やプレイヤーの交戦中の移動には影響しない。")]
        [SerializeField] private float playerIdleHoldWidth = 3f;

        [Header("交戦中の近接ユニットの接近(仮値、要調整)")]
        [Tooltip("攻撃対象がいる間、近接タイプがこの距離(ワールド単位)まで近づいたら接近をやめて" +
                 "その場に留まる。攻撃射程(MeleeAttackRange)より小さい値にすることで、単に攻撃が" +
                 "命中する距離まで近づいて止まるのではなく、攻撃の前後を問わず対象のすぐそばまで" +
                 "近づき続けるようにする。遠距離タイプ(RangedRetreatDistance)には影響しない。")]
        [SerializeField] private float meleeContactDistance = 1.2f;

        [Header("交戦中の遠距離ユニットの後退(仮値、要調整)")]
        [Tooltip("攻撃対象がいる間、遠距離タイプがこの距離(ワールド単位)まで離れたら後退をやめて" +
                 "その場に留まる。近接タイプのMeleeContactDistanceと対になる値。これが無いと、" +
                 "既に十分離れていても対象の位置がわずかに動くたびに全速力で反対方向へ移動し直そうと" +
                 "してしまい、対象(特に接近をやめて静止している近接タイプ)のわずかな位置補正だけで" +
                 "後退方向が毎フレーム逆転し、高速に左右へ振り向き続ける不具合になっていた。")]
        [SerializeField] private float rangedRetreatDistance = 4f;
        [Tooltip("遠距離タイプが後退する向きを再計算する間隔(秒)。対象の座標を毎フレーム生で参照して" +
                 "向きを計算し直すと、対象(特に接近中の近接タイプ)の位置がわずかに動く/揺れるだけで" +
                 "全速力で反対方向へ切り返してしまい、高速に左右へ振り向き続ける不具合になる。この間隔の" +
                 "間は同じ向きを保持することで、瞬間的な位置のブレに過敏に反応しないようにする。")]
        [SerializeField] private float retreatDirectionRefreshInterval = 0.25f;
        [Tooltip("後退方向へ進もうとしても行動帯の端に阻まれてほとんど位置が変わらない(壁際まで" +
                 "追い詰められて、これ以上対象から離れられない)状態を検知するための閾値。1フレームで" +
                 "本来進めるはずの距離に対する、行動帯の端でクランプされた後に実際に進めた距離の比率が" +
                 "この値を下回ったら「追い詰められている」とみなし、その場に固まらせず" +
                 "OrbitAroundTargetで対象を中心に周回させる(壁に張り付いたまま停止するのは" +
                 "不自然、GDD「常に動き続けている」方針とも矛盾するため)。周回中に角度が回れば、" +
                 "壁際から外れた瞬間に自然と後退を再開できる。")]
        [SerializeField] private float retreatStuckMovementRatio = 0.15f;

        [Header("交戦中の待機時の周回移動(仮値、要調整)")]
        [Tooltip("近接タイプが接触距離(MeleeContactDistance)まで、遠距離タイプが後退距離" +
                 "(RangedRetreatDistance)まで到達した後、その場で完全に静止させず、対象を中心とした" +
                 "円周上を低速(WanderSpeedを流用)で周回させ続けるための角速度(度/秒)。GDD「常に動き" +
                 "続けている(バウンド/転がる)キャラクターのため、待機モーションは用意しない」方針上、" +
                 "実座標が完全に止まると見た目も静止してしまうため(特に遠距離タイプは実移動量に応じて" +
                 "転がるアニメーションのため顕著)、接近/後退が完了した後も低速で動き続けさせる。" +
                 "0にすると従来通り完全静止に戻る。")]
        [SerializeField] private float combatIdleOrbitAngularSpeed = 50f;

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
        private readonly List<PendingMeteor> pendingMeteors = new();
        private Queue<EncounterWaveData> remainingWaves = new();
        private BattleUnit lockedTarget;
        private float encounterTimer;

        public DungeonState State { get; private set; } = DungeonState.NotStarted;
        public IReadOnlyList<BattleUnit> PlayerUnits => playerUnits;
        public IReadOnlyList<BattleUnit> AliveEnemies => aliveEnemies;

        /// <summary>直近にStartDungeonへ渡されたダンジョン定義。クリア/失敗後もStartDungeonが
        /// 再度呼ばれるまで保持されるため、OnDungeonCleared購読側(進行度管理等)が
        /// 「どのダンジョンがクリアされたか」を参照するのに使える。</summary>
        public DungeonData CurrentDungeon => dungeonData;

        /// <summary>近接攻撃の接近〜着弾の遅延(秒)。見た目側が突進モーションの尺を合わせるために参照する。</summary>
        public float MeleeHitDelay => balanceData != null ? balanceData.MeleeHitDelay : 0.18f;

        /// <summary>必殺技の範囲攻撃(コライダー)が検出対象とする、各ユニットのBattleUnitHurtbox半径。</summary>
        public float HurtboxRadius => balanceData != null ? balanceData.HurtboxRadius : 0.5f;
        /// <summary>ダンジョン戦闘中に表示するスライムのスプライトサイズの倍率。見た目側(BattleUnitView)がBind時に参照する。</summary>
        public float UnitVisualScale => balanceData != null ? balanceData.UnitVisualScale : 1f;
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

        /// <summary>
        /// 通常攻撃・単体対象必殺技・範囲必殺技(Circle/Line)・スイープのいずれかで、対象に実際に
        /// ダメージが反映された瞬間(1ヒットごと)に発火する(attacker, target, damage)。ダメージ発生源を
        /// 問わず一箇所で拾える通知イベントで、ダメージ数字ポップアップ等の演出に使う想定。
        /// ミス(ダメージ0)の場合は発火しない。
        /// </summary>
        public event Action<BattleUnit, BattleUnit, int> OnDamageDealt;

        /// <summary>スイープ型必殺技(左右往復)が発動した瞬間に発火する(見た目側の帯の生成に使用)。</summary>
        public event Action<BattleUnit, SlimeSkillData> OnSweepStarted;
        /// <summary>スイープの現在位置(帯の中心x座標)が更新されるたびに発火する(見た目側の帯の追従に使用)。</summary>
        public event Action<BattleUnit, float> OnSweepPositionUpdated;
        /// <summary>スイープが対象にダメージを与えた瞬間に発火する(ヒットエフェクト用)。</summary>
        public event Action<BattleUnit, BattleUnit, int> OnSweepHit;
        /// <summary>スイープの持続時間が終了し消滅した瞬間に発火する。</summary>
        public event Action<BattleUnit> OnSweepEnded;

        /// <summary>
        /// メテオ型必殺技(高く飛び上がり対象付近へ落下する)が発動した瞬間に発火する
        /// (caster, skill, landingPosition)。landingPositionは発動時点で確定する着地予定地点で、
        /// 見た目側はこれを目標地点として予備動作→上空移動→落下の一連のモーションを再生する
        /// (Sweepと同様、実際のダメージ判定・着地座標の確定は着地時にApplyAreaDamage/
        /// QueryUnitsInCircleへ委譲するOnAreaUltimateActivated/OnDamageDealtで拾う)。
        /// </summary>
        public event Action<BattleUnit, SlimeSkillData, Vector2> OnMeteorStarted;

        public event Action<BattleUnit> OnUnitFainted;

        /// <summary>
        /// デバッグ表示専用: 必殺技の当たり判定(Circle/Line/Sweep/Meteorいずれか)のコライダー
        /// クエリ(Physics2D.OverlapCircleAll/OverlapBoxAll)が実際に実行された瞬間に発火する。
        /// 戦闘ロジック(ダメージ計算・ターゲティング)には一切影響しない、Scene View上での
        /// Gizmos可視化用の通知専用イベント(実際のGame Viewには表示されない)。
        /// </summary>
        public event Action<UltimateHitboxDebugInfo> OnUltimateHitboxChecked;

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
            pendingMeteors.Clear();
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

                SlimeStats stats = individual.CalculateCurrentStats();
                playerUnits.Add(new BattleUnit(individual.Species.SpeciesName, individual.Species.Type,
                    individual.Species.AttackType, individual.Species, stats, true, individual, individual.AssignedUltimate,
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
                unit.WanderTarget = PickWanderTarget(unit.Position, bounds);
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

        /// <summary>
        /// selfの現在の攻撃対象。固定中(lockedTarget、パーティー全体で共有)はそれを優先する。
        /// 未固定ならself自身の索敵結果(BattleUnit.AutoTarget)を使う。索敵は「selfの位置を
        /// 中心に索敵半径内の生存する敵対ユニットからランダムに1体選ぶ」方式(ResolveAutoTarget/
        /// PickRandomNearbyOpponent参照)で、対象がいなければnull。
        /// </summary>
        public BattleUnit GetCurrentTarget(BattleUnit self)
        {
            BattleUnit target;
            if (lockedTarget != null && !lockedTarget.IsFainted && aliveEnemies.Contains(lockedTarget))
            {
                target = lockedTarget;
            }
            else
            {
                lockedTarget = null;
                target = ResolveAutoTarget(self);
            }

            EnsureApproachAngle(self, target);
            return target;
        }

        /// <summary>必殺技の発動をプレイヤーが指示する。クールタイム中や戦闘外ならfalseを返す。</summary>
        public bool TryActivateUltimate(BattleUnit unit)
        {
            if (State != DungeonState.InBattle || unit == null || !unit.CanActivateUltimate)
            {
                return false;
            }

            BattleUnit target = GetCurrentTarget(unit);
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
                case UltimateAreaShape.Meteor:
                    ActivateMeteorUltimate(unit, target, skill);
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
                OnDamageDealt?.Invoke(unit, target, damage);
                HandleIfFainted(target);
            }
        }

        /// <summary>指定対象(primaryTarget)の座標を中心とした円形範囲に、範囲内の対象全員へダメージを与える(Physics2D.OverlapCircleAllで検出)。</summary>
        private void ActivateCircleUltimate(BattleUnit caster, BattleUnit primaryTarget, SlimeSkillData skill)
        {
            Vector2 origin = primaryTarget.Position;
            List<BattleUnit> hits = QueryUnitsInCircle(origin, skill.AreaRadius, caster);
            OnUltimateHitboxChecked?.Invoke(new UltimateHitboxDebugInfo(origin, skill.AreaRadius));
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
            OnUltimateHitboxChecked?.Invoke(new UltimateHitboxDebugInfo(boxCenter, boxSize, angle));
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
                OnDamageDealt?.Invoke(caster, target, damage);
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

        /// <summary>
        /// メテオ型必殺技を開始する。着地予定地点は発動した瞬間に一度だけ確定する(以後、対象が
        /// 移動しても着地地点は追従しない)。飛び上がっている間は対象の実座標を追いかけ続ける
        /// 実装も検討したが、見た目側(BattleUnitView)は疑似フレーム送りでの検証・独立した
        /// タイマー駆動というこのプロジェクトの制約上、ロジック側の着地座標と見た目側の落下先を
        /// 毎フレーム同期させる仕組みが必要になり複雑化するため、発動時点で確定させるシンプルな
        /// 実装を採用した(依頼文に明記されていない解釈上の判断)。着地地点は対象の座標そのもの
        /// ではなく、対象を中心・半径meleeContactDistanceの円周上の点(EnsureApproachAngleで
        /// ユニットごとに割り当てられる接近角度をそのまま流用)にすることで、対象の真上に
        /// めり込まず、かつ着地後にIsInSkillMotionを解除した瞬間、通常の交戦距離
        /// (ApproachTargetWithStandoffのstandoff)にちょうど収まった状態で復帰できるようにしている。
        /// </summary>
        private void ActivateMeteorUltimate(BattleUnit caster, BattleUnit target, SlimeSkillData skill)
        {
            caster.IsInSkillMotion = true;

            EnsureApproachAngle(caster, target);
            Vector2 offset = new(Mathf.Cos(caster.ApproachAngleRadians), Mathf.Sin(caster.ApproachAngleRadians));
            Vector2 landingPosition = target.Position + offset * meleeContactDistance;

            pendingMeteors.Add(new PendingMeteor
            {
                Caster = caster,
                Skill = skill,
                Phase = MeteorPhase.Windup,
                PhaseElapsed = 0f,
                LandingPosition = landingPosition,
            });

            Log($"{caster.DisplayName}の必殺技「{skill.SkillName}」!高く飛び上がった。");
            OnMeteorStarted?.Invoke(caster, skill, landingPosition);
        }

        private void TickPendingMeteors(float dt)
        {
            for (int i = pendingMeteors.Count - 1; i >= 0; i--)
            {
                PendingMeteor pending = pendingMeteors[i];
                pending.PhaseElapsed += dt;

                switch (pending.Phase)
                {
                    case MeteorPhase.Windup:
                        if (pending.PhaseElapsed >= pending.Skill.MeteorWindupDuration)
                        {
                            // 離地した瞬間から着地するまでの間、無敵として扱う(上空にいる間は
                            // 通常攻撃・必殺技いずれの被弾判定にも掛からない、ResolveHurtboxUnits参照)。
                            pending.Caster.IsInvulnerable = true;
                            pending.Phase = MeteorPhase.Rising;
                            pending.PhaseElapsed = 0f;
                        }
                        break;
                    case MeteorPhase.Rising:
                        if (pending.PhaseElapsed >= pending.Skill.MeteorRiseDuration)
                        {
                            pending.Phase = MeteorPhase.Hovering;
                            pending.PhaseElapsed = 0f;
                        }
                        break;
                    case MeteorPhase.Hovering:
                        if (pending.PhaseElapsed >= pending.Skill.MeteorHoverDuration)
                        {
                            pending.Phase = MeteorPhase.Falling;
                            pending.PhaseElapsed = 0f;
                        }
                        break;
                    case MeteorPhase.Falling:
                        if (pending.PhaseElapsed >= pending.Skill.MeteorFallDuration)
                        {
                            pendingMeteors.RemoveAt(i);
                            ResolveMeteorLanding(pending);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// 着地の瞬間の処理。実座標を着地地点へ移し、無敵・スキル演出中フラグを解除して通常の
        /// 交戦(接近・後退・通常攻撃)に復帰させた上で、着地地点を中心とした楕円形範囲ダメージを
        /// ApplyAreaDamage/QueryUnitsInEllipseで適用する(円形ではなく楕円形での判定、ユーザー要望)。
        /// </summary>
        private void ResolveMeteorLanding(PendingMeteor pending)
        {
            BattleUnit caster = pending.Caster;
            caster.Position = pending.LandingPosition;
            caster.IsInSkillMotion = false;
            caster.IsInvulnerable = false;

            float radiusX = pending.Skill.AreaRadius;
            float radiusY = pending.Skill.MeteorAreaRadiusY;
            List<BattleUnit> hits = QueryUnitsInEllipse(pending.LandingPosition, radiusX, radiusY, caster);
            OnUltimateHitboxChecked?.Invoke(UltimateHitboxDebugInfo.Ellipse(pending.LandingPosition, radiusX, radiusY));
            ApplyAreaDamage(caster, hits, pending.Skill, pending.LandingPosition);
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
                OnUltimateHitboxChecked?.Invoke(new UltimateHitboxDebugInfo(boxCenter, boxSize, 0f));

                foreach (BattleUnit target in hitThisTick)
                {
                    int damage = BattleFormulas.CalculateDamage(sweep.Caster.Stats.attack, target.Stats.defense,
                        sweep.Skill.Power, sweep.Caster.Type, target.Type, affinityData);
                    target.ApplyDamage(damage);
                    Log($"{sweep.Caster.DisplayName}の必殺技「{sweep.Skill.SkillName}」が{target.DisplayName}に{damage}ダメージ!");
                    OnSweepHit?.Invoke(sweep.Caster, target, damage);
                    OnDamageDealt?.Invoke(sweep.Caster, target, damage);
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

        /// <summary>
        /// 楕円範囲で、casterの敵対勢力に属し気絶していないユニットを検出する(Meteor専用)。
        /// Physics2Dに楕円クエリは存在しないため、まずOverlapCircleAllで候補を広めに拾い
        /// (半径は楕円の長い方の軸+各ユニットのHurtbox半径)、各候補のBattleUnitHurtboxの座標を
        /// 楕円の方程式((x/rx)^2+(y/ry)^2<=1)で絞り込む。Circle/Boxクエリはコライダー同士の
        /// 接触判定(コライダーの大きさ込み)で命中を検出するのに対し、この絞り込みはHurtbox中心点
        /// のみで判定するため、境界付近の巻き込み方に厳密な一致はないが、rx/ryにHurtbox半径を
        /// 加算することで同程度の判定の緩さ(コライダーが少しでも重なれば命中)を近似している。
        /// </summary>
        private List<BattleUnit> QueryUnitsInEllipse(Vector2 center, float radiusX, float radiusY, BattleUnit caster)
        {
            Physics2D.SyncTransforms();
            float hurtboxRadius = HurtboxRadius * UnitVisualScale;
            float effectiveRadiusX = Mathf.Max(0.01f, radiusX + hurtboxRadius);
            float effectiveRadiusY = Mathf.Max(0.01f, radiusY + hurtboxRadius);
            float boundingRadius = Mathf.Max(effectiveRadiusX, effectiveRadiusY);

            Collider2D[] candidates = Physics2D.OverlapCircleAll(center, boundingRadius, GetOpposingHurtboxMask(caster));
            var filtered = new List<Collider2D>();
            foreach (Collider2D candidate in candidates)
            {
                Vector2 offset = (Vector2)candidate.transform.position - center;
                float nx = offset.x / effectiveRadiusX;
                float ny = offset.y / effectiveRadiusY;
                if (nx * nx + ny * ny <= 1f)
                {
                    filtered.Add(candidate);
                }
            }

            return ResolveHurtboxUnits(filtered.ToArray());
        }

        /// <summary>
        /// selfの自動照準対象を返す。既に選んでいる対象(BattleUnit.AutoTarget)が生存していれば
        /// そのまま使い続け(TickMovement等、毎フレーム参照される箇所でフレームごとに違う相手へ
        /// ふらつかないようにするため)、対象がいない/気絶した時だけPickRandomNearbyOpponentで
        /// 選び直す。対象がメテオ等で無敵(IsInvulnerable、着地するまで攻撃が一切通らない)になった
        /// 場合も選び直す対象に含める。手動指定(lockedTarget)と違い自動照準はプレイヤーの意図が
        /// 介在しないため、届かない相手を律儀に待ち続けるより他の相手に切り替えた方が自然なため。
        /// </summary>
        private BattleUnit ResolveAutoTarget(BattleUnit self)
        {
            if (self.AutoTarget != null && !self.AutoTarget.IsFainted && !self.AutoTarget.IsInvulnerable)
            {
                return self.AutoTarget;
            }

            self.AutoTarget = PickRandomNearbyOpponent(self);
            return self.AutoTarget;
        }

        /// <summary>
        /// selfの位置を中心に索敵半径(BattleBalanceData.TargetSearchRadius)内の敵対勢力の
        /// 生存ユニットを検出し(QueryUnitsInCircleを流用、既存の射程判定・範囲必殺技と同じ
        /// Physics2D.OverlapCircleAllベースの当たり判定)、その中からランダムに1体選ぶ。
        /// 範囲内に誰もいなければnull。
        /// </summary>
        private BattleUnit PickRandomNearbyOpponent(BattleUnit self)
        {
            List<BattleUnit> candidates = QueryUnitsInCircle(self.Position, balanceData.TargetSearchRadius, self);
            return candidates.Count > 0 ? candidates[UnityEngine.Random.Range(0, candidates.Count)] : null;
        }

        /// <summary>
        /// IsInvulnerable(メテオ等、演出上「その場にいない」ことにしたいユニット)をここで一括して
        /// 除外することで、索敵(PickRandomNearbyOpponent)・射程判定(IsTargetInAttackRange)・
        /// 範囲必殺技(ApplyAreaDamage経由のCircle/Line/Meteor)のいずれの当たり判定からも
        /// 一貫して除外される。
        /// </summary>
        private static List<BattleUnit> ResolveHurtboxUnits(Collider2D[] colliders)
        {
            var hits = new List<BattleUnit>();
            foreach (Collider2D col in colliders)
            {
                BattleUnitHurtbox hurtbox = col.GetComponent<BattleUnitHurtbox>();
                if (hurtbox != null && hurtbox.Unit != null && !hurtbox.Unit.IsFainted && !hurtbox.Unit.IsInvulnerable)
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
                    TickUltimateCooldowns(dt);
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
                    TickPendingMeteors(dt);
                    TickUltimateCooldowns(dt);
                    TickBattle(dt);
                    break;

                case DungeonState.Cleared:
                    // クリア後の結果表示中も、村へ戻るまでプレイヤー側スライムが画面に残り続けるため、
                    // 停止した見た目にならないよう徘徊移動だけは継続させる(敵は既にいないので
                    // aliveEnemiesは空、TickMovement内の敵側ループは何もしない)。
                    TickMovement(dt);
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

        /// <summary>
        /// プレイヤー側の必殺技クールタイムを進行させる。次ウェーブ待機中(WaitingForEncounter)も
        /// 呼ばれるため、TickBattle(交戦中のみ実行)から切り出している。
        /// </summary>
        private void TickUltimateCooldowns(float dt)
        {
            foreach (BattleUnit unit in playerUnits)
            {
                if (unit.IsFainted || !unit.HasUltimate)
                {
                    continue;
                }

                unit.UltimateCooldownRemaining = Mathf.Max(0f, unit.UltimateCooldownRemaining - dt);
            }
        }

        private void TickBattle(float dt)
        {
            foreach (BattleUnit unit in playerUnits)
            {
                if (unit.IsFainted)
                {
                    continue;
                }

                if (unit.IsInSkillMotion)
                {
                    // メテオ等の必殺技演出中(予備動作〜着地)は通常攻撃を行わない
                    // (クールタイム自体はTickUltimateCooldownsで演出中も進行する)。
                    continue;
                }

                unit.NormalAttackTimer -= dt;
                if (unit.NormalAttackTimer <= 0f)
                {
                    BattleUnit target = GetCurrentTarget(unit);
                    if (target == null)
                    {
                        unit.NormalAttackTimer = balanceData.NormalAttackInterval;
                    }
                    else if (IsAttackOutOfRange(unit, target))
                    {
                        // 射程外(近接=接近中、遠距離=距離を離されている等): 攻撃は発動せず様子を見る。
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
                if (enemy.IsFainted || enemy.IsInSkillMotion)
                {
                    continue;
                }

                enemy.NormalAttackTimer -= dt;
                if (enemy.NormalAttackTimer <= 0f)
                {
                    BattleUnit target = PickAlivePlayerUnit(enemy);
                    if (target == null)
                    {
                        enemy.NormalAttackTimer = balanceData.NormalAttackInterval;
                        continue;
                    }

                    if (IsAttackOutOfRange(enemy, target))
                    {
                        // 射程外(近接=接近中、遠距離=距離を離されている等): 攻撃は発動せず様子を見る。
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

        /// <summary>
        /// selfの現在の攻撃対象(プレイヤー側ユニット)。敵側には手動指定の概念がないため、
        /// 常にself自身の索敵結果(ResolveAutoTarget)を使う。
        /// </summary>
        private BattleUnit PickAlivePlayerUnit(BattleUnit self)
        {
            BattleUnit target = ResolveAutoTarget(self);
            EnsureApproachAngle(self, target);
            return target;
        }

        /// <summary>
        /// selfがtargetへ接近する角度(ApproachAngleRadians)を、targetが確定するたび
        /// (対象が変わった時だけ)ランダムに割り当てて保持する。GetCurrentTarget/
        /// PickAlivePlayerUnit(手動指定・自動照準どちらの経路も)がターゲットを確定する
        /// たびにここを通るため、同じ対象を複数ユニットが狙っても接近する角度が独立して
        /// ばらける(ApproachTargetWithStandoff/GetApproachPoint参照)。
        /// </summary>
        private static void EnsureApproachAngle(BattleUnit self, BattleUnit target)
        {
            if (target == null || self.ApproachAngleTarget == target)
            {
                return;
            }

            self.ApproachAngleTarget = target;
            self.ApproachAngleRadians = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        }

        /// <summary>attackerがtargetに対して射程外にいるかどうか(近接・遠距離共通、コライダーベース判定)。</summary>
        private bool IsAttackOutOfRange(BattleUnit attacker, BattleUnit target)
        {
            return !IsTargetInAttackRange(attacker, target);
        }

        /// <summary>attackerのタイプに応じた攻撃射程(近接=MeleeAttackRange、遠距離=RangedAttackRange)。</summary>
        private float GetAttackRange(BattleUnit attacker)
        {
            return attacker.AttackType == SlimeAttackType.Melee
                ? balanceData.MeleeAttackRange
                : balanceData.RangedAttackRange;
        }

        /// <summary>
        /// 通常攻撃・必殺技共通の射程判定(近接・遠距離とも)。attackerの実座標を中心とした半径
        /// (GetAttackRange)の範囲をPhysics2D.OverlapCircleAllで検出し、その中にtargetのBattleUnitHurtboxが
        /// 含まれるかで判定する(必殺技の範囲攻撃と同じコライダーベースの当たり判定に統一するため、
        /// 数学的な距離比較は行わない)。
        /// </summary>
        private bool IsTargetInAttackRange(BattleUnit attacker, BattleUnit target)
        {
            Physics2D.SyncTransforms();
            Collider2D[] results = Physics2D.OverlapCircleAll(attacker.Position, GetAttackRange(attacker),
                GetOpposingHurtboxMask(attacker));
            foreach (Collider2D col in results)
            {
                BattleUnitHurtbox hurtbox = col.GetComponent<BattleUnitHurtbox>();
                // IsInvulnerable(メテオの上空滞在中など)は既にAutoTargetの選び直し対象になっている
                // (ResolveAutoTarget参照)が、手動指定(lockedTarget)経由で無敵な相手を狙っている
                // 場合や、選択後に無敵になった場合でも通常攻撃が通らないよう、ここでも判定する。
                if (hurtbox != null && hurtbox.Unit == target && !target.IsInvulnerable)
                {
                    return true;
                }
            }

            return false;
        }

        private void PerformNormalAttack(BattleUnit attacker, BattleUnit target)
        {
            if (attacker.AttackType == SlimeAttackType.Melee)
            {
                BeginMeleeAttack(attacker, target, balanceData.NormalAttackPower, isUltimate: false, skillName: null);
                return;
            }

            // 遠距離タイプ: 直前のTickBattleでのIsAttackOutOfRangeによりコライダーベースの射程判定
            // (IsTargetInAttackRange)を通過済みのため、ここでは改めて判定せず確定した命中として扱う。
            // 近接と違い接近モーションによる時間差がないため、判定と着弾を同一フレームで完結させる。
            int damage = BattleFormulas.CalculateDamage(attacker.Stats.attack, target.Stats.defense,
                balanceData.NormalAttackPower, attacker.Type, target.Type, affinityData);
            target.ApplyDamage(damage);
            Log($"{attacker.DisplayName}の通常攻撃!{target.DisplayName}に{damage}ダメージ");
            OnNormalAttackPerformed?.Invoke(attacker, target, damage);
            OnDamageDealt?.Invoke(attacker, target, damage);
            HandleIfFainted(target);
        }

        /// <summary>
        /// 近接攻撃(通常攻撃・必殺技共通)。attackerを中心としたBattleUnitHurtboxのコライダー判定
        /// (IsTargetInAttackRange)でtargetに実際に届くかを判定し、届かない場合は「接近が間に合わず
        /// 攻撃が届かない」ものとしてミス(ダメージ0)にする。命中/ミスいずれの場合も、実際の決着
        /// (ダメージ反映)はMeleeHitDelay秒後まで遅延させ、見た目側の接近モーションが完了するタイミング
        /// と同期させる。
        /// </summary>
        private void BeginMeleeAttack(BattleUnit attacker, BattleUnit target, int power, bool isUltimate, string skillName)
        {
            bool willHit = IsTargetInAttackRange(attacker, target);
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

            // 発動(BeginMeleeAttack)時点では射程内でヒット確定していても、決着(MeleeHitDelay後)までの
            // 間に対象がメテオ等で無敵(IsInvulnerable)になった場合はヒットを取り消す。無敵中は
            // 一切被弾しないという仕様を、着弾が遅延する近接攻撃でも一貫させるための対策
            // (実測で、予備動作中に発生させた攻撃が、その後無敵になった直後の上空へ着弾して
            // しまう抜け道を確認して特定した)。
            int damage = target.IsInvulnerable ? 0 : pending.Damage;
            string skillLabel = pending.IsUltimate ? $"必殺技「{pending.SkillName}」" : "通常攻撃";

            if (damage > 0)
            {
                target.ApplyDamage(damage);
                Log($"{attacker.DisplayName}の{skillLabel}が{target.DisplayName}にヒット!{damage}ダメージ");
                OnDamageDealt?.Invoke(attacker, target, damage);
            }
            else if (target.IsInvulnerable)
            {
                Log($"{attacker.DisplayName}の{skillLabel}!しかし{target.DisplayName}は無敵状態で届かなかった。");
            }
            else
            {
                Log($"{attacker.DisplayName}の{skillLabel}!しかし{target.DisplayName}との距離が離れすぎていて届かなかった。");
            }

            if (pending.IsUltimate)
            {
                OnUltimateResolved?.Invoke(attacker, target, damage);
            }
            else
            {
                OnNormalAttackResolved?.Invoke(attacker, target, damage);
            }

            if (damage > 0)
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

        /// <summary>
        /// Rect.Containsは上限(xMax/yMax)を含まない排他判定のため、クランプ直後で境界ちょうどに
        /// いるユニットを「帯の外」と誤判定してしまう。境界ちょうども内側として扱う版。
        /// </summary>
        private static bool IsWithinMovementBounds(Rect bounds, Vector2 position)
        {
            return position.x >= bounds.xMin && position.x <= bounds.xMax
                && position.y >= bounds.yMin && position.y <= bounds.yMax;
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
        /// 行動帯の中を、各ユニットが持つ徘徊目標に向かって歩き続けさせる。到達すると静止せず
        /// 即座に次の目標を選び直す。見た目側(BattleUnitView)は同期を取らずBattleUnit.Positionを
        /// 毎フレームそのまま参照するだけでよい。
        /// </summary>
        private void TickMovement(float dt)
        {
            Rect bounds = GetMovementBounds();

            bool enemiesPresent = aliveEnemies.Count > 0;
            foreach (BattleUnit unit in playerUnits)
            {
                if (!unit.IsFainted && !unit.IsInSkillMotion)
                {
                    BattleUnit target = enemiesPresent ? GetCurrentTarget(unit) : null;
                    TickUnitMovement(unit, target, bounds, dt);
                }
            }

            foreach (BattleUnit unit in aliveEnemies)
            {
                if (!unit.IsFainted && !unit.IsInSkillMotion)
                {
                    TickUnitMovement(unit, PickAlivePlayerUnit(unit), bounds, dt);
                }
            }
        }

        /// <summary>
        /// 攻撃対象(敵)がいる間、近接タイプは対象へ接近(追尾)し、遠距離タイプは対象から離れる
        /// (後退)。いずれもCombatMovementSpeed(通常の徘徊速度より速く動けるよう個別に調整可能)を
        /// 使う。近接タイプは、攻撃射程(MeleeAttackRange)に入った後も攻撃前後を問わず
        /// meleeContactDistanceまで近づき続け、そこに達して初めて接近をやめる(対象がいる間は
        /// 常に近づこうとする挙動にするため、通常の徘徊(TickWander)には切り替えない)。対象がいない
        /// 場合のみ通常の徘徊を行う。
        /// また、行動帯にまだ入っていないユニット(敵の登場演出中で帯の外を起点位置にしている最中)は
        /// この交戦時の移動には切り替えない。特に遠距離タイプのRetreatFromTargetは座標を帯の内側へ
        /// クランプするため、登場直後にいきなり適用すると帯の端まで一瞬で座標が飛んでしまい、
        /// 「画面外から歩いて登場する」演出が壊れてしまう。帯に入るまでは従来通りTickWander
        /// (登場時に設定した徘徊目標=帯内のランダムな点へ向かう動き)に任せる。
        /// 攻撃対象(敵)が1体もいない間、プレイヤー側は行動帯全体ではなく中央付近(GetPlayerIdleHoldBounds)
        /// だけを徘徊範囲にし、なるべく中央周辺に留まり続けるようにする(敵側の徘徊には影響しない)。
        /// </summary>
        private void TickUnitMovement(BattleUnit unit, BattleUnit attackTarget, Rect bounds, float dt)
        {
            // Rect.Containsは上限(xMax/yMax)を含まない(x < xMaxで判定する)排他的な範囲判定のため、
            // RetreatFromTargetが座標を帯の端(xMax/yMax含む)へクランプした直後のフレームで
            // 「帯に入っていない」と誤判定されてしまう。その結果このメソッドの分岐がTickWanderへ
            // 落ち、対象から離れた古い徘徊目標へ引き戻される→次のフレームで再びRetreatFromTargetが
            // 対象から遠ざける、を繰り返して端でカクカクと高速に左右へ振り向き続ける不具合になって
            // いた。境界ちょうども「帯の内側」として扱うIsWithinMovementBoundsを使う。
            if (attackTarget != null && IsWithinMovementBounds(bounds, unit.Position))
            {
                if (unit.AttackType == SlimeAttackType.Melee)
                {
                    ApproachTargetWithStandoff(unit, attackTarget, meleeContactDistance, bounds, dt);
                    return;
                }

                RetreatFromTarget(unit, attackTarget, bounds, dt);
                return;
            }

            Rect wanderBounds = attackTarget == null && unit.IsPlayerSide
                ? GetPlayerIdleHoldBounds(bounds)
                : bounds;
            TickWander(unit, wanderBounds, dt);
        }

        /// <summary>行動帯の中央付近に幅を絞った矩形(playerIdleHoldWidth)。高さは行動帯全体のまま。</summary>
        private Rect GetPlayerIdleHoldBounds(Rect bounds)
        {
            float width = Mathf.Min(playerIdleHoldWidth, bounds.width);
            return new Rect(bounds.center.x - width * 0.5f, bounds.yMin, width, bounds.height);
        }

        /// <summary>
        /// 近接タイプが対象へ向けて、standoff(接触とみなす距離)まで近づき続ける。射程外からの
        /// 接近(ChaseTarget)と射程内での微調整の両方をこの1メソッドに統一しており、攻撃の
        /// 発生タイミングに関わらず対象がいる間は常に近づこうとする。
        /// 目標地点は対象の実座標そのもの(target.Position)へ直進する。これは意図的な設計判断:
        /// 以前は「対象を中心・半径standoffの円周上のオフセット点(ユニットごとに独立した
        /// ランダムな角度)」へ向かう実装だったが、対象自身も同時に(自分の索敵結果に向けて)
        /// 動く場合、このオフセット点は毎フレームずれ続ける「動く目標」になる。等速の2体が
        /// 互いに「相手の実座標」を直接追い続ける場合は距離が幾何学的に必ず単調に縮まる
        /// (三角不等式により収束が保証される)が、互いに「相手のオフセット点」を追い続ける
        /// 場合はこの収束保証が成り立たない。特定の接近角度の組み合わせでは両者のオフセット点が
        /// 常に相手の少し先を指し続けてしまい、距離が一切縮まらないまま同じ方向へ延々と
        /// 並走し続ける(行動帯を大きく離れて彷徨い続ける)退化平衡状態に陥ることを実測で
        /// 確認した(気絶とタイマーを無効化し、2体を相互に唯一の対象とさせた状態で数百フレーム
        /// 疑似シミュレートし、距離がほぼ変化しないまま両者が同じ方向へ座標を伸ばし続ける
        /// ことを確認して特定。攻撃射程内でのみオフセット点へ切り替える折衷案も試したが、
        /// 射程内側でも同じ退化平衡が起こり得ることを再現テストで確認したため採用しなかった)。
        /// 「同じ対象を複数体が狙っても一点に完全収束しない」という目的は、接近フェーズの
        /// 目標地点ではなく、対象確定時に割り当てられる独立したランダム角度(EnsureApproachAngle)
        /// と、standoffへ到達した後のOrbitAroundTarget(対象を中心に各ユニット独立の角度で
        /// 周回し続ける)で満たされる。複数ユニットが同じ地点に同時到達しても、到達した瞬間から
        /// 各々が独立の角度・角速度で周回を始めるため自然に分散する。
        /// また、目標地点への到達だけを止まる条件にすると、対象を追い越して反対側へ回り込もうと
        /// する動きが延々と続き、複数体が絡むと振動・発散してしまう不具合があったため、実際の
        /// 対象との距離が既にstandoff以内まで縮まっていれば、目標地点への到達を待たずにそこで
        /// 移動を止める。ただし完全に静止させるのではなく、OrbitAroundTargetで対象を中心に
        /// 低速で周回させ続ける(GDD「常に動き続けている」方針、詳細はOrbitAroundTarget参照)。
        /// </summary>
        private void ApproachTargetWithStandoff(BattleUnit unit, BattleUnit target, float standoff, Rect bounds, float dt)
        {
            float distance = Vector2.Distance(unit.Position, target.Position);
            if (distance <= standoff)
            {
                OrbitAroundTarget(unit, target, standoff, bounds, dt);
                return;
            }

            Vector2 direction = (target.Position - unit.Position) / distance;
            unit.WanderTarget = target.Position;

            // distanceからstandoffを差し引いた残り距離でtravelをキャップすると、standoff直前で
            // 歩幅が無限に小さくなり続け(距離が縮むほど1ステップの移動量も縮む)、実際には
            // distance<=standoffの停止条件に到達できないまま無限に足踏みし続けてしまう
            // (ゼノンのパラドックスと同型の不具合、実測でOrbitAroundTargetが一度も発動せず
            // 完全に静止したままになることを確認して特定)。対象の実座標へ向けて単純に
            // distance分でキャップすることで、1ステップでstandoffを飛び越えて対象の目の前まで
            // 進むことを許容し、次のフレームでdistance<=standoffの停止条件(→Orbit)に
            // 確実に到達できるようにする。
            float travel = Mathf.Min(balanceData.CombatMovementSpeed * dt, distance);
            unit.Position += direction * travel;
        }

        /// <summary>
        /// 遠距離タイプが対象から離れる方向へ後退する(行動帯の外へは出ないようクランプする)。
        /// 既にRangedRetreatDistance以上離れていれば後退をやめ、OrbitAroundTargetによる
        /// 低速の周回移動に切り替える(完全静止にはしない、GDD「常に動き続けている」方針)。
        /// この停止条件がないと、対象(特に接近をやめて静止している近接タイプ)のわずかな位置補正のたびに、
        /// 既に十分離れているにも関わらず全速力で反対方向へ移動し直そうとしてしまい、対象が
        /// 自分の左右どちらにいるかが毎フレーム入れ替わって高速に振り向き続ける不具合になる
        /// (近接タイプのApproachTargetWithStandoffが持つ「対象に十分近づいたら止まる」条件と
        /// 対になる安定化条件)。
        /// また、後退中の向き(BattleUnit.RetreatDirection)は対象の座標を毎フレーム生で
        /// 参照して計算し直すのではなく、RetreatDirectionRefreshInterval秒おきにしか
        /// 再計算しない。近接タイプが接近し続けている間は対象の座標が毎フレームわずかに
        /// 揺れ動くため、生の向きをそのまま使うと、対象が自分の左右どちらにいるかの
        /// 符号がわずかなブレで反転するたびに全速力で切り返してしまい、これも高速な
        /// 左右反転の原因になっていた(RangedRetreatDistanceの停止条件だけでは、後退が
        /// まだアクティブな距離の間に起きるこの種のブレは防げない)。
        /// </summary>
        private void RetreatFromTarget(BattleUnit unit, BattleUnit target, Rect bounds, float dt)
        {
            float distanceToTarget = Vector2.Distance(unit.Position, target.Position);
            if (distanceToTarget >= rangedRetreatDistance)
            {
                OrbitAroundTarget(unit, target, rangedRetreatDistance, bounds, dt);
                return;
            }

            unit.RetreatDirectionRefreshTimer -= dt;
            if (unit.RetreatDirectionTarget != target || unit.RetreatDirectionRefreshTimer <= 0f)
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

                unit.RetreatDirection = away.normalized;
                unit.RetreatDirectionTarget = target;
                unit.RetreatDirectionRefreshTimer = retreatDirectionRefreshInterval;
            }

            Vector2 step = unit.RetreatDirection * (balanceData.CombatMovementSpeed * dt);
            Vector2 newPosition = unit.Position + step;
            newPosition.x = Mathf.Clamp(newPosition.x, bounds.xMin, bounds.xMax);
            newPosition.y = Mathf.Clamp(newPosition.y, bounds.yMin, bounds.yMax);

            // 行動帯の端に押し付けられていて、後退方向へ進もうとしてもクランプでほとんど位置が
            // 変わらない(=これ以上対象から離れられず追い詰められている)場合は、その場に固まらせず
            // OrbitAroundTargetに切り替える(retreatStuckMovementRatioのコメント参照)。
            float intendedTravel = step.magnitude;
            float actualTravel = Vector2.Distance(unit.Position, newPosition);
            if (intendedTravel > 0.0001f && actualTravel < intendedTravel * retreatStuckMovementRatio)
            {
                OrbitAroundTarget(unit, target, distanceToTarget, bounds, dt);
                return;
            }

            unit.Position = newPosition;
        }

        /// <summary>
        /// 近接タイプの接近(ApproachTargetWithStandoff)・遠距離タイプの後退(RetreatFromTarget)が
        /// それぞれの目標距離(standoff/rangedRetreatDistance)に達した後、その場で完全静止させず、
        /// 対象を中心・半径radiusの円周上を低速で周回させ続ける処理(共通化)。GDD「常に動き続けている
        /// (バウンド/転がる)キャラクターのため、待機モーションは用意しない」方針上、実座標が完全に
        /// 止まると見た目も静止してしまう(特に遠距離タイプは実移動量に応じて転がるアニメーションの
        /// ため顕著)。
        /// 周回の目標地点は、対象を確定した際にランダムに割り当てられる接近角度
        /// (BattleUnit.ApproachAngleRadians、EnsureApproachAngle参照)を毎フレーム少しずつ
        /// 進めることで作る。追従速度はCombatMovementSpeedではなくWanderSpeed(徘徊と同じ低速)を
        /// 使う。以前、接近目標(相手も同時に動く移動点)を高速(CombatMovementSpeed)で追い続けると
        /// 追い越し・振動が連鎖して発散する不具合があったため(ApproachTargetWithStandoffのコメント
        /// 参照)、周回では常に低速側を使い、同じ発散を起こさないようにしている。
        /// 座標は行動帯の外へ出ないようクランプする(対象が帯の端付近にいる場合、周回目標地点が
        /// 帯の外に出ることがあるため)。
        /// </summary>
        private void OrbitAroundTarget(BattleUnit unit, BattleUnit target, float radius, Rect bounds, float dt)
        {
            EnsureApproachAngle(unit, target);

            unit.ApproachAngleRadians += combatIdleOrbitAngularSpeed * Mathf.Deg2Rad * dt;
            if (unit.ApproachAngleRadians > Mathf.PI * 2f)
            {
                unit.ApproachAngleRadians -= Mathf.PI * 2f;
            }

            Vector2 offset = new(Mathf.Cos(unit.ApproachAngleRadians), Mathf.Sin(unit.ApproachAngleRadians));
            Vector2 orbitPoint = target.Position + offset * radius;
            Vector2 toOrbitPoint = orbitPoint - unit.Position;
            float distance = toOrbitPoint.magnitude;
            if (distance <= 0.0001f)
            {
                return;
            }

            Vector2 direction = toOrbitPoint / distance;
            float travel = Mathf.Min(balanceData.WanderSpeed * dt, distance);
            Vector2 newPosition = unit.Position + direction * travel;
            newPosition.x = Mathf.Clamp(newPosition.x, bounds.xMin, bounds.xMax);
            newPosition.y = Mathf.Clamp(newPosition.y, bounds.yMin, bounds.yMax);
            unit.Position = newPosition;
        }

        private void TickWander(BattleUnit unit, Rect bounds, float dt)
        {
            Vector2 toTarget = unit.WanderTarget - unit.Position;
            float distance = toTarget.magnitude;

            // 目標に到達したら静止せず即座に次の目標を選び直す(常に動き回っているように見せる)。
            if (distance <= balanceData.WanderArriveThreshold)
            {
                unit.WanderTarget = PickWanderTarget(unit.Position, bounds);
                return;
            }

            Vector2 step = toTarget.normalized * (balanceData.WanderSpeed * dt);
            unit.Position = step.sqrMagnitude >= toTarget.sqrMagnitude ? unit.WanderTarget : unit.Position + step;
        }

        /// <summary>
        /// 徘徊の次の目標を選ぶ。現在位置のすぐ近くが選ばれると、ほとんど移動していないのに
        /// 進行方向(見た目側の向き・回転)だけが反転して見える不具合になるため、現在位置から
        /// ある程度離れた点が出るまで数回だけ再抽選する(狭い行動帯では毎回条件を満たすとは
        /// 限らないため、規定回数で妥協して最後に抽選した点を使う)。
        /// </summary>
        private static Vector2 PickWanderTarget(Vector2 currentPosition, Rect bounds)
        {
            float minDistance = Mathf.Max(bounds.width, bounds.height) * 0.4f;
            Vector2 candidate = PickRandomPointInBounds(bounds);
            for (int attempt = 0; attempt < 8 && Vector2.Distance(candidate, currentPosition) < minDistance; attempt++)
            {
                candidate = PickRandomPointInBounds(bounds);
            }

            return candidate;
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

        private enum MeteorPhase
        {
            Windup,
            Rising,
            Hovering,
            Falling,
        }

        private class PendingMeteor
        {
            public BattleUnit Caster;
            public SlimeSkillData Skill;
            public MeteorPhase Phase;
            public float PhaseElapsed;
            public Vector2 LandingPosition;
        }
    }

    /// <summary>
    /// デバッグ表示専用: 必殺技の当たり判定(Physics2D.OverlapCircleAll/OverlapBoxAll)が実際に
    /// 実行された瞬間の形状情報。DungeonRunController.OnUltimateHitboxCheckedで通知され、
    /// Scene View上でのGizmos可視化(開発用、実際のGame Viewには表示されない)にのみ使う。
    /// </summary>
    /// <summary>デバッグ可視化(UltimateHitboxGizmoDebugView)向けの当たり判定の形状種別。</summary>
    public enum UltimateHitboxShapeKind
    {
        Circle,
        Box,
        Ellipse,
    }

    public readonly struct UltimateHitboxDebugInfo
    {
        public readonly UltimateHitboxShapeKind Shape;
        public readonly Vector2 Center;
        public readonly float Radius;
        public readonly Vector2 BoxSize;
        public readonly float AngleDegrees;

        public UltimateHitboxDebugInfo(Vector2 center, float radius)
        {
            Shape = UltimateHitboxShapeKind.Circle;
            Center = center;
            Radius = radius;
            BoxSize = default;
            AngleDegrees = 0f;
        }

        public UltimateHitboxDebugInfo(Vector2 center, Vector2 boxSize, float angleDegrees)
        {
            Shape = UltimateHitboxShapeKind.Box;
            Center = center;
            Radius = 0f;
            BoxSize = boxSize;
            AngleDegrees = angleDegrees;
        }

        private UltimateHitboxDebugInfo(Vector2 center, Vector2 ellipseRadii, bool _)
        {
            Shape = UltimateHitboxShapeKind.Ellipse;
            Center = center;
            Radius = 0f;
            BoxSize = ellipseRadii;
            AngleDegrees = 0f;
        }

        /// <summary>楕円形の当たり判定情報を作る。radiusX/radiusYはBoxSizeに(半径として、直径ではない)格納する。</summary>
        public static UltimateHitboxDebugInfo Ellipse(Vector2 center, float radiusX, float radiusY)
        {
            return new UltimateHitboxDebugInfo(center, new Vector2(radiusX, radiusY), true);
        }
    }
}
