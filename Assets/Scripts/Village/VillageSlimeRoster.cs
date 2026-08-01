using System;
using System.Collections.Generic;
using UnityEngine;
using Slimelands.Cards;
using Slimelands.Slime;

namespace Slimelands.Village
{
    /// <summary>
    /// 村に在籍している全スライム(SlimeIndividual)の名簿。
    /// 月齢消費量の合計算出と、スライムを売って資源に変換する処理を担う。
    /// カード盤面(CardBoard)とは独立したデータとして持つ
    /// (現状のSlimeCardDataは種族選択のみで個体状態を持たないため)。
    /// </summary>
    public class VillageSlimeRoster : MonoBehaviour
    {
        [Tooltip("売却で得た資源カードをこのCardBoardの盤面に出現させる。未設定なら生成せずイベント通知のみ行う。")]
        [SerializeField] private CardBoard cardBoard;
        [Tooltip("売却で得た資源カードを出現させるワールド座標。")]
        [SerializeField] private Vector3 sellSpawnPosition = Vector3.zero;

        private readonly List<SlimeIndividual> slimes = new();

        public IReadOnlyList<SlimeIndividual> Slimes => slimes;

        /// <summary>
        /// 次のダンジョンに編成可能な(HPが最大HPの50%以上に回復している)個体のみを返す。
        /// パーティー編成UIは未実装だが、実装時はここから選択候補を取得する想定。
        /// </summary>
        public IReadOnlyList<SlimeIndividual> GetPartyEligibleSlimes()
        {
            var eligible = new List<SlimeIndividual>();
            foreach (SlimeIndividual slime in slimes)
            {
                if (slime.CanJoinParty)
                {
                    eligible.Add(slime);
                }
            }

            return eligible;
        }

        /// <summary>名簿にスライムが追加/削除されるたびに呼ばれる。</summary>
        public event Action OnRosterChanged;

        /// <summary>スライムが売却されたときに呼ばれる(売れたスライム, 得られた資源カード, 枚数)。</summary>
        public event Action<SlimeIndividual, CardData, int> OnSlimeSold;

        public void AddSlime(SlimeIndividual slime)
        {
            if (slime == null)
            {
                return;
            }

            slimes.Add(slime);
            OnRosterChanged?.Invoke();
        }

        /// <summary>
        /// 配合・進化などでカードごと消費された個体を、売却報酬なしで名簿から除く
        /// (TrySellとは異なり資源カードは生成しない)。
        /// </summary>
        public bool RemoveSlime(SlimeIndividual slime)
        {
            if (slime == null || !slimes.Remove(slime))
            {
                return false;
            }

            OnRosterChanged?.Invoke();
            return true;
        }

        /// <summary>在籍する全スライムの、種族ごとに固定された月齢消費量の合計。</summary>
        public int CalculateTotalMonthlyConsumption()
        {
            int total = 0;
            foreach (SlimeIndividual slime in slimes)
            {
                if (slime.Species != null)
                {
                    total += slime.Species.MonthlyFoodConsumption;
                }
            }

            return total;
        }

        /// <summary>
        /// 指定したスライムを売って名簿から除き、種族データの売却設定に応じた資源カードを
        /// (CardBoardが設定されていれば)盤面に出現させる。交換レートは種族ごとの仮の値。
        /// </summary>
        public bool TrySell(SlimeIndividual slime)
        {
            if (slime == null || !slimes.Remove(slime))
            {
                return false;
            }

            SlimeSpeciesData species = slime.Species;
            CardData sellResource = species != null ? species.SellResource : null;
            int sellAmount = species != null ? species.SellAmount : 0;

            if (sellResource != null && sellAmount > 0 && cardBoard != null)
            {
                for (int i = 0; i < sellAmount; i++)
                {
                    cardBoard.SpawnStack(sellResource, sellSpawnPosition);
                }
            }

            OnSlimeSold?.Invoke(slime, sellResource, sellAmount);
            OnRosterChanged?.Invoke();
            return true;
        }
    }
}
