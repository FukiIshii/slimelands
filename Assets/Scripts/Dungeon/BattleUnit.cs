using UnityEngine;
using Slimelands.Slime;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// 戦闘中の1体(プレイヤー側・敵側どちらも)の実行時状態。MonoBehaviourではない純粋なC#クラス。
    /// プレイヤー側はSourceIndividualを持ち、戦闘終了時にHPを個体へ書き戻す。
    /// 敵側は新しいデータ構造を作らずSlimeSpeciesDataから都度生成する(SourceIndividualはnull)。
    /// </summary>
    public class BattleUnit
    {
        public string DisplayName { get; }
        public SlimeType Type { get; }
        public SlimeAttackType AttackType { get; }
        public SlimeSpeciesData Species { get; }
        public SlimeStats Stats { get; }
        public bool IsPlayerSide { get; }
        public SlimeIndividual SourceIndividual { get; }
        public SlimeSkillData UltimateSkill { get; }

        public int CurrentHp { get; private set; }
        public float NormalAttackTimer { get; set; }
        public float UltimateCooldownRemaining { get; set; }

        /// <summary>
        /// 戦闘中の実座標(ワールド単位)。DungeonRunControllerが徘徊シミュレーションで毎フレーム更新する。
        /// 見た目(BattleUnitView)はこの座標をそのまま描画の基準位置として使い、ホップ/転がり/突進などの
        /// 見た目のみのオフセットをその上に重ねる。近接攻撃の距離判定(命中/ミス)にも使用する。
        /// </summary>
        public Vector2 Position { get; set; }
        public Vector2 WanderTarget { get; set; }

        /// <summary>
        /// このユニットが自動照準(手動指定なし)で狙っている相手。索敵(近くの敵対ユニットから
        /// ランダムに選ぶ)で決めた結果を、対象が生存し続ける限り保持する(TickMovement等
        /// 毎フレーム参照される箇所で、フレームごとに違う相手へふらついてしまわないようにする
        /// ため)。対象が気絶したら次に必要になったタイミングで選び直される。
        /// </summary>
        public BattleUnit AutoTarget { get; set; }

        /// <summary>ApproachAngleRadiansがどの対象について決めたものかを示す。この値と現在の
        /// ターゲットが一致しなくなったら(対象が変わったら)接近角度を選び直す。</summary>
        public BattleUnit ApproachAngleTarget { get; set; }

        /// <summary>
        /// 近接タイプがtargetへ接近する際、targetを中心・半径standoffの円周上のどの角度
        /// (ラジアン)を目標地点にするか。対象が変わるまで保持する。複数のユニットが同じ対象を
        /// 狙う場合でも、この角度が独立してランダムに決まることで、対象の座標付近の一点に
        /// 完全収束せず接近方向がばらける。
        /// </summary>
        public float ApproachAngleRadians { get; set; }

        /// <summary>
        /// 遠距離タイプが後退する向き。対象の座標を毎フレーム生で参照して向きを計算し直すと、
        /// 対象(特に接近中の近接タイプ)のわずかな位置のブレにも全速力で反応してしまい、
        /// 高速に左右へ切り返し続ける不具合になる。これを防ぐため、RetreatDirectionRefreshTimerが
        /// 切れるまでは同じ向きを保持する(RetreatFromTarget参照)。
        /// </summary>
        public Vector2 RetreatDirection { get; set; }

        /// <summary>RetreatDirectionを再計算するまでの残り時間(秒)。0以下になったら計算し直す。</summary>
        public float RetreatDirectionRefreshTimer { get; set; }

        /// <summary>RetreatDirectionがどの対象について計算したものかを示す。対象が変わったら
        /// タイマーを待たずに即座に計算し直す。</summary>
        public BattleUnit RetreatDirectionTarget { get; set; }

        /// <summary>
        /// メテオ等、発動から着地/決着までの間、実座標を専用の演出ロジック(DungeonRunController側の
        /// pendingXxxリスト)が制御する必殺技を実行中かどうか。trueの間、TickMovement/TickBattleは
        /// このユニットの通常の接近・後退移動(TickUnitMovement)と通常攻撃を一時的にスキップする
        /// (両者が同じBattleUnit.Positionを競合して書き換えないようにするため)。
        /// </summary>
        public bool IsInSkillMotion { get; set; }

        /// <summary>
        /// 攻撃対象の検出(ResolveHurtboxUnits経由の全クエリ: 索敵・射程判定・範囲必殺技)から
        /// 除外され、通常攻撃・必殺技のいずれの被弾判定にも掛からない状態かどうか。メテオの
        /// 上空滞在中(離地後〜着地前)のように、一時的に「その場にいない」ものとして扱いたい
        /// 演出のために用意する。
        /// </summary>
        public bool IsInvulnerable { get; set; }

        public bool IsFainted => CurrentHp <= 0;
        public bool HasUltimate => UltimateSkill != null;
        public bool CanActivateUltimate => HasUltimate && !IsFainted && !IsInSkillMotion && UltimateCooldownRemaining <= 0f;

        public BattleUnit(string displayName, SlimeType type, SlimeAttackType attackType, SlimeSpeciesData species,
            SlimeStats stats, bool isPlayerSide, SlimeIndividual sourceIndividual, SlimeSkillData ultimateSkill,
            int startingHp)
        {
            DisplayName = displayName;
            Type = type;
            AttackType = attackType;
            Species = species;
            Stats = stats;
            IsPlayerSide = isPlayerSide;
            SourceIndividual = sourceIndividual;
            UltimateSkill = ultimateSkill;
            CurrentHp = Mathf.Clamp(startingHp, 0, stats.maxHp);
        }

        public void ApplyDamage(int amount)
        {
            CurrentHp = Mathf.Max(0, CurrentHp - Mathf.Max(0, amount));
        }
    }
}
