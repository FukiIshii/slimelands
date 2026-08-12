namespace Slimelands.Slime
{
    /// <summary>
    /// 必殺技(isUltimate=true)がダメージを与える範囲の形状。SingleTarget以外は、範囲自体は
    /// Colliderを持たず、対象座標から計算した円/矩形の形でPhysics2D.OverlapCircleAll/
    /// OverlapBoxAllを1回だけ呼び出し、各ユニットが常時持っているCollider2D(BattleUnitHurtbox)
    /// を検出する方式で命中判定を行う(DungeonRunController.QueryUnitsInCircle/QueryUnitsInBox
    /// 参照)。攻撃側・防御側どちらもColliderの接触イベント(OnTriggerEnter等)には依存しない。
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
