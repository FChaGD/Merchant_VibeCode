using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 기획 08번 문서 §13.1 - 3개 적 타입(약탈자/괴수/적대자) 중 하나를 균등 무작위로 골라 그 타입
    /// 단일 구성으로 제공한다(인카운터당 혼합 없음). 두목급 변형·타입 선택 가중치·난이도 스케일링
    /// (§14.7 잔여 미정)을 다루는 실제 인카운터 콘텐츠 시스템이 설계되면 대체/제거 대상.
    /// </summary>
    public class PlaceholderEnemyTypeCompositionProvider : IEnemyCompositionProvider
    {
        private static readonly Dictionary<EnemyType, BattleUnitStats> StatsByType = new()
        {
            // moraleSyncRate는 기획 08번 §7.4 표 - 약탈자(빠른 붕괴) 7.5, 괴수(표준) 5, 적대자(느린 붕괴) 2.5.
            [EnemyType.Marauder] = new BattleUnitStats(maxHp: 60, attack: 12, defense: 5, moveSpeed: 3.0f, attackInterval: 1.0f, range: 1.5f, moraleSyncRate: 7.5f, enemyType: EnemyType.Marauder),
            [EnemyType.Monster] = new BattleUnitStats(maxHp: 90, attack: 12, defense: 6, moveSpeed: 4.0f, attackInterval: 1.0f, range: 1.5f, moraleSyncRate: 5f, hpRegenPerSecond: 3f, enemyType: EnemyType.Monster),
            [EnemyType.Adversary] = new BattleUnitStats(maxHp: 50, attack: 20, defense: 4, moveSpeed: 3.5f, attackInterval: 1.2f, range: 1.5f, moraleSyncRate: 2.5f, enemyType: EnemyType.Adversary),
        };

        // Random.Range의 max는 배타적 - (3,6)=3~5, (2,5)=2~4, (1,3)=1~2 (기획 08번 §13.1 표 그대로).
        private static readonly Dictionary<EnemyType, Vector2Int> CountRangeByType = new()
        {
            [EnemyType.Marauder] = new Vector2Int(3, 6),
            [EnemyType.Monster] = new Vector2Int(2, 5),
            [EnemyType.Adversary] = new Vector2Int(1, 3),
        };

        public IReadOnlyList<BattleUnitStats> GetEncounterComposition()
        {
            var type = (EnemyType)Random.Range(0, 3); // 3개 타입 균등 무작위 - §13.1 확정
            var stats = StatsByType[type];
            var countRange = CountRangeByType[type];
            var count = Random.Range(countRange.x, countRange.y);

            var result = new List<BattleUnitStats>(count);
            for (var i = 0; i < count; i++)
            {
                result.Add(stats);
            }
            return result;
        }
    }
}
