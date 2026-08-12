namespace Slimelands.Slime
{
    /// <summary>
    /// 必殺技(isUltimate=true)がダメージを与える範囲の形状。SingleTarget以外は、範囲自体は
    /// Colliderを持たず、対象座標から計算した円/矩形/楕円の形で各ユニットが常時持っている
    /// Collider2D(BattleUnitHurtbox)を検出する方式で命中判定を行う
    /// (DungeonRunController.QueryUnitsInCircle/QueryUnitsInBox/QueryUnitsInEllipse参照。
    /// 楕円のみPhysics2Dにネイティブのクエリが無いため、円形の広域候補取得+楕円の方程式による
    /// 絞り込みで近似する)。攻撃側・防御側どちらもColliderの接触イベント(OnTriggerEnter等)には
    /// 依存しない。
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
        /// 着地地点を中心とした楕円形範囲(QueryUnitsInEllipse、X半径areaRadius・Y半径
        /// meteorAreaRadiusY)でダメージを与える。円形ではなく楕円形にすることで、横に長い
        /// 着弾範囲を表現できる(ユーザー要望)。
        /// 飛び上がってから着地するまでの間、発動者は通常の接近・後退移動と通常攻撃を行わない
        /// (BattleUnit.IsInSkillMotion)。上空にいる間(離地後〜着地前)は無敵として扱う
        /// (BattleUnit.IsInvulnerable)。
        /// </summary>
        Meteor,
    }
}
