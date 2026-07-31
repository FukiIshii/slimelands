using System;
using UnityEngine;

namespace Slimelands.Village
{
    /// <summary>
    /// 村全体で共有する食料ストック(単一の数値)を管理する。
    /// 加算は「アイテム+納屋→食料ストック加算」のようなCardRecipeData(foodStockAmount)の完了によって行われ、
    /// 月齢経過時にまとめて消費される。
    /// </summary>
    public class FoodStockManager : MonoBehaviour
    {
        [Tooltip("初期の食料ストック。GDD上「食料値の具体的バランス」は未決定のため仮の値。")]
        [SerializeField] private int currentFood = 20;

        public int CurrentFood => currentFood;

        /// <summary>食料ストックが変化するたびに呼ばれる(引数は変化後の値)。</summary>
        public event Action<int> OnFoodChanged;

        public void AddFood(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            currentFood += amount;
            OnFoodChanged?.Invoke(currentFood);
        }

        /// <summary>
        /// 指定量を消費できる場合のみ実際に消費してtrueを返す。不足している場合は何も変更せずfalseを返す
        /// (月齢経過時、消費量合計がストックを上回るときは一切差し引かない仕様のため)。
        /// </summary>
        public bool TryConsume(int amount)
        {
            int nonNegativeAmount = Mathf.Max(0, amount);
            if (currentFood < nonNegativeAmount)
            {
                return false;
            }

            currentFood -= nonNegativeAmount;
            OnFoodChanged?.Invoke(currentFood);
            return true;
        }
    }
}
