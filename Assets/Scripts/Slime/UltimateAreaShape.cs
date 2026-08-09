namespace Slimelands.Slime
{
    /// <summary>
    /// 必殺技(isUltimate=true)がダメージを与える範囲の形状。SingleTarget以外はコライダー/物理演算を
    /// 使わず、実座標(BattleUnit.Position)同士の幾何計算(距離・線分距離)で命中判定を行う
    /// (このプロジェクトの検証手法がUpdate()の疑似フレーム送りに依存しており、実際のPhysics2D
    /// ステップが進行しないため)。
    /// </summary>
    public enum UltimateAreaShape
    {
        /// <summary>従来通り、単一の対象(GetCurrentTarget)にのみダメージを与える。</summary>
        SingleTarget,
        /// <summary>指定した一点(対象の座標)を中心とした円形範囲。</summary>
        Circle,
        /// <summary>発動者から対象方向へ伸びる直線状の範囲。</summary>
        Line,
        /// <summary>行動帯の左端〜右端を往復横切り続け、接触している間ダメージを与え続ける。</summary>
        Sweep,
        /// <summary>
        /// 発動者が高く飛び上がり、対象付近の上空で静止した後、対象付近へ落下する。着地の瞬間に
        /// 着地地点を中心とした円形範囲(Circleと同じQueryUnitsInCircle)でダメージを与える。
        /// 飛び上がってから着地するまでの間、発動者は通常の接近・後退移動と通常攻撃を行わない
        /// (BattleUnit.IsInSkillMotion)。上空にいる間(離地後〜着地前)は無敵として扱う
        /// (BattleUnit.IsInvulnerable)。
        /// </summary>
        Meteor,
    }
}
