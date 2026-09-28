using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 캐릭터 Id → 한국어 이름(Docs/설계/54번 §2.2). 행 단위가 직업에서 캐릭터로 바뀌면서 직업명은
    /// MercenaryClassStringsTableAsset으로 옮겨 갔다. CharacterStatsTableImporter가 채운다.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterStringsTable", menuName = "Game/Table/Character Strings Table")]
    public class CharacterStringsTableAsset : ScriptableObject
    {
        [SerializeField] private List<SlugLocalizedStringEntry> strings = new();

        public bool TryGetLabel(string characterId, out string ko)
        {
            if (TableEntryLookup.TryFind(strings, characterId, e => e.Id, out var entry))
            {
                ko = entry.Ko;
                return true;
            }

            ko = null;
            return false;
        }
    }
}
