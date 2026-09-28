using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 직업 Id → 한국어 직업명(Docs/설계/54번 §2.2). 캐릭터 이름과 직업명은 다른 대상이라 CharacterStringsTableAsset에서
    /// 분리했다. CharacterStatsTableImporter가 같은 워크북의 MercenaryClassStrings 시트에서 채운다.
    /// </summary>
    [CreateAssetMenu(fileName = "MercenaryClassStringsTable", menuName = "Game/Table/Mercenary Class Strings Table")]
    public class MercenaryClassStringsTableAsset : ScriptableObject
    {
        [SerializeField] private List<SlugLocalizedStringEntry> strings = new();

        public bool TryGetLabel(string mercenaryClass, out string ko)
        {
            if (TableEntryLookup.TryFind(strings, mercenaryClass, e => e.Id, out var entry))
            {
                ko = entry.Ko;
                return true;
            }

            ko = null;
            return false;
        }
    }
}
