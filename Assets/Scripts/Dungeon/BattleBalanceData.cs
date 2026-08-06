using UnityEngine;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// 戦闘の基本バランス値。数値バランスは未調整のため仮値。コード変更なしに調整できるよう
    /// ScriptableObjectにまとめてある。
    /// </summary>
    [CreateAssetMenu(fileName = "NewBattleBalance", menuName = "Slimelands/Dungeon/Battle Balance Data", order = 62)]
    public class BattleBalanceData : ScriptableObject
    {
        [Tooltip("通常攻撃が自動で発生する間隔(秒)。プレイヤー側・敵側で共通(仮)。")]
        [SerializeField] private float normalAttackInterval = 1.5f;
        [Tooltip("通常攻撃の技威力(SlimeSkillData.Powerと同じ単位)。")]
        [SerializeField] private int normalAttackPower = 10;

        [Header("行動帯での徘徊(仮値、要調整)")]
        [Tooltip("徘徊移動の速度(ワールド単位/秒)。")]
        [SerializeField] private float wanderSpeed = 1.2f;
        [Tooltip("徘徊目標に到達したとみなす距離。到達すると静止せず即座に次の目標を選び直す" +
                 "(常に動き回っているように見せるため、目標到達後の待機時間は設けていない)。")]
        [SerializeField] private float wanderArriveThreshold = 0.15f;

        [Header("交戦中の移動速度(仮値、要調整)")]
        [Tooltip("攻撃対象となる敵がいるときの移動速度(ワールド単位/秒)。近接タイプの接近(追尾)・" +
                 "遠距離タイプの後退(離脱)で使う。通常の徘徊速度(WanderSpeed)とは別に、" +
                 "戦闘時は素早く動けるよう個別に調整できるようにしてある。")]
        [SerializeField] private float combatMovementSpeed = 2.5f;

        [Header("近接攻撃(仮値、要調整)")]
        [Tooltip("近接攻撃(通常攻撃・必殺技とも)が実際に届く最大距離。攻撃者と対象の実座標の距離が" +
                 "これを超える場合、接近が間に合わず攻撃は命中しない(ダメージ0)。")]
        [SerializeField] private float meleeAttackRange = 6f;
        [Tooltip("近接攻撃を発動してから、実際に接触してダメージが発生するまでの遅延(秒)。" +
                 "見た目側の突進(接近)モーションの所要時間もこれに合わせて同期される。")]
        [SerializeField] private float meleeHitDelay = 0.25f;

        [Header("遠距離攻撃(仮値、要調整)")]
        [Tooltip("遠距離攻撃(通常攻撃・必殺技とも)が実際に届く最大距離。近接と同じくコライダーベースの" +
                 "射程判定に使う。デフォルトは行動帯を十分カバーする大きめの値にしてあり、通常プレイでは" +
                 "常に射程内(=従来通り必ず命中)になるが、値を絞ればあえて距離を離されると外れる仕様にも調整できる。")]
        [SerializeField] private float rangedAttackRange = 20f;

        [Header("必殺技の範囲攻撃(コライダー、仮値・要調整)")]
        [Tooltip("各ユニットが持つ当たり判定用コライダー(BattleUnitHurtbox)の半径。" +
                 "必殺技の範囲攻撃(Circle/Line/Sweep)がPhysics2Dのクエリで命中対象を検出する際に使う。")]
        [SerializeField] private float hurtboxRadius = 0.5f;

        [Header("見た目(仮値、要調整)")]
        [Tooltip("ダンジョン戦闘中に表示するスライムのスプライトサイズの倍率。HPバーはこの倍率の" +
                 "影響を受けず、スプライトのみ拡大縮小される。")]
        [SerializeField] private float unitVisualScale = 1.6f;

        public float NormalAttackInterval => normalAttackInterval;
        public int NormalAttackPower => normalAttackPower;
        public float WanderSpeed => wanderSpeed;
        public float WanderArriveThreshold => wanderArriveThreshold;
        public float CombatMovementSpeed => combatMovementSpeed;
        public float MeleeAttackRange => meleeAttackRange;
        public float MeleeHitDelay => meleeHitDelay;
        public float RangedAttackRange => rangedAttackRange;
        public float HurtboxRadius => hurtboxRadius;
        public float UnitVisualScale => unitVisualScale;
    }
}
