using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    [Serializable]
    public struct CharacterStatsEntry
    {
        public string Id; // 캐릭터 Id(예: Warrior01) - 행 단위가 직업에서 캐릭터로 바뀌었다(Docs/기획/53번 §3.1)
        public string MercenaryClass; // 직업 Id - 역할군·방향성 지시 분류용
        public float MaxHp;
        public float Attack;
        public float Defense;
        public float MoveSpeed;
        public float AttackInterval;
        public float Range;
        public float MoraleSyncRate;
        public int HireCost;
    }

    /// <summary>
    /// 캐릭터별 스탯(Docs/설계/54번 §2.2). 같은 직업이라도 캐릭터마다 행을 따로 가진다 - 캐릭터끼리 스탯 행을 공유하지 않게 하는
    /// 것이 요구사항이다(기획 53번 §3.1). 값은 CharacterStatsTableImporter가 Assets/Table/Character/CharacterStats.xlsx에서
    /// 채운다. 이름은 CharacterStringsTableAsset, 직업명은 MercenaryClassStringsTableAsset으로 분리되어 있다.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterStatsTable", menuName = "Game/Table/Character Stats Table")]
    public class CharacterStatsTableAsset : ScriptableObject
    {
        [SerializeField] private List<CharacterStatsEntry> entries = new();

        public bool TryGetEntry(string characterId, out CharacterStatsEntry entry)
            => TableEntryLookup.TryFind(entries, characterId, e => e.Id, out entry);

        // 직업 단위로만 유닛을 만드는 배틀 테스트 씬용 - 해당 직업의 테이블 첫 행을 대표 캐릭터로 쓴다(설계 54번 §6.3).
        // 대표를 코드 상수로 박지 않아 테이블 행이 바뀌어도 배틀 테스트가 깨지지 않는다.
        public bool TryGetFirstOfClass(string mercenaryClass, out CharacterStatsEntry entry)
            => TableEntryLookup.TryFind(entries, mercenaryClass, e => e.MercenaryClass, out entry);

        public IReadOnlyList<CharacterStatsEntry> Entries => entries;
    }
}
