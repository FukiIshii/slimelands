using System.Collections.Generic;
using UnityEngine;
using Slimelands.Slime;
using Slimelands.UI;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// DungeonRunControllerの通知イベントを購読し、スプライト表示・攻撃エフェクトを統括する
    /// 見た目専用のコンポーネント。戦闘ロジック(ダメージ計算・ターゲティング・クールタイム等)には
    /// 一切書き込まない、完全に反応的(リアクティブ)な演出レイヤー。行動帯・実座標(BattleUnit.Position)
    /// はDungeonRunController側が一元管理しており、このクラスはそれを読み取って描画するだけ。
    /// </summary>
    public class DungeonBattleView : MonoBehaviour
    {
        private static readonly Color PlayerVfxColor = new(0.9f, 0.9f, 0.9f);
        private static readonly Color EnemyVfxColor = new(0.9f, 0.5f, 0.3f);
        private static readonly Color UltimateVfxColor = new(1f, 0.85f, 0.2f);
        private static readonly Color PlayerDamagedNumberColor = new(1f, 0.35f, 0.35f);
        private static readonly Color EnemyDamagedNumberColor = Color.white;

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

            // クリア時は敵のみ消し、プレイヤー側は村に戻るまで画面に残す(全滅させて姿を消したくないため)。
            // 失敗時(パーティー全滅)はこれまで通り全ユニットの見た目を消す。
            controller.OnPartySpawned += HandlePartySpawned;
            controller.OnEnemyWaveSpawned += HandleEnemyWaveSpawned;
            controller.OnNormalAttackPerformed += HandleNormalAttack;
            controller.OnUltimateActivated += HandleUltimateActivated;
            controller.OnNormalAttackResolved += HandleNormalAttackResolved;
            controller.OnUltimateResolved += HandleUltimateResolved;
            controller.OnDamageDealt += HandleDamageDealt;
            controller.OnUnitFainted += HandleUnitFainted;
            controller.OnDungeonCleared += ClearEnemyViews;
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
            controller.OnNormalAttackResolved -= HandleNormalAttackResolved;
            controller.OnUltimateResolved -= HandleUltimateResolved;
            controller.OnDamageDealt -= HandleDamageDealt;
            controller.OnUnitFainted -= HandleUnitFainted;
            controller.OnDungeonCleared -= ClearEnemyViews;
            controller.OnDungeonFailed -= ClearAllViews;
        }

        private void HandlePartySpawned(IReadOnlyList<BattleUnit> party)
        {
            ClearAllViews();
            foreach (BattleUnit unit in party)
            {
                CreateView(unit);
            }
        }

        private void HandleEnemyWaveSpawned(IReadOnlyList<BattleUnit> enemies)
        {
            ClearEnemyViews();
            foreach (BattleUnit unit in enemies)
            {
                CreateView(unit);
            }
        }

        private void HandleNormalAttack(BattleUnit attacker, BattleUnit target, int damage)
        {
            BeginAttackVisual(attacker, target, isUltimate: false);
        }

        private void HandleUltimateActivated(BattleUnit attacker, BattleUnit target, int damage)
        {
            BeginAttackVisual(attacker, target, isUltimate: true);
        }

        private void HandleNormalAttackResolved(BattleUnit attacker, BattleUnit target, int damage)
        {
            ResolveMeleeAttackVisual(attacker, target, damage, isUltimate: false);
        }

        private void HandleUltimateResolved(BattleUnit attacker, BattleUnit target, int damage)
        {
            ResolveMeleeAttackVisual(attacker, target, damage, isUltimate: true);
        }

        /// <summary>
        /// ダメージ発生源(通常攻撃/単体・範囲必殺技/スイープ)を問わず、対象に実際にダメージが
        /// 反映されるたびに呼ばれる。対象の現在位置の周辺にダメージ数字を表示し、上昇しながら
        /// フェードアウトさせる(FloatingNumberPopup側の演出)。
        /// </summary>
        private void HandleDamageDealt(BattleUnit attacker, BattleUnit target, int damage)
        {
            if (damage <= 0 || !views.TryGetValue(target, out BattleUnitView targetView) || targetView == null)
            {
                return;
            }

            Color color = target.IsPlayerSide ? PlayerDamagedNumberColor : EnemyDamagedNumberColor;
            FloatingNumberPopup.Spawn(targetView.CurrentPosition, damage, color);
        }

        /// <summary>
        /// 敵側の気絶演出(灰色化→フェードアウト)が完全に終わった瞬間に呼ばれる。見た目(GameObject)を
        /// 破棄する。BattleUnitView側でプレイヤー側では発火しないよう既にガードされているため、
        /// ここでは敵味方を判定せず単純にviewsから取り除くだけでよい。
        /// </summary>
        private void HandleFaintVisualCompleted(BattleUnit unit)
        {
            if (views.TryGetValue(unit, out BattleUnitView view) && view != null)
            {
                Destroy(view.gameObject);
            }

            views.Remove(unit);
        }

        private void HandleUnitFainted(BattleUnit unit)
        {
            if (views.TryGetValue(unit, out BattleUnitView view) && view != null)
            {
                view.SetFainted();
            }
        }

        /// <summary>
        /// 攻撃の「開始」演出。近接タイプは対象へ向けて接近(突進)モーションを再生するのみで、
        /// ヒットエフェクトはここでは出さない(ロジック側のOnNormalAttackResolved/OnUltimateResolvedで
        /// 決着した瞬間に出す)。遠距離タイプは従来通り、発射から着弾までを1本の弾で完結させる
        /// (ロジック側は距離によるミス判定をしないため、開始と決着が実質同時)。
        /// </summary>
        private void BeginAttackVisual(BattleUnit attacker, BattleUnit target, bool isUltimate)
        {
            if (!views.TryGetValue(attacker, out BattleUnitView attackerView) || attackerView == null)
            {
                return;
            }

            Vector3 targetPos = views.TryGetValue(target, out BattleUnitView targetView) && targetView != null
                ? targetView.CurrentPosition
                : attackerView.CurrentPosition;

            if (attacker.AttackType == SlimeAttackType.Melee)
            {
                float approachDuration = controller != null ? controller.MeleeHitDelay : 0.18f;
                attackerView.PlayAttackMotion(targetPos, isUltimate, approachDuration);
            }
            else
            {
                Color vfxColor = isUltimate ? UltimateVfxColor : (attacker.IsPlayerSide ? PlayerVfxColor : EnemyVfxColor);
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
        /// 近接攻撃の決着演出。damageが0(距離が届かずミス)の場合はヒットエフェクトを出さない。
        /// ヒット時は対象の現在位置にエフェクトを出す(接触した瞬間を表現するため)。
        /// </summary>
        private void ResolveMeleeAttackVisual(BattleUnit attacker, BattleUnit target, int damage, bool isUltimate)
        {
            if (damage <= 0)
            {
                return;
            }

            Vector3 vfxPosition = views.TryGetValue(target, out BattleUnitView targetView) && targetView != null
                ? targetView.CurrentPosition
                : (views.TryGetValue(attacker, out BattleUnitView attackerView) && attackerView != null
                    ? attackerView.CurrentPosition
                    : Vector3.zero);

            Color vfxColor = isUltimate ? UltimateVfxColor : (attacker.IsPlayerSide ? PlayerVfxColor : EnemyVfxColor);
            BattleVfx.SpawnBurst(vfxPosition, vfxColor);
        }

        private void CreateView(BattleUnit unit)
        {
            var go = new GameObject($"BattleUnitView_{unit.DisplayName}",
                typeof(BattleUnitView), typeof(CircleCollider2D), typeof(BattleUnitHurtbox));
            go.transform.SetParent(EnsureVisualsRoot(), false);
            BattleUnitView view = go.GetComponent<BattleUnitView>();
            view.Bind(unit);
            view.OnFaintVisualCompleted += HandleFaintVisualCompleted;
            views[unit] = view;

            // 全攻撃(近接・遠距離の通常攻撃、単体対象必殺技、範囲必殺技)のコライダーベースの当たり判定に
            // 使うトリガーコライダー。見た目の描画位置(このGameObjectのtransform)をそのまま当たり判定に使う。
            if (controller != null)
            {
                BattleUnitHurtbox hurtbox = go.GetComponent<BattleUnitHurtbox>();
                int layer = unit.IsPlayerSide ? controller.PlayerHurtboxLayer : controller.EnemyHurtboxLayer;
                hurtbox.Bind(unit, controller.HurtboxRadius, layer);
            }
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
