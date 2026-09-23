using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 아이템 표시명(Id→Ko) String 테이블 - ItemDefinitionTableAsset과 같은 이유로 카테고리 4종이
    /// 클래스 하나를 공유한다(Docs/설계/35번 §4). SlugLocalizedStringEntry는 36번 문서(기존 테이블
    /// enum→슬러그 전환) 작업에서 먼저 만들어진 공용 구조체를 그대로 재사용한다.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemStringTable", menuName = "Game/Table/Item String Table")]
    public class ItemStringTableAsset : ScriptableObject
    {
        [SerializeField] private List<SlugLocalizedStringEntry> strings = new();

        private Dictionary<string, string> lookup;

        public bool TryGetLabel(string id, out string ko)
        {
            EnsureLookupBuilt();
            return lookup.TryGetValue(id, out ko);
        }

        private void EnsureLookupBuilt()
        {
            if (lookup != null) return;

            lookup = new Dictionary<string, string>(strings.Count);
            foreach (var entry in strings)
            {
                lookup[entry.Id] = entry.Ko;
            }
        }
    }
}
