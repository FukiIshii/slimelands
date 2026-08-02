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
        private const float IdleHopPeriod = 0.55f;
        private const float IdleHopHeight = 0.16f;
        private const float FaintFadeDuration = 0.35f;
        private const float WanderSpeed = 1.2f;
        private const float WanderArriveThreshold = 0.15f;
        private const float WanderPauseMin = 0.3f;
        private const float WanderPauseMax = 1.5f;
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
        private float idleHopTimer;
        private bool isFaintedVisual;
        private float faintTimer;

        private Rect movementBounds;
        private Vector3 wanderTarget;
        private float wanderPauseTimer;

        /// <summary>現在の基準位置(徘徊で常に移動する)。ホップ/突進はこの位置からの相対オフセットで表現する。</summary>
        public Vector3 CurrentPosition => homePosition;
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

        /// <summary>
        /// この見た目インスタンスを1体のBattleUnitに紐付け、初期表示を行う。
        /// movementBoundsは自由に動き回れる範囲(画面中央の帯、ワールド座標)。
        /// spawnPositionを省略した場合、初期位置はこの範囲内からランダムに選ぶ(通常の即時出現)。
        /// spawnPositionを指定した場合は、その位置(通常は範囲外)を起点とし、最初の徘徊目標を
        /// 範囲内に設定する。これにより既存のTickWanderがそのまま「歩いて登場する」入場モーション
        /// として機能する(敵の画面外からの登場演出に使用)。
        /// </summary>
        public void Bind(BattleUnit unit, Rect bounds, Vector3? spawnPosition = null)
        {
            boundUnit = unit;
            attackType = unit.AttackType;
            movementBounds = bounds;
            homePosition = spawnPosition ?? PickRandomPointInBounds();
            wanderTarget = spawnPosition.HasValue ? PickRandomPointInBounds() : homePosition;
            wanderPauseTimer = Random.Range(WanderPauseMin, WanderPauseMax);
            transform.position = homePosition;
            meleePhase = MeleeAnimPhase.Idle;
            idleHopTimer = 0f;
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

            // 待機中ホップの分だけ位置・スケールがずれている場合があるため、突進開始時に基準へ戻す
            // (ホップと突進の軌道が混ざって位置がスナップするのを防ぐ)。
            transform.position = homePosition;
            visual.localScale = Vector3.one;

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

            if (meleePhase != MeleeAnimPhase.Idle)
            {
                // 突進中は徘徊を止め、突進アニメーションのみ進行させる(軌道が混ざらないようにする)。
                TickMeleeAnim();
                return;
            }

            // 待機中(徘徊)は移動(TickWander)とその場アニメーション(ホップ/回転)を両方進める。
            TickWander();
            transform.position = homePosition;

            if (attackType == SlimeAttackType.Ranged)
            {
                visual.Rotate(Vector3.forward, -RollSpeedDegreesPerSecond * Time.deltaTime, Space.Self);
            }
            else
            {
                TickIdleHop();
            }
        }

        /// <summary>
        /// 画面中央の帯(movementBounds)の中を、ランダムな目標点に向かって歩き続ける徘徊ロジック。
        /// 到達すると少し静止してから次の目標点を選び直す。可動範囲は必ずmovementBounds内に収まる。
        /// </summary>
        private void TickWander()
        {
            Vector3 toTarget = wanderTarget - homePosition;
            float distance = toTarget.magnitude;

            if (distance <= WanderArriveThreshold)
            {
                wanderPauseTimer -= Time.deltaTime;
                if (wanderPauseTimer <= 0f)
                {
                    wanderTarget = PickRandomPointInBounds();
                    wanderPauseTimer = Random.Range(WanderPauseMin, WanderPauseMax);
                }

                return;
            }

            Vector3 step = toTarget.normalized * (WanderSpeed * Time.deltaTime);
            homePosition = step.sqrMagnitude >= toTarget.sqrMagnitude ? wanderTarget : homePosition + step;
        }

        private Vector3 PickRandomPointInBounds()
        {
            float x = Random.Range(movementBounds.xMin, movementBounds.xMax);
            float y = Random.Range(movementBounds.yMin, movementBounds.yMax);
            return new Vector3(x, y, 0f);
        }

        /// <summary>
        /// 近距離タイプの待機中ループモーション。攻撃時の突進とは独立しており、
        /// 敵味方を問わず常にホップし続ける(GDD「常に動き続けている」方針に合わせる)。
        /// 徘徊(TickWander)で更新されたhomePositionからの相対オフセットとして重ねて再生する。
        /// </summary>
        private void TickIdleHop()
        {
            idleHopTimer += Time.deltaTime;
            float phase = (idleHopTimer % IdleHopPeriod) / IdleHopPeriod;
            float hopCurve = Mathf.Sin(phase * Mathf.PI); // 0→1→0を繰り返す、常に0以上の連続ループ

            transform.position = homePosition + Vector3.up * (hopCurve * IdleHopHeight);
            visual.localScale = new Vector3(1f - hopCurve * 0.12f, 1f + hopCurve * 0.18f, 1f);
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
