using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 마차·시설 개체 Id → 한국어 이름(Docs/설계/56번 §2.2). CaravanAssetTableAsset과 같이 마차·시설이 클래스를 공유한다.
    /// </summary>
    [CreateAssetMenu(fileName = "CaravanAssetStrings", menuName = "Game/Table/Caravan Asset Strings Table")]
    public class CaravanAssetStringsTableAsset : ScriptableObject
    {
        [SerializeField] private List<SlugLocalizedStringEntry> strings = new();

        public bool TryGetLabel(string id, out string ko)
        {
            if (TableEntryLookup.TryFind(strings, id, e => e.Id, out var entry))
            {
                ko = entry.Ko;
                return true;
            }

            ko = null;
            return false;
        }
    }
}
