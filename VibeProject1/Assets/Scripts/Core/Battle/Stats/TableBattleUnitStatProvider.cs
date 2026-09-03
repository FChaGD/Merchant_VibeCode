using System;

namespace Game.Core
{
    /// <summary>
    /// CharacterStatsTableAsset(엑셀 임포트 결과)을 IBattleUnitStatProvider로 노출한다 -
    /// PlaceholderBattleUnitStatProvider 대체(Docs/설계/17번 §5). 값 자체가 아니라 "값의 출처"만
    /// 바뀐 것이라 소비자(LiveBattleSimulationRule 등)는 무변경.
    /// </summary>
    public class TableBattleUnitStatProvider : IBattleUnitStatProvider
    {
        private readonly CharacterStatsTableAsset table;

        public TableBattleUnitStatProvider(CharacterStatsTableAsset table)
        {
            this.table = table;
        }

        public BattleUnitStats GetStats(MercenaryClass mercenaryClass)
        {
            if (table == null || !table.TryGetEntry(mercenaryClass, out var entry))
            {
                throw new InvalidOperationException($"{nameof(CharacterStatsTableAsset)}에 '{mercenaryClass}' 항목이 없다 - Tools/Game/Table/Import Character Stats를 실행했는지 확인.");
            }

            return new BattleUnitStats(entry.MaxHp, entry.Attack, entry.Defense, entry.MoveSpeed, entry.AttackInterval, entry.Range, entry.MoraleSyncRate);
        }
    }
}
