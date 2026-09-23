using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// EnemyType Id→한국어 표시명(Docs/설계/18번 §5.2) - EnemyStatsTableAsset에서 분리된 문자열
    /// 전용 테이블. EnemyStatsTableImporter가 채운다. 현재 이 값을 읽는 소비자는 없다(설계 18번 §6 -
    /// 데이터만 준비, 향후 UI가 필요해지면 그때 배선).
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyStringsTable", menuName = "Game/Table/Enemy Strings Table")]
    public class EnemyStringsTableAsset : ScriptableObject
    {
        [SerializeField] private List<SlugLocalizedStringEntry> strings = new();

        public bool TryGetLabel(string enemyType, out string ko)
        {
            if (TableEntryLookup.TryFind(strings, enemyType, e => e.Id, out var entry))
            {
                ko = entry.Ko;
                return true;
            }

            ko = null;
            return false;
        }
    }
}
