using System;
using UnityEngine;

namespace Slimelands.Slime
{
    /// <summary>
    /// 才能値・個体値のような、同じ種族でも個体差を生む隠しパラメータ。
    /// 範囲(MinValue/MaxValue)はGDD上「今後詰める論点」として未決定のため仮値。
    /// バランス確定時はここだけ調整すればよいようにまとめてある。
    /// </summary>
    [Serializable]
    public struct SlimeIndividualValues
    {
        public const int MinValue = 0;
        public const int MaxValue = 31; // 仮値。要調整。

        [Range(MinValue, MaxValue)] public int hp;
        [Range(MinValue, MaxValue)] public int attack;
        [Range(MinValue, MaxValue)] public int defense;
        [Range(MinValue, MaxValue)] public int speed;

        public static SlimeIndividualValues CreateRandom()
        {
            return new SlimeIndividualValues
            {
                hp = UnityEngine.Random.Range(MinValue, MaxValue + 1),
                attack = UnityEngine.Random.Range(MinValue, MaxValue + 1),
                defense = UnityEngine.Random.Range(MinValue, MaxValue + 1),
                speed = UnityEngine.Random.Range(MinValue, MaxValue + 1),
            };
        }
    }
}
