using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 아이템 표시 문자열(이름·설명) 테이블 - ItemDefinitionTableAsset과 같은 이유로 카테고리들이 클래스 하나를
    /// 공유한다(Docs/설계/35번 §4). 편집은 공용 스트링 테이블 한 곳에서 하고, 임포터가 카테고리별 자산으로 나눠
    /// 기록한다(설계 50번 §4.1).
    /// </summary>
    [CreateAssetMenu(fileName = "ItemStringTable", menuName = "Game/Table/Item String Table")]
    public class ItemStringTableAsset : ScriptableObject
    {
        [SerializeField] private List<ItemStringEntry> strings = new();

        private Dictionary<string, ItemStringEntry> lookup;

        public bool TryGetStrings(string id, out ItemStringEntry entry)
        {
            EnsureLookupBuilt();
            return lookup.TryGetValue(id, out entry);
        }

        private void EnsureLookupBuilt()
        {
            if (lookup != null) return;

            lookup = new Dictionary<string, ItemStringEntry>(strings.Count);
            foreach (var entry in strings)
            {
                lookup[entry.Id] = entry;
            }
        }
    }
}
