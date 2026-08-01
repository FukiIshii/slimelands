using System;
using System.Collections.Generic;
using UnityEngine;
using Slimelands.Slime;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// attacker→defenderの組み合わせ1件の相性。attackerがdefenderに対して有利な場合のみ登録する
    /// (不利側の減算は今回未対応。片方向の優位ボーナスのみで表現する)。
    /// </summary>
    [Serializable]
    public struct TypeAffinityEntry
    {
        public SlimeType attacker;
        public SlimeType defender;
        [Tooltip("attackerがdefenderを攻撃するときのダメージ倍率(有利な組み合わせのみ登録する想定)。")]
        public float multiplier;
    }

    /// <summary>
    /// タイプ(属性)相性表。Inspector上でattacker/defenderの組み合わせと倍率を編集できる。
    /// GDD上「タイプ相性が戦闘に絡むか」は本実装により解決済みの仕様として扱う。
    /// </summary>
    [CreateAssetMenu(fileName = "NewTypeAffinity", menuName = "Slimelands/Dungeon/Type Affinity Data", order = 60)]
    public class TypeAffinityData : ScriptableObject
    {
        [SerializeField] private List<TypeAffinityEntry> entries = new();

        public IReadOnlyList<TypeAffinityEntry> Entries => entries;

        /// <summary>
        /// attackerがdefenderを攻撃するときのダメージ倍率を返す。登録がなければ1.0(補正なし)。
        /// 無属性(Neutral)が絡む組み合わせは常に1.0(相性なし)。
        /// </summary>
        public float GetMultiplier(SlimeType attacker, SlimeType defender)
        {
            if (attacker == SlimeType.Neutral || defender == SlimeType.Neutral)
            {
                return 1f;
            }

            foreach (TypeAffinityEntry entry in entries)
            {
                if (entry.attacker == attacker && entry.defender == defender)
                {
                    return entry.multiplier;
                }
            }

            return 1f;
        }
    }
}
