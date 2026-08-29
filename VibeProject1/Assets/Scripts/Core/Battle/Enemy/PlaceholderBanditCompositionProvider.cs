using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 실제 인카운터 콘텐츠 시스템 설계 후 대체/제거 대상 - Docs/기획/08_전투_해석로직_기획.md §13,
    /// 도적/산적 단일 유형을 인카운터당 8~10마리 무작위로 제공한다.
    /// </summary>
    public class PlaceholderBanditCompositionProvider : IEnemyCompositionProvider
    {
        private static readonly BattleUnitStats BanditStats =
            new(maxHp: 60, attack: 12, defense: 5, moveSpeed: 3.0f, attackInterval: 1.0f, range: 1.5f);

        public IReadOnlyList<BattleUnitStats> GetEncounterComposition()
        {
            var count = Random.Range(8, 11); // 8~10 (Range의 max는 배타적)
            var result = new List<BattleUnitStats>(count);
            for (var i = 0; i < count; i++)
            {
                result.Add(BanditStats);
            }
            return result;
        }
    }
}
