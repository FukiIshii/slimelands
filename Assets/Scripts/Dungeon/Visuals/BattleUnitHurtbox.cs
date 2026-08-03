using UnityEngine;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// 必殺技の範囲攻撃(コライダーベースの当たり判定)専用のトリガーコライダー。
    /// あくまで「何に触れたか」を検出するためだけのコンポーネントで、ダメージ計算・タイプ相性判定
    /// などの戦闘ロジックは一切持たない(それらはDungeonRunController側に一元化する)。
    /// 通常攻撃・移動などの既存ロジックにも一切関与しない。
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
