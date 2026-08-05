using UnityEngine;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// 全攻撃(近接・遠距離の通常攻撃、単体対象必殺技、範囲必殺技)のコライダーベースの当たり判定に
    /// 使うトリガーコライダー。あくまで「何に触れたか」を検出するためだけのコンポーネントで、
    /// ダメージ計算・タイプ相性判定・射程の数値そのものなどの戦闘ロジックは一切持たない
    /// (それらはDungeonRunController側に一元化する)。
    /// BattleUnitViewと同じGameObjectに付与し、見た目の描画位置(BattleUnit.Positionを反映した
    /// transform.position)をそのままコライダー位置として利用する。
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public class BattleUnitHurtbox : MonoBehaviour
    {
        private CircleCollider2D hitCollider;

        /// <summary>このコライダーが紐づくBattleUnit。Overlapクエリの結果からユニットを逆引きするために使う。</summary>
        public BattleUnit Unit { get; private set; }

        private void Awake()
        {
            hitCollider = GetComponent<CircleCollider2D>();
            hitCollider.isTrigger = true;
        }

        /// <summary>紐づくユニット・半径・所属レイヤー(Player/Enemyで別レイヤーにし、範囲攻撃のクエリで敵味方を区別する)を設定する。</summary>
        public void Bind(BattleUnit unit, float radius, int layer)
        {
            Unit = unit;
            gameObject.layer = layer;

            if (hitCollider == null)
            {
                hitCollider = GetComponent<CircleCollider2D>();
            }

            hitCollider.isTrigger = true;
            hitCollider.radius = radius;
        }
    }
}
