using System;

namespace Slimelands.Slime
{
    /// <summary>
    /// HP・攻撃力などスライムの基本ステータス一式。
    /// </summary>
    [Serializable]
    public struct SlimeStats
    {
        public int maxHp;
        public int attack;
        public int defense;
        public int speed;

        public static SlimeStats operator +(SlimeStats a, SlimeStats b)
        {
            return new SlimeStats
            {
                maxHp = a.maxHp + b.maxHp,
                attack = a.attack + b.attack,
                defense = a.defense + b.defense,
                speed = a.speed + b.speed,
            };
        }

        public static SlimeStats operator *(SlimeStats a, int scalar)
        {
            return new SlimeStats
            {
                maxHp = a.maxHp * scalar,
                attack = a.attack * scalar,
                defense = a.defense * scalar,
                speed = a.speed * scalar,
            };
        }
    }
}
