using System;
using System.Collections.Generic;
using UnityEngine;
using Slimelands.Slime;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// 戦闘中の1体(BattleUnit)の見た目を担当する。ダメージ計算・ターゲティング等の戦闘ロジックには
    /// 一切触れず、DungeonRunControllerが発火する通知イベントを受けてDungeonBattleViewから呼ばれる
    /// 純粋な演出コンポーネント。徘徊による実座標(BattleUnit.Position)はロジック側が管理しており、
    /// このクラスは毎フレームそれを読み取って描画するだけで、自前ではシミュレーションしない。
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
        private const float MeleeDashBackDuration = 0.12f;
        private const float IdleHopPeriod = 0.55f;
        private const float IdleHopHeight = 0.16f;
        private const float FaintFadeDuration = 0.35f;
        // 敵側のみ: 灰色化(FaintFadeDuration)が終わってから、フェードアウトして見た目が消えるまでの
        // 追加時間(秒、仮値)。倒した敵がいつまでも灰色のまま残り続けないようにするため。
        // プレイヤー側は村に戻るまで見た目を残し続ける仕様のため対象外。
        private const float EnemyFaintRemovalDelay = 0.5f;
        private const float HpBarWidth = 0.9f;
        private const float HpBarHeight = 0.14f;
        private const float HpBarYOffset = 0.75f;
        // SlimeSpeciesData.BattleAnimationFramesのコマ送り速度(仮値、要調整)。気絶中は止める。
        private const float AnimationFrameDuration = 0.12f;

        private static readonly Color FaintedTint = new(0.45f, 0.45f, 0.45f, 0.5f);

        private Transform visual;
        private SpriteRenderer visualRenderer;
        private SpriteRenderer hpBarFillRenderer;
        private Transform hpBarFillTransform;
        private GameObject hpBarBackgroundGo;
        private GameObject hpBarFillGo;

        private BattleUnit boundUnit;
        private SlimeAttackType attackType;
        private Vector3 homePosition;
        private Vector3 meleeDashTarget;
        private MeleeAnimPhase meleePhase = MeleeAnimPhase.Idle;
        private float meleeAnimTimer;
        private float meleeDashOutDuration = 0.18f;
        private float idleHopTimer;
        private bool isFaintedVisual;
        private float faintTimer;
        private bool faintRemovalNotified;
        private IReadOnlyList<Sprite> animationFrames;
        private int animationFrameIndex;
        private float animationTimer;

        /// <summary>現在の基準位置(ロジック側のBattleUnit.Positionをそのまま反映)。ホップ/突進はこの位置からの相対オフセットで表現する。</summary>
        public Vector3 CurrentPosition => homePosition;
        public bool IsPlayingAttackMotion => meleePhase != MeleeAnimPhase.Idle;

        /// <summary>
        /// 敵側の気絶演出(灰色化→フェードアウト)が完全に終わった瞬間に発火する。
        /// DungeonBattleViewはこれを購読し、見た目(GameObject)を破棄する。プレイヤー側では発火しない。
        /// </summary>
        public event Action<BattleUnit> OnFaintVisualCompleted;

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
            hpBarBackgroundGo = bgGo;

            var fillGo = new GameObject("HpBarFill", typeof(SpriteRenderer));
            fillGo.transform.SetParent(transform, false);
            fillGo.transform.localScale = new Vector3(HpBarWidth, HpBarHeight * 0.7f, 1f);
            hpBarFillTransform = fillGo.transform;
            hpBarFillRenderer = fillGo.GetComponent<SpriteRenderer>();
            hpBarFillRenderer.sprite = RuntimeSpriteFactory.GetSquareSprite(Color.white);
            hpBarFillRenderer.sortingOrder = 21;
            hpBarFillGo = fillGo;
        }

        /// <summary>この見た目インスタンスを1体のBattleUnitに紐付け、初期表示を行う。初期位置はunit.Positionをそのまま使う。</summary>
        public void Bind(BattleUnit unit)
        {
            boundUnit = unit;
            attackType = unit.AttackType;
            homePosition = new Vector3(unit.Position.x, unit.Position.y, 0f);
            transform.position = homePosition;
            meleePhase = MeleeAnimPhase.Idle;
            idleHopTimer = 0f;
            isFaintedVisual = false;
            faintTimer = 0f;
            faintRemovalNotified = false;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;
            hpBarBackgroundGo.SetActive(true);
            hpBarFillGo.SetActive(true);

            animationFrames = unit.Species != null ? unit.Species.BattleAnimationFrames : null;
            animationFrameIndex = 0;
            animationTimer = 0f;

            Sprite sprite = animationFrames != null && animationFrames.Count > 0 ? animationFrames[0] : null;
            if (sprite == null)
            {
                sprite = unit.Species != null ? unit.Species.BattleSprite : null;
            }
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

        /// <summary>
        /// 近接タイプの通常攻撃/必殺技モーション(ターゲットへ跳ねて突進し、戻ってくる)を再生する。
        /// approachDurationは接近(DashOut)フェーズの所要時間で、ロジック側の命中判定タイミング
        /// (DungeonRunController.MeleeHitDelay)と一致させることで、実際にダメージが発生する瞬間と
        /// 視覚的に接触する瞬間を同期させる。
        /// </summary>
        public void PlayAttackMotion(Vector3 targetWorldPosition, bool isUltimate, float approachDuration)
        {
            if (attackType != SlimeAttackType.Melee || isFaintedVisual)
            {
                return;
            }

            // 待機中ホップの分だけ位置・スケールがずれている場合があるため、突進開始時に基準へ戻す
            // (ホップと突進の軌道が混ざって位置がスナップするのを防ぐ)。
            transform.position = homePosition;
            visual.localScale = Vector3.one;

            // 対象のY座標へは寄らず、常にhomePositionと同じ高さのまま真横(X方向)にのみ突進する
            // (縦の行動帯位置がずれていても斜めに突っ込まず、まっすぐ横方向のタックルに見せるため)。
            float reach = isUltimate ? 0.7f : 0.5f;
            Vector3 horizontalTarget = new Vector3(targetWorldPosition.x, homePosition.y, homePosition.z);
            meleeDashTarget = Vector3.Lerp(homePosition, horizontalTarget, reach);
            meleeDashOutDuration = Mathf.Max(0.01f, approachDuration);
            meleePhase = MeleeAnimPhase.DashOut;
            meleeAnimTimer = 0f;
        }

        /// <summary>戦闘不能(HP0)になった際の見た目(グレーアウト、敵側はその後フェードアウト)へ切り替える。</summary>
        public void SetFainted()
        {
            isFaintedVisual = true;
            faintTimer = 0f;
            faintRemovalNotified = false;
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
                faintTimer += Time.deltaTime;
                float tintProgress = Mathf.Clamp01(faintTimer / FaintFadeDuration);
                Color tinted = Color.Lerp(Color.white, FaintedTint, tintProgress);

                // 敵側のみ、灰色化が終わった後さらにフェードアウトさせて見た目を消す。プレイヤー側は
                // 村に戻るまで見た目を残し続ける仕様のため、灰色のまま(このifに入らない)にする。
                if (!boundUnit.IsPlayerSide)
                {
                    float fadeElapsed = faintTimer - FaintFadeDuration;
                    if (fadeElapsed >= 0f)
                    {
                        if (hpBarBackgroundGo.activeSelf)
                        {
                            hpBarBackgroundGo.SetActive(false);
                            hpBarFillGo.SetActive(false);
                        }

                        float fadeProgress = Mathf.Clamp01(fadeElapsed / EnemyFaintRemovalDelay);
                        tinted.a = FaintedTint.a * (1f - fadeProgress);

                        if (fadeProgress >= 1f && !faintRemovalNotified)
                        {
                            faintRemovalNotified = true;
                            OnFaintVisualCompleted?.Invoke(boundUnit);
                        }
                    }
                }

                visualRenderer.color = tinted;
                return;
            }

            TickAnimation();

            if (meleePhase != MeleeAnimPhase.Idle)
            {
                // 突進中はロジック側の座標追従を止め、突進アニメーションのみ進行させる
                // (軌道が混ざらないようにする。ロジック側の徘徊自体は裏で進み続けており、
                // 突進が終わった次のIdleフレームで最新のBattleUnit.Positionに追いつく)。
                TickMeleeAnim();
                return;
            }

            // 待機中は、ロジック側が徘徊シミュレーションで更新したBattleUnit.Positionをそのまま反映する。
            homePosition = new Vector3(boundUnit.Position.x, boundUnit.Position.y, 0f);
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
        /// SlimeSpeciesData.BattleAnimationFramesが2枚以上設定されている場合のみ、一定間隔で
        /// コマを進めてループ再生する(1枚以下なら何もしない=従来通りの静止画表示)。
        /// ホップ/突進の位置・拡縮アニメーションとは独立して、常時進行させる。
        /// </summary>
        private void TickAnimation()
        {
            if (animationFrames == null || animationFrames.Count < 2)
            {
                return;
            }

            animationTimer += Time.deltaTime;
            if (animationTimer < AnimationFrameDuration)
            {
                return;
            }

            animationTimer -= AnimationFrameDuration;
            animationFrameIndex = (animationFrameIndex + 1) % animationFrames.Count;
            visualRenderer.sprite = animationFrames[animationFrameIndex];
        }

        /// <summary>
        /// 近距離タイプの待機中ループモーション。攻撃時の突進とは独立しており、
        /// 敵味方を問わず常にホップし続ける(GDD「常に動き続けている」方針に合わせる)。
        /// ロジック側の徘徊で更新されたhomePositionからの相対オフセットとして重ねて再生する。
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
            meleeAnimTimer += Time.deltaTime;

            if (meleePhase == MeleeAnimPhase.DashOut)
            {
                float p = Mathf.Clamp01(meleeAnimTimer / meleeDashOutDuration);
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
                float p = Mathf.Clamp01(meleeAnimTimer / MeleeDashBackDuration);
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
