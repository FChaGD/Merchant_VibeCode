using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 아이템 카테고리 4종이 공유하는 Data 테이블 자산(Docs/설계/35번 §4) - 카테고리별로 자산
    /// 인스턴스만 따로 두고(경로가 다름) 클래스는 하나다. 아이템은 수십~수백 개로 늘어날 콘텐츠라
    /// TableEntryLookup(선형 탐색)을 쓰지 않고 Dictionary로 O(1) 조회한다(CLAUDE.md 최적화 규칙) -
    /// 최초 조회 시 1회만 구축하고 이후 재사용한다.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDefinitionTable", menuName = "Game/Table/Item Definition Table")]
    public class ItemDefinitionTableAsset : ScriptableObject
    {
        [SerializeField] private List<ItemDefinitionEntry> entries = new();

        private Dictionary<string, ItemDefinitionEntry> lookup;

        public IReadOnlyList<ItemDefinitionEntry> Entries => entries;

        public bool TryGetEntry(string id, out ItemDefinitionEntry entry)
        {
            EnsureLookupBuilt();
            return lookup.TryGetValue(id, out entry);
        }

        private void EnsureLookupBuilt()
        {
            if (lookup != null) return;

            lookup = new Dictionary<string, ItemDefinitionEntry>(entries.Count);
            foreach (var entry in entries)
            {
                lookup[entry.Id] = entry;
            }
        }
    }
}
