using System.Collections.Generic;
using UnityEngine;

namespace Slimelands.Cards
{
    /// <summary>
    /// 盤面の同じ場所に積まれたカードの山(Stacklands風のスタック)。下から上へ順にカードを保持する。
    /// 任意の位置から上を一括で持ち上げて新しいスタックとして切り離せる(RemoveFrom)。
    /// 中身が0枚になると自身を破棄する。
    /// </summary>
    public class CardStack : MonoBehaviour
    {
        // 縦方向のみにずらして積む(Stacklands風)。後から乗せたカード(=前面)ほど下にずれ、
        // 手前のカードの上端から、後ろのカードの上端(名前部分)が少し覗く見た目になる。
        private static readonly Vector3 CardOffset = new Vector3(0f, -0.18f, 0f);

        private readonly List<CardController> cards = new();

        private Vector3 dragOffset;

        public int Count => cards.Count;
        public IReadOnlyList<CardController> Cards => cards;

        public static CardStack Create(Transform parent, Vector3 position)
        {
            var go = new GameObject("Stack");
            go.transform.SetParent(parent);
            go.transform.position = position;
            return go.AddComponent<CardStack>();
        }

        private void Awake()
        {
            // cards/ParentStackはUnityにシリアライズされないランタイム状態のため、
            // シーンロードやPlayモード開始のたびにTransform階層(子=カード、下から上の順)から復元する。
            cards.Clear();
            for (int i = 0; i < transform.childCount; i++)
            {
                var card = transform.GetChild(i).GetComponent<CardController>();
                if (card != null)
                {
                    cards.Add(card);
                    card.SetParentStack(this);
                }
            }
        }

        public CardController PeekTop()
        {
            return cards.Count > 0 ? cards[cards.Count - 1] : null;
        }

        public int IndexOf(CardController card)
        {
            return cards.IndexOf(card);
        }

        public void AddToTop(CardController card)
        {
            AddRangeToTop(new[] { card });
        }

        public void AddRangeToTop(IReadOnlyList<CardController> cardsBottomToTop)
        {
            foreach (CardController card in cardsBottomToTop)
            {
                cards.Add(card);
                card.transform.SetParent(transform);
                card.transform.localPosition = CardOffset * (cards.Count - 1);
                card.SetParentStack(this);
            }
        }

        /// <summary>
        /// 位置に関わらず特定の1枚を取り除く(合成完了で特定の材料カードを消費する用途)。
        /// 取り除いた位置より上にあったカードは、隙間を詰めるようずれ幅を再計算する。
        /// </summary>
        public bool Remove(CardController card)
        {
            int index = cards.IndexOf(card);
            if (index < 0)
            {
                return false;
            }

            cards.RemoveAt(index);
            card.transform.SetParent(null, true);
            card.SetParentStack(null);

            for (int i = index; i < cards.Count; i++)
            {
                cards[i].transform.localPosition = CardOffset * i;
            }

            if (cards.Count == 0)
            {
                Destroy(gameObject);
            }

            return true;
        }

        /// <summary>
        /// index位置のカードから一番上までを、相対的な重なり順(ずれ幅)を保ったまま切り離し、
        /// 新しいCardStack(持ち上げたグループ)として返す。index未満のカードは自分自身に残る。
        /// </summary>
        public CardStack RemoveFrom(int index)
        {
            CardCraftProcess activeCraft = GetComponent<CardCraftProcess>();
            // スタック全体をまとめて持ち上げる場合(index==0)は合成を中断せず新しいスタックへ引き継ぐ。
            // 途中のカードだけを持ち上げる場合は素材が分断されてしまうため中断する。
            bool willLiftEntireStack = index == 0;
            if (activeCraft != null && !willLiftEntireStack)
            {
                activeCraft.Cancel();
                activeCraft = null;
            }

            int liftCount = cards.Count - index;
            List<CardController> lifted = cards.GetRange(index, liftCount);
            cards.RemoveRange(index, liftCount);

            CardStack newStack = Create(transform.parent, lifted[0].transform.position);
            newStack.AddRangeToTop(lifted);

            if (activeCraft != null)
            {
                activeCraft.TransferTo(newStack);
            }

            if (cards.Count == 0)
            {
                Destroy(gameObject);
            }

            return newStack;
        }

        /// <summary>保持している全カードを下から上の順で切り離して返す。自分自身は破棄される。</summary>
        public List<CardController> TakeAll()
        {
            List<CardController> all = new List<CardController>(cards);
            cards.Clear();

            foreach (CardController card in all)
            {
                card.transform.SetParent(null, true);
                card.SetParentStack(null);
            }

            Destroy(gameObject);
            return all;
        }

        /// <summary>現在の並び順を保ったまま、全カードのローカル位置をCardOffsetに基づき再計算する。</summary>
        public void RelayoutAll()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                cards[i].transform.localPosition = CardOffset * i;
            }
        }

        public void BeginDrag(Vector3 pointerWorldPosition)
        {
            dragOffset = transform.position - pointerWorldPosition;
        }

        public void UpdateDrag(Vector3 pointerWorldPosition)
        {
            transform.position = pointerWorldPosition + dragOffset;
        }
    }
}
