using UnityEngine;

namespace Slimelands.Cards
{
    /// <summary>
    /// スライムカード下部の装備インジケーター(小さな楕円)の当たり判定オブジェクトに付与する目印。
    /// CardBoardはクリック位置にこのコンポーネントが見つかった場合、通常のカードドラッグ開始ではなく
    /// 装備中アクセサリーのオーバーレイ表示切り替えとして処理する(親CardControllerを参照して判定する)。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class EquipmentIndicatorMarker : MonoBehaviour
    {
    }
}
