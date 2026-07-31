using System;
using UnityEngine;

namespace Slimelands.Village
{
    /// <summary>
    /// 村の月齢の経過を管理する。ひとまず一定時間の経過で1ヶ月進める(将来、一定操作数トリガーに
    /// 差し替える場合もAdvanceMonthNow()を呼ぶ形にすれば流用できる)。
    /// 月齢経過時、在籍する全スライムの消費量合計を食料ストックからまとめて差し引く。
    /// ストックが足りない場合は消費せずに進行をブロックし、スライムの売却や食料投入で
    /// 消費量合計がストック以下に戻ると自動的にブロックを解除して進行を再開する。
    /// </summary>
    public class MoonCycleManager : MonoBehaviour
    {
        [SerializeField] private FoodStockManager foodStockManager;
        [SerializeField] private VillageSlimeRoster slimeRoster;
        [Tooltip("1ヶ月が経過するまでの秒数(ひとまず時間経過方式)。")]
        [SerializeField] private float monthDurationSeconds = 30f;

        private float elapsedInMonth;

        public int CurrentMonth { get; private set; } = 1;
        public bool IsBlocked { get; private set; }

        /// <summary>現在の月の経過割合(0〜1)。ブロック中は進行しない。</summary>
        public float MonthProgress01 => Mathf.Clamp01(elapsedInMonth / Mathf.Max(monthDurationSeconds, 0.0001f));

        /// <summary>月齢が進んだときに呼ばれる(引数: 消費した食料量、進行後の月齢)。</summary>
        public event Action<int, int> OnMonthAdvanced;

        /// <summary>食料不足で進行がブロックされたときに呼ばれる(引数: 消費量合計、現在の食料ストック)。</summary>
        public event Action<int, int> OnMonthBlocked;

        /// <summary>ブロックが解除されたときに呼ばれる。</summary>
        public event Action OnMonthUnblocked;

        private void OnEnable()
        {
            if (slimeRoster != null)
            {
                slimeRoster.OnRosterChanged += HandleConditionsMayHaveChanged;
            }

            if (foodStockManager != null)
            {
                foodStockManager.OnFoodChanged += HandleFoodChanged;
            }
        }

        private void OnDisable()
        {
            if (slimeRoster != null)
            {
                slimeRoster.OnRosterChanged -= HandleConditionsMayHaveChanged;
            }

            if (foodStockManager != null)
            {
                foodStockManager.OnFoodChanged -= HandleFoodChanged;
            }
        }

        private void Update()
        {
            if (IsBlocked)
            {
                return;
            }

            elapsedInMonth += Time.deltaTime;
            if (elapsedInMonth >= monthDurationSeconds)
            {
                elapsedInMonth -= monthDurationSeconds;
                AdvanceMonthNow();
            }
        }

        /// <summary>
        /// 月齢を1つ進める。消費量合計を算出し、ストックが足りればまとめて差し引いて進行し、
        /// 足りなければ何も消費せずブロック状態にする。タイマー以外(操作数トリガー等)からも呼べる。
        /// </summary>
        public void AdvanceMonthNow()
        {
            int totalConsumption = slimeRoster != null ? slimeRoster.CalculateTotalMonthlyConsumption() : 0;

            if (foodStockManager == null || !foodStockManager.TryConsume(totalConsumption))
            {
                IsBlocked = true;
                int currentFood = foodStockManager != null ? foodStockManager.CurrentFood : 0;
                OnMonthBlocked?.Invoke(totalConsumption, currentFood);
                return;
            }

            CurrentMonth++;
            OnMonthAdvanced?.Invoke(totalConsumption, CurrentMonth);
        }

        private void HandleConditionsMayHaveChanged()
        {
            RecheckBlock();
        }

        private void HandleFoodChanged(int _)
        {
            RecheckBlock();
        }

        /// <summary>
        /// ブロック中に消費量合計がストック以下に戻っていないかを再確認し、
        /// 満たしていればブロックを解除して次の月から進行を再開する。
        /// </summary>
        public void RecheckBlock()
        {
            if (!IsBlocked)
            {
                return;
            }

            int totalConsumption = slimeRoster != null ? slimeRoster.CalculateTotalMonthlyConsumption() : 0;
            int currentFood = foodStockManager != null ? foodStockManager.CurrentFood : 0;

            if (totalConsumption > currentFood)
            {
                return;
            }

            IsBlocked = false;
            elapsedInMonth = 0f;
            OnMonthUnblocked?.Invoke();
        }
    }
}
