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
        private const float RowSpacing = 1.6f;
        private const float PlayerRowY = -2.4f;
        private const float EnemyRowY = 2.4f;

        private static readonly Color PlayerVfxColor = new(0.9f, 0.9f, 0.9f);
        private static readonly Color EnemyVfxColor = new(0.9f, 0.5f, 0.3f);
        private static readonly Color UltimateVfxColor = new(1f, 0.85f, 0.2f);

        [SerializeField] private DungeonRunController controller;

        private readonly Dictionary<BattleUnit, BattleUnitView> views = new();

        private void OnEnable()
        {
            if (controller == null)
            {
                return;
            }

            controller.OnPartySpawned += HandlePartySpawned;
            controller.OnEnemySpawned += HandleEnemySpawned;
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
            controller.OnEnemySpawned -= HandleEnemySpawned;
            controller.OnNormalAttackPerformed -= HandleNormalAttack;
            controller.OnUltimateActivated -= HandleUltimateActivated;
            controller.OnUnitFainted -= HandleUnitFainted;
            controller.OnDungeonCleared -= ClearAllViews;
            controller.OnDungeonFailed -= ClearAllViews;
        }

        private void HandlePartySpawned(IReadOnlyList<BattleUnit> party)
        {
            ClearAllViews();
            for (int i = 0; i < party.Count; i++)
            {
                CreateView(party[i], GetSlotPosition(i, party.Count, PlayerRowY));
            }
        }

        private void HandleEnemySpawned(BattleUnit enemy)
        {
            ClearEnemyViews();
            CreateView(enemy, GetSlotPosition(0, 1, EnemyRowY));
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
                ? targetView.HomePosition
                : attackerView.HomePosition;

            Color vfxColor = isUltimate ? UltimateVfxColor : (attacker.IsPlayerSide ? PlayerVfxColor : EnemyVfxColor);

            if (attacker.AttackType == SlimeAttackType.Melee)
            {
                attackerView.PlayAttackMotion(targetPos, isUltimate);
                BattleVfx.SpawnBurst(attackerView.HomePosition, vfxColor);
            }
            else
            {
                var projectileGo = new GameObject("BattleProjectile", typeof(SpriteRenderer), typeof(BattleProjectile));
                BattleProjectile projectile = projectileGo.GetComponent<BattleProjectile>();
                float travelDuration = isUltimate ? 0.35f : 0.25f;
                Vector3 arrivalPos = targetPos;
                projectile.Launch(attackerView.HomePosition, targetPos, travelDuration, vfxColor,
                    () => BattleVfx.SpawnBurst(arrivalPos, vfxColor));
            }
        }

        private void CreateView(BattleUnit unit, Vector3 position)
        {
            var go = new GameObject($"BattleUnitView_{unit.DisplayName}", typeof(BattleUnitView));
            go.transform.SetParent(transform, false);
            BattleUnitView view = go.GetComponent<BattleUnitView>();
            view.Bind(unit, position);
            views[unit] = view;
        }

        private Vector3 GetSlotPosition(int index, int count, float rowY)
        {
            float totalWidth = (count - 1) * RowSpacing;
            float x = transform.position.x - totalWidth * 0.5f + index * RowSpacing;
            return new Vector3(x, transform.position.y + rowY, transform.position.z);
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
