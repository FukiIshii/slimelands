using System;
using UnityEngine;
using Slimelands.Cards;

namespace Slimelands.Village
{
    /// <summary>
    /// スライムガチャ(召喚)。SlimeSpeciesData.GachaWeightに基づく重み付き抽選で1体を選び、
    /// 既存のCardController/CardData/CardBoardの仕組みでスライムカードを盤面に生成する。
    /// コスト消費は「盤面上に置かれた資源カードを実際に取り除く」方式(既存のカード経済と一貫させるため)。
    /// </summary>
    public class GachaManager : MonoBehaviour
    {
        [SerializeField] private CardBoard cardBoard;
        [SerializeField] private GachaPoolData gachaPool;
        [Tooltip("生成したスライムカードを配置する盤面上の座標。")]
        [SerializeField] private Vector3 spawnPosition = new Vector3(0f, 4f, 0f);

        [Header("消費コスト(仮の値。バランスは未調整)")]
        [Tooltip("ガチャ1回につき消費する資源カード。nullなら無償。")]
        [SerializeField] private ResourceCardData costResource;
        [SerializeField] private int costAmount = 10;

        /// <summary>ガチャ1回で消費する資源カードの種類(UI側でのコスト表示に使う)。nullなら無償。</summary>
        public ResourceCardData CostResource => costResource;

        /// <summary>ガチャ1回で消費する資源カードの枚数(UI側でのコスト表示に使う)。</summary>
        public int CostAmount => costAmount;

        /// <summary>ガチャの結果、実際に排出されたスライムカードを通知する。</summary>
        public event Action<SlimeCardData> OnGachaDrawn;

        /// <summary>コスト不足などでガチャが実行できなかったときの理由を通知する。</summary>
        public event Action<string> OnGachaFailed;

        /// <summary>
        /// ガチャを1回実行する。コストが払えた場合のみ抽選・カード生成を行いtrueを返す。
        /// </summary>
        public bool TryDrawGacha()
        {
            if (cardBoard == null)
            {
                OnGachaFailed?.Invoke("CardBoardが設定されていません");
                return false;
            }

            if (gachaPool == null || gachaPool.Entries.Count == 0)
            {
                OnGachaFailed?.Invoke("ガチャの排出対象が設定されていません");
                return false;
            }

            if (costResource != null && costAmount > 0 && !cardBoard.TryConsumeCards(costResource, costAmount))
            {
                OnGachaFailed?.Invoke($"{costResource.CardName}が{costAmount}枚足りません");
                return false;
            }

            SlimeCardData picked = gachaPool.PickWeighted();
            if (picked == null)
            {
                OnGachaFailed?.Invoke("排出対象の重みが全て0以下です");
                return false;
            }

            cardBoard.SpawnStack(picked, spawnPosition);
            OnGachaDrawn?.Invoke(picked);
            return true;
        }
    }
}
