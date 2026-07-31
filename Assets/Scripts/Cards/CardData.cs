using UnityEngine;

namespace Slimelands.Cards
{
    /// <summary>
    /// カードが表すデータの抽象基底。資源・スライム・建物などはサブクラスで表現する。
    /// 個体差のある可変データ(スライムのレベル等)は持たない。カードは種類を表すマスターデータの参照。
    /// </summary>
    public abstract class CardData : ScriptableObject
    {
        [SerializeField] private string cardName;
        [SerializeField] private Sprite icon;
        [SerializeField, TextArea] private string description;

        public string CardName => cardName;
        public Sprite Icon => icon;
        public string Description => description;

        public abstract CardCategory Category { get; }
    }
}
