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
        [Tooltip("カード表示の背景に敷くフレーム画像(任意)。未設定ならフレームなしでicon(前景)のみ描画する" +
            "(既存カードとの後方互換のため)。")]
        [SerializeField] private Sprite frame;
        [SerializeField, TextArea] private string description;

        public string CardName => cardName;
        public Sprite Icon => icon;
        public Sprite Frame => frame;
        public string Description => description;

        public abstract CardCategory Category { get; }
    }
}
