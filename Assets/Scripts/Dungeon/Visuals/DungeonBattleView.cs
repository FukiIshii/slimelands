using System.Collections.Generic;
using UnityEngine;
using Slimelands.Slime;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// DungeonRunControllerの通知イベントを購読し、スプライト表示・攻撃エフェクトを統括する
    /// 見た目専用のコンポーネント。戦闘ロジック(ダメージ計算・ターゲティング・クールタイム等)には
    /// 一切書き込まない、完全に反応的(リアクティブ)な演出レイヤー。
    /// </summary>
    public class DungeonBattleView : MonoBehaviour
    {
        [Tooltip("プレイヤー・敵とも自由に動き回る帯の幅(ワールド単位)。")]
        [SerializeField] private float movementBandWidth = 11f;
        [Tooltip("帯の高さ(ワールド単位)。背景画像の水平中央線(床と奥の境界線)から下方向へこの高さ分だけ帯を確保する。")]
        [SerializeField] private float movementBandHeight = 3f;

        [Header("敵の登場演出")]
        [Tooltip("敵が画面外のどちら側から歩いて登場するか。ダンジョン/演出方針に応じて製作者が調整する。")]
        [SerializeField] private EnemyEntranceSide enemyEntranceSide = EnemyEntranceSide.Right;
        [Tooltip("登場開始位置を行動帯の端からどれだけ外側(画面外方向)に離すか(ワールド単位)。")]
        [SerializeField] private float enemyEntranceMargin = 5f;

        private static readonly Color PlayerVfxColor = new(0.9f, 0.9f, 0.9f);
        private static readonly Color EnemyVfxColor = new(0.9f, 0.5f, 0.3f);
        private static readonly Color UltimateVfxColor = new(1f, 0.85f, 0.2f);

        [SerializeField] private DungeonRunController controller;

        private readonly Dictionary<BattleUnit, BattleUnitView> views = new();
        private Transform visualsRoot;

        /// <summary>
        /// 生成した見た目(スライム・発射物)を全てぶら下げる専用コンテナ。画面切り替え側は
        /// これをまとめてSetActiveするだけで、ダンジョン戦闘の見た目一式を表示/非表示できる。
        /// Awake()に依存すると、Edit Mode上のスクリプト操作(ドメインリロード後など)で
        /// 未初期化のまま参照される事故につながるため、遅延初期化で自己修復させる。
        /// </summary>
        public GameObject VisualsRoot => EnsureVisualsRoot().gameObject;

        private Transform EnsureVisualsRoot()
        {
            if (visualsRoot == null)
            {
                // Edit Mode上での操作やPlay Mode開始直後など、フィールドが未初期化な状態で
                // 呼ばれても、既にシーンに存在する同名の子を再利用して重複生成を防ぐ。
                visualsRoot = transform.Find("BattleVisuals");
            }

            if (visualsRoot == null)
            {
                var root = new GameObject("BattleVisuals");
                root.transform.SetParent(transform, false);
                visualsRoot = root.transform;
            }

            return visualsRoot;
        }

        private void OnEnable()
        {
            if (controller == null)
            {
                return;
            }

            controller.OnPartySpawned += HandlePartySpawned;
            controller.OnEnemyWaveSpawned += HandleEnemyWaveSpawned;
            controller.OnNormalAttackPerformed += HandleNormalAttack;
            controller.OnUltimateActivated += HandleUltimateActivated;
            controller.OnUnitFainted += HandleUnitFainted;
            controller.OnDungeonCleared += ClearAllViews;
            controller.OnDungeonFailed += ClearAllViews;
        }

        private void OnDisable()
        {
            if (controller == null)
            {
                return;
            }

            controller.OnPartySpawned -= HandlePartySpawned;
            controller.OnEnemyWaveSpawned -= HandleEnemyWaveSpawned;
            controller.OnNormalAttackPerformed -= HandleNormalAttack;
            controller.OnUltimateActivated -= HandleUltimateActivated;
            controller.OnUnitFainted -= HandleUnitFainted;
            controller.OnDungeonCleared -= ClearAllViews;
            controller.OnDungeonFailed -= ClearAllViews;
        }

        private void HandlePartySpawned(IReadOnlyList<BattleUnit> party)
        {
            ClearAllViews();
            foreach (BattleUnit unit in party)
            {
                CreateView(unit, useEntrance: false);
            }
        }

        private void HandleEnemyWaveSpawned(IReadOnlyList<BattleUnit> enemies)
        {
            ClearEnemyViews();
            foreach (BattleUnit unit in enemies)
            {
                CreateView(unit, useEntrance: true);
            }
        }

        private void HandleNormalAttack(BattleUnit attacker, BattleUnit target, int damage)
        {
            PlayAttackVisual(attacker, target, isUltimate: false);
        }

        private void HandleUltimateActivated(BattleUnit attacker, BattleUnit target, int damage)
        {
            PlayAttackVisual(attacker, target, isUltimate: true);
        }

        private void HandleUnitFainted(BattleUnit unit)
        {
            if (views.TryGetValue(unit, out BattleUnitView view) && view != null)
            {
                view.SetFainted();
            }
        }

        private void PlayAttackVisual(BattleUnit attacker, BattleUnit target, bool isUltimate)
        {
            if (!views.TryGetValue(attacker, out BattleUnitView attackerView) || attackerView == null)
            {
                return;
            }

            Vector3 targetPos = views.TryGetValue(target, out BattleUnitView targetView) && targetView != null
                ? targetView.CurrentPosition
                : attackerView.CurrentPosition;

            Color vfxColor = isUltimate ? UltimateVfxColor : (attacker.IsPlayerSide ? PlayerVfxColor : EnemyVfxColor);

            if (attacker.AttackType == SlimeAttackType.Melee)
            {
                attackerView.PlayAttackMotion(targetPos, isUltimate);
                BattleVfx.SpawnBurst(attackerView.CurrentPosition, vfxColor);
            }
            else
            {
                var projectileGo = new GameObject("BattleProjectile", typeof(SpriteRenderer), typeof(BattleProjectile));
                projectileGo.transform.SetParent(EnsureVisualsRoot(), false);
                BattleProjectile projectile = projectileGo.GetComponent<BattleProjectile>();
                float travelDuration = isUltimate ? 0.35f : 0.25f;
                Vector3 arrivalPos = targetPos;
                projectile.Launch(attackerView.CurrentPosition, targetPos, travelDuration, vfxColor,
                    () => BattleVfx.SpawnBurst(arrivalPos, vfxColor));
            }
        }

        /// <summary>
        /// useEntrance=trueの場合、敵を行動帯の外側(enemyEntranceSideで指定した側)に配置し、
        /// 最初の徘徊目標を帯内に設定することで、既存の徘徊ロジック(TickWander)をそのまま
        /// 「歩いて登場する」入場モーションとして流用する。
        /// </summary>
        private void CreateView(BattleUnit unit, bool useEntrance)
        {
            var go = new GameObject($"BattleUnitView_{unit.DisplayName}", typeof(BattleUnitView));
            go.transform.SetParent(EnsureVisualsRoot(), false);
            BattleUnitView view = go.GetComponent<BattleUnitView>();
            Rect bounds = GetMovementBounds();
            Vector3? spawnPosition = useEntrance ? GetEnemyEntrancePosition(bounds) : (Vector3?)null;
            view.Bind(unit, bounds, spawnPosition);
            views[unit] = view;
        }

        /// <summary>行動帯の右端または左端(enemyEntranceSide)から、外側にenemyEntranceMargin分離れた地点。</summary>
        private Vector3 GetEnemyEntrancePosition(Rect bounds)
        {
            float x = enemyEntranceSide == EnemyEntranceSide.Right
                ? bounds.xMax + enemyEntranceMargin
                : bounds.xMin - enemyEntranceMargin;
            float y = Random.Range(bounds.yMin, bounds.yMax);
            return new Vector3(x, y, 0f);
        }

        /// <summary>
        /// プレイヤー・敵とも共通で自由に動き回れる帯(ワールド座標のRect)。
        /// 背景スプライトはこのコンポーネントと同じ位置(transform.position.y)を水平中央線として
        /// 上下対称に配置されるため(DungeonBackgroundScroller参照)、その中央線は「床」と
        /// 「奥の壁・天井」の境界線に相当する。行動範囲を中央線から下(床側)のみに限定することで、
        /// 画像の下半分(床)でのみ敵味方が行動するようにする。
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

        private void ClearAllViews()
        {
            foreach (KeyValuePair<BattleUnit, BattleUnitView> entry in views)
            {
                if (entry.Value != null)
                {
                    Destroy(entry.Value.gameObject);
                }
            }

            views.Clear();
        }

        private void ClearEnemyViews()
        {
            var toRemove = new List<BattleUnit>();
            foreach (KeyValuePair<BattleUnit, BattleUnitView> entry in views)
            {
                if (entry.Key.IsPlayerSide)
                {
                    continue;
                }

                if (entry.Value != null)
                {
                    Destroy(entry.Value.gameObject);
                }

                toRemove.Add(entry.Key);
            }

            foreach (BattleUnit key in toRemove)
            {
                views.Remove(key);
            }
        }
    }
}
