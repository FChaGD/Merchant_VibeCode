using System;

namespace Game.Core
{
    /// <summary>
    /// CharacterStatsTableAsset(엑셀 임포트 결과)을 스탯 조회 계약으로 노출한다(Docs/설계/17번 §5, 54번 §5). 캐릭터 Id 조회와
    /// 직업 대표 조회를 한 클래스가 구현하지만 소비자는 필요한 계약 하나만 받는다(실제 전투 / 배틀 테스트).
    /// </summary>
    public class TableBattleUnitStatProvider : IBattleUnitStatProvider, IClassRepresentativeStatProvider
    {
        private readonly CharacterStatsTableAsset table;

        public TableBattleUnitStatProvider(CharacterStatsTableAsset table)
        {
            this.table = table;
        }

        public BattleUnitStats GetStats(string characterId)
        {
            if (table == null || !table.TryGetEntry(characterId, out var entry))
            {
                throw new InvalidOperationException($"{nameof(CharacterStatsTableAsset)}에 캐릭터 '{characterId}' 항목이 없다 - Tools/Game/Table/Import Character Stats를 실행했는지 확인.");
            }

            return ToStats(entry);
        }

        public BattleUnitStats GetRepresentativeStats(string mercenaryClass)
        {
            if (table == null || !table.TryGetFirstOfClass(mercenaryClass, out var entry))
            {
                throw new InvalidOperationException($"{nameof(CharacterStatsTableAsset)}에 직업 '{mercenaryClass}'의 캐릭터가 없다 - Tools/Game/Table/Import Character Stats를 실행했는지 확인.");
            }

            return ToStats(entry);
        }

        public static BattleUnitStats ToStats(CharacterStatsEntry entry)
            => new(entry.MaxHp, entry.Attack, entry.Defense, entry.MoveSpeed, entry.AttackInterval, entry.Range, entry.MoraleSyncRate);
    }
}
