using UnityEngine;
using Slimelands.Slime;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// 戦闘中の1体(BattleUnit)の見た目を担当する。ダメージ計算・ターゲティング等の戦闘ロジックには
    /// 一切触れず、DungeonRunControllerが発火する通知イベントを受けてDungeonBattleViewから呼ばれる
    /// 純粋な演出コンポーネント。
    /// このツール実行環境ではPlay Mode中にUnityのコルーチン/フレームが自動進行しないため、
    /// アニメーションはコルーチンを使わずUpdate()内の経過時間加算のみで駆動する
    /// (GameObject.SendMessage("Update", ...)による疑似フレーム送りでテスト可能にするため)。
    /// </summary>
    public class BattleUnitView : MonoBehaviour
    {
        private enum MeleeAnimPhase
        {
            Idle,
            DashOut,
            DashBack,
        }

        private const float RollSpeedDegreesPerSecond = 220f;
        private const float MeleeDashDuration = 0.18f;
        private const float FaintFadeDuration = 0.35f;
        private const float HpBarWidth = 0.9f;
        private const float HpBarHeight = 0.14f;
        private const float HpBarYOffset = 0.75f;

        private static readonly Color FaintedTint = new(0.45f, 0.45f, 0.45f, 0.5f);

        private Transform visual;
        private SpriteRenderer visualRenderer;
        private SpriteRenderer hpBarFillRenderer;
        private Transform hpBarFillTransform;

        private BattleUnit boundUnit;
        private SlimeAttackType attackType;
        private Vector3 homePosition;
        private Vector3 meleeDashTarget;
        private MeleeAnimPhase meleePhase = MeleeAnimPhase.Idle;
        private float meleeAnimTimer;
        private bool isFaintedVisual;
        private float faintTimer;

        public Vector3 HomePosition => homePosition;
        public bool IsPlayingAttackMotion => meleePhase != MeleeAnimPhase.Idle;

        private void Awake()
        {
            var visualGo = new GameObject("Visual", typeof(SpriteRenderer));
            visualGo.transform.SetParent(transform, false);
            visual = visualGo.transform;
            visualRenderer = visualGo.GetComponent<SpriteRenderer>();
            visualRenderer.sortingOrder = 10;

            var bgGo = new GameObject("HpBarBackground", typeof(SpriteRenderer));
            bgGo.transform.SetParent(transform, false);
            bgGo.transform.localPosition = new Vector3(0f, HpBarYOffset, 0f);
            bgGo.transform.localScale = new Vector3(HpBarWidth, HpBarHeight, 1f);
            SpriteRenderer bgRenderer = bgGo.GetComponent<SpriteRenderer>();
            bgRenderer.sprite = RuntimeSpriteFactory.GetSquareSprite(new Color(0.1f, 0.1f, 0.1f, 0.85f));
            bgRenderer.sortingOrder = 20;

            var fillGo = new GameObject("HpBarFill", typeof(SpriteRenderer));
            fillGo.transform.SetParent(transform, false);
            fillGo.transform.localScale = new Vector3(HpBarWidth, HpBarHeight * 0.7f, 1f);
            hpBarFillTransform = fillGo.transform;
            hpBarFillRenderer = fillGo.GetComponent<SpriteRenderer>();
            hpBarFillRenderer.sprite = RuntimeSpriteFactory.GetSquareSprite(Color.white);
            hpBarFillRenderer.sortingOrder = 21;
        }

        /// <summary>この見た目インスタンスを1体のBattleUnitに紐付け、初期表示を行う。</summary>
        public void Bind(BattleUnit unit, Vector3 worldHomePosition)
        {
            boundUnit = unit;
            attackType = unit.AttackType;
            homePosition = worldHomePosition;
            transform.position = worldHomePosition;
            meleePhase = MeleeAnimPhase.Idle;
            isFaintedVisual = false;
            faintTimer = 0f;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;

            Sprite sprite = unit.Species != null ? unit.Species.BattleSprite : null;
            if (sprite == null)
            {
                sprite = RuntimeSpriteFactory.GetCircleSprite(
                    RuntimeSpriteFactory.GetFallbackColor(attackType, unit.IsPlayerSide));
            }

            visualRenderer.sprite = sprite;
            visualRenderer.color = Color.white;
            visualRenderer.flipX = !unit.IsPlayerSide;

            RefreshHpBar();
        }

        /// <summary>近接タイプの通常攻撃/必殺技モーション(ターゲットへ跳ねて突進し、戻ってくる)を再生する。</summary>
        public void PlayAttackMotion(Vector3 targetWorldPosition, bool isUltimate = false)
        {
            if (attackType != SlimeAttackType.Melee || isFaintedVisual)
            {
                return;
            }

            float reach = isUltimate ? 0.7f : 0.5f;
            meleeDashTarget = Vector3.Lerp(homePosition, targetWorldPosition, reach);
            meleePhase = MeleeAnimPhase.DashOut;
            meleeAnimTimer = 0f;
        }

        /// <summary>戦闘不能(HP0)になった際の見た目(グレーアウト)へ切り替える。</summary>
        public void SetFainted()
        {
            isFaintedVisual = true;
            faintTimer = 0f;
            meleePhase = MeleeAnimPhase.Idle;
            transform.position = homePosition;
            visual.localScale = Vector3.one;
        }

        private void Update()
        {
            if (boundUnit == null)
            {
                return;
            }

            RefreshHpBar();

            if (isFaintedVisual)
            {
                faintTimer = Mathf.Min(FaintFadeDuration, faintTimer + Time.deltaTime);
                visualRenderer.color = Color.Lerp(Color.white, FaintedTint, faintTimer / FaintFadeDuration);
                return;
            }

            if (attackType == SlimeAttackType.Ranged)
            {
                visual.Rotate(Vector3.forward, -RollSpeedDegreesPerSecond * Time.deltaTime, Space.Self);
            }

            if (meleePhase != MeleeAnimPhase.Idle)
            {
                TickMeleeAnim();
            }
        }

        private void TickMeleeAnim()
        {
            const float half = MeleeDashDuration * 0.5f;
            meleeAnimTimer += Time.deltaTime;
            float p = Mathf.Clamp01(meleeAnimTimer / half);

            if (meleePhase == MeleeAnimPhase.DashOut)
            {
                transform.position = Vector3.Lerp(homePosition, meleeDashTarget, p);
                visual.localScale = Vector3.Lerp(Vector3.one, new Vector3(1.3f, 0.7f, 1f), p);
                if (p >= 1f)
                {
                    meleePhase = MeleeAnimPhase.DashBack;
                    meleeAnimTimer = 0f;
                }
            }
            else
            {
                transform.position = Vector3.Lerp(meleeDashTarget, homePosition, p);
                visual.localScale = Vector3.Lerp(new Vector3(1.3f, 0.7f, 1f), Vector3.one, p);
                if (p >= 1f)
                {
                    meleePhase = MeleeAnimPhase.Idle;
                    transform.position = homePosition;
                    visual.localScale = Vector3.one;
                }
            }
        }

        private void RefreshHpBar()
        {
            int maxHp = Mathf.Max(1, boundUnit.Stats.maxHp);
            float ratio = Mathf.Clamp01((float)boundUnit.CurrentHp / maxHp);

            hpBarFillTransform.localScale = new Vector3(HpBarWidth * ratio, HpBarHeight * 0.7f, 1f);
            hpBarFillTransform.localPosition =
                new Vector3(-HpBarWidth * 0.5f * (1f - ratio), HpBarYOffset, 0f);
            hpBarFillRenderer.color = Color.Lerp(new Color(0.85f, 0.2f, 0.2f), new Color(0.3f, 0.85f, 0.3f), ratio);
        }
    }
}
