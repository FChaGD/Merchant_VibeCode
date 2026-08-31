using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 실제 용병 밸런싱 시스템 설계 후 대체/제거 대상 - Docs/기획/08_전투_해석로직_기획.md §12 표를
    /// 그대로 반영한 테스트 수치다.
    /// </summary>
    public class PlaceholderBattleUnitStatProvider : IBattleUnitStatProvider
    {
        private static readonly Dictionary<MercenaryClass, BattleUnitStats> Table = new()
        {
            // 마지막 인자(moraleSyncRate)는 기획 08번 §7.4 표 - 방패병만 방어력과 결을 맞춰 낮게(3), 나머지는 5.
            [MercenaryClass.Warrior] = new BattleUnitStats(100, 20, 10, 3.0f, 1.0f, 1.5f, moraleSyncRate: 5f),
            [MercenaryClass.Archer] = new BattleUnitStats(70, 15, 5, 3.0f, 1.2f, 6.0f, moraleSyncRate: 5f),
            [MercenaryClass.ShieldBearer] = new BattleUnitStats(150, 8, 20, 2.5f, 1.5f, 1.2f, moraleSyncRate: 3f),
        };

        public BattleUnitStats GetStats(MercenaryClass mercenaryClass) => Table[mercenaryClass];
    }
}
