using System;
using System.Collections.Generic;
using UnityEngine;

namespace Slimelands.Dungeon
{
    /// <summary>
    /// ダンジョンツリー(木構造)での解放・クリア状態を一元管理する。DungeonRunController.OnDungeonCleared
    /// を購読してクリア済みダンジョンを記録するだけの、見た目に依存しないロジック層。
    /// 「全ダンジョンのマスターリスト」はPartyFormationUI.availableDungeonsをそのまま流用する
    /// (別の場所に重複したリストを持たせない)。
    /// </summary>
    public class DungeonProgressManager : MonoBehaviour
    {
        [SerializeField] private DungeonRunController dungeonController;
        [SerializeField] private PartyFormationUI partyFormationUI;

        private readonly HashSet<DungeonData> clearedDungeons = new();

        /// <summary>クリア状態が変化した(新たにダンジョンをクリアした)瞬間に発火する。ツリー画面の再描画に使う。</summary>
        public event Action OnProgressChanged;

        /// <summary>マスターリスト(PartyFormationUI.availableDungeons)。</summary>
        public IReadOnlyList<DungeonData> AllDungeons =>
            partyFormationUI != null ? partyFormationUI.AvailableDungeons : Array.Empty<DungeonData>();

        private void OnEnable()
        {
            if (dungeonController != null)
            {
                dungeonController.OnDungeonCleared += HandleDungeonCleared;
            }
        }

        private void OnDisable()
        {
            if (dungeonController != null)
            {
                dungeonController.OnDungeonCleared -= HandleDungeonCleared;
            }
        }

        private void HandleDungeonCleared()
        {
            DungeonData cleared = dungeonController != null ? dungeonController.CurrentDungeon : null;
            if (cleared == null)
            {
                return;
            }

            if (clearedDungeons.Add(cleared))
            {
                OnProgressChanged?.Invoke();
            }
        }

        public bool IsCleared(DungeonData dungeon)
        {
            return dungeon != null && clearedDungeons.Contains(dungeon);
        }

        /// <summary>親ダンジョンを持たない(=初期解放)か、全ての親をクリア済みなら解放されている。</summary>
        public bool IsUnlocked(DungeonData dungeon)
        {
            if (dungeon == null)
            {
                return false;
            }

            IReadOnlyList<DungeonData> parents = dungeon.ParentDungeons;
            if (parents == null || parents.Count == 0)
            {
                return true;
            }

            foreach (DungeonData parent in parents)
            {
                if (parent != null && !IsCleared(parent))
                {
                    return false;
                }
            }

            return true;
        }

        public DungeonNodeState GetState(DungeonData dungeon)
        {
            if (IsCleared(dungeon))
            {
                return DungeonNodeState.Cleared;
            }

            return IsUnlocked(dungeon) ? DungeonNodeState.UnlockedNotCleared : DungeonNodeState.Locked;
        }

        /// <summary>
        /// ツリー表示用の列番号(起点=0、子は親より必ず右側)。親が複数ある場合は最も深い親+1を採用する。
        /// 循環参照(データ不備)に対しては無限ループを避けるため探索済み集合でガードする。
        /// </summary>
        public int GetDepth(DungeonData dungeon)
        {
            return GetDepth(dungeon, new HashSet<DungeonData>());
        }

        private int GetDepth(DungeonData dungeon, HashSet<DungeonData> visiting)
        {
            if (dungeon == null || dungeon.ParentDungeons == null || dungeon.ParentDungeons.Count == 0)
            {
                return 0;
            }

            if (!visiting.Add(dungeon))
            {
                // 循環参照を検知した場合はこれ以上深追いしない(データ不備からの防御的フォールバック)。
                return 0;
            }

            int maxParentDepth = -1;
            foreach (DungeonData parent in dungeon.ParentDungeons)
            {
                if (parent == null)
                {
                    continue;
                }

                int parentDepth = GetDepth(parent, visiting);
                if (parentDepth > maxParentDepth)
                {
                    maxParentDepth = parentDepth;
                }
            }

            visiting.Remove(dungeon);
            return maxParentDepth + 1;
        }
    }
}
