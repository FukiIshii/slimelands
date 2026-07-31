using UnityEngine;

namespace Slimelands.Cards
{
    /// <summary>
    /// 盤面に置かれた1枚のカードの見た目を管理する。
    /// ドラッグ・重なりの管理はCardStack、入力の受付・合成判定はCardBoardが担う。
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Collider2D))]
    public class CardController : MonoBehaviour
    {
        [SerializeField] private CardData data;

        private SpriteRenderer cachedSpriteRenderer;

        public CardData Data => data;
        public CardStack ParentStack { get; private set; }

        public SpriteRenderer SpriteRenderer =>
            cachedSpriteRenderer != null ? cachedSpriteRenderer : (cachedSpriteRenderer = GetComponent<SpriteRenderer>());

        public void Initialize(CardData cardData)
        {
            data = cardData;
            SpriteRenderer.sprite = data != null ? data.Icon : null;
            name = data != null ? $"Card_{data.CardName}" : "Card";
        }

        public void SetSortingOrder(int order)
        {
            SpriteRenderer.sortingOrder = order;
        }

        public void SetParentStack(CardStack stack)
        {
            ParentStack = stack;
        }
    }
}
