using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    [Serializable]
    public struct EnemyStatsEntry
    {
        public EnemyType EnemyType;
        public float MaxHp;
        public float Attack;
        public float Defense;
        public float MoveSpeed;
        public float AttackInterval;
        public float Range;
        public float MoraleSyncRate;
        public float HpRegenPerSecond;
    }

    /// <summary>
    /// 적 타입별 스탯 - PlaceholderEnemyTypeCompositionProvider의 StatsByType Dictionary를 대체하는
    /// 실제 데이터(Docs/설계/17번 §3). EnemyEncounterCompositionTableAsset(인원수 구성)과 별도 에셋으로
    /// 분리한 이유는 그 에셋 요약 주석 참고. 표시명은 EnemyStringsTableAsset으로 분리(Docs/설계/18번 §6).
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyStatsTable", menuName = "Game/Table/Enemy Stats Table")]
    public class EnemyStatsTableAsset : ScriptableObject
    {
        [SerializeField] private List<EnemyStatsEntry> entries = new();

        public bool TryGetEntry(EnemyType enemyType, out EnemyStatsEntry entry)
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

        public IReadOnlyList<EnemyStatsEntry> Entries => entries;
    }
}
