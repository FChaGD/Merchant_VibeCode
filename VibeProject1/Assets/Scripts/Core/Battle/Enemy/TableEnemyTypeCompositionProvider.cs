using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// EnemyStatsTableAsset/EnemyEncounterCompositionTableAsset(엑셀 임포트 결과)을
    /// IEnemyCompositionProvider로 노출한다 - PlaceholderEnemyTypeCompositionProvider 대체
    /// (Docs/설계/17번 §5). 3타입 균등 무작위 선택 로직(기획 08번 §13.1)은 그대로 옮겼다 - 이번
    /// 변경은 값의 출처만 바꾼다. GetStatsForType은 과거 정적 헬퍼(배틀 테스트 씬이 별도 스탯을
    /// 복제하지 않도록 하려는 목적, PlaceholderEnemyTypeCompositionProvider 참고)와 같은 목적이지만,
    /// 이제 테이블 에셋 참조가 인스펙터 배선이라 정적일 수 없어 인스턴스 메서드로 노출한다.
    /// </summary>
    public class TableEnemyTypeCompositionProvider : IEnemyCompositionProvider
    {
        // Random.Range의 max는 배타적 - 기획 14번 §2가 CountMax를 포함 상한으로 정했으므로 +1 보정.
        private static readonly EnemyType[] AllTypes = { EnemyType.Marauder, EnemyType.Monster, EnemyType.Adversary };

        private readonly EnemyStatsTableAsset statsTable;
        private readonly EnemyEncounterCompositionTableAsset compositionTable;

        public TableEnemyTypeCompositionProvider(EnemyStatsTableAsset statsTable, EnemyEncounterCompositionTableAsset compositionTable)
        {
            this.statsTable = statsTable;
            this.compositionTable = compositionTable;
        }

        public IReadOnlyList<BattleUnitStats> GetEncounterComposition()
        {
            var type = AllTypes[UnityEngine.Random.Range(0, AllTypes.Length)]; // 3개 타입 균등 무작위 - 기획 08번 §13.1 확정
            var stats = GetStatsForType(type);

            if (!compositionTable.TryGetEntry(type, out var composition))
            {
                throw new InvalidOperationException($"{nameof(EnemyEncounterCompositionTableAsset)}에 '{type}' 항목이 없다 - Tools/Game/Table/Import Character Stats를 실행했는지 확인.");
            }

            var count = UnityEngine.Random.Range(composition.CountMin, composition.CountMax + 1);

            var result = new List<BattleUnitStats>(count);
            for (var i = 0; i < count; i++)
            {
                result.Add(stats);
            }
            return result;
        }

        public BattleUnitStats GetStatsForType(EnemyType type)
        {
            if (statsTable == null || !statsTable.TryGetEntry(type, out var entry))
            {
                throw new InvalidOperationException($"{nameof(EnemyStatsTableAsset)}에 '{type}' 항목이 없다 - Tools/Game/Table/Import Character Stats를 실행했는지 확인.");
            }

            return new BattleUnitStats(entry.MaxHp, entry.Attack, entry.Defense, entry.MoveSpeed, entry.AttackInterval, entry.Range, entry.MoraleSyncRate, entry.HpRegenPerSecond, type);
        }
    }
}
