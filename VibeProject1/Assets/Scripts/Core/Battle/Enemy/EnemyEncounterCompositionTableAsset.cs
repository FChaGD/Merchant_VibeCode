using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    [Serializable]
    public struct EnemyEncounterCompositionEntry
    {
        public EnemyType EnemyType;
        // 기획 14번 §2 확정 - 포함 상한(Min~Max 둘 다 실제 등장 가능한 값). Random.Range(min, max)는
        // max가 배타적이라, 호출부(TableEnemyTypeCompositionProvider)가 CountMax+1로 보정해서 넘긴다.
        public int CountMin;
        public int CountMax;
    }

    /// <summary>
    /// 적 타입별 인카운터 인원수 구성 - PlaceholderEnemyTypeCompositionProvider의 CountRangeByType
    /// Dictionary를 대체한다. EnemyStatsTableAsset(개체 고유 스탯)과 별도 에셋으로 분리한 이유는
    /// 기획 08번 §14.7이 예고한 "두목급 변형·난이도 스케일링" 확장 시 구성 규칙만 타입당 1:N으로
    /// 늘어날 여지가 있어서다(Docs/기획/14번 §3.3, Docs/설계/17번 §3).
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyEncounterCompositionTable", menuName = "Game/Table/Enemy Encounter Composition Table")]
    public class EnemyEncounterCompositionTableAsset : ScriptableObject
    {
        [SerializeField] private List<EnemyEncounterCompositionEntry> entries = new();

        public bool TryGetEntry(EnemyType enemyType, out EnemyEncounterCompositionEntry entry)
        {
            foreach (var candidate in entries)
            {
                if (candidate.EnemyType == enemyType)
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = default;
            return false;
        }

        public IReadOnlyList<EnemyEncounterCompositionEntry> Entries => entries;
    }
}
