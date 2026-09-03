using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// MercenaryClass Id→한국어 표시명(Docs/설계/18번 §5.2) - CharacterStatsTableAsset에서 분리된
    /// 문자열 전용 테이블. CharacterStatsTableImporter가 채운다. 현재 이 값을 읽는 소비자는 없다
    /// (설계 18번 §6 - 데이터만 준비, 향후 로스터/부대원 표시 기능이 생기면 그때 배선).
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterStringsTable", menuName = "Game/Table/Character Strings Table")]
    public class CharacterStringsTableAsset : ScriptableObject
    {
        [SerializeField] private List<LocalizedStringEntry> strings = new();

        public bool TryGetLabel(MercenaryClass mercenaryClass, out string ko)
        {
            foreach (var entry in strings)
            {
                if (entry.Id == (int)mercenaryClass)
                {
                    ko = entry.Ko;
                    return true;
                }
            }

            ko = null;
            return false;
        }
    }
}
