namespace Game.Core
{
    /// <summary>
    /// 캐릭터 한 명의 표시·거래용 정보(Docs/설계/54번 §3). 스탯·이름·직업명이 서로 다른 테이블에 있어 소비자(고용 화면·후보 목록)가
    /// 매번 조인하지 않게 카탈로그가 한 번 묶어 둔다.
    /// </summary>
    public sealed class CharacterProfile
    {
        public string CharacterId { get; }
        public string Name { get; }
        public string MercenaryClass { get; }
        public string ClassLabel { get; }
        public BattleUnitStats Stats { get; }
        public int HireCost { get; }

        public CharacterProfile(string characterId, string name, string mercenaryClass, string classLabel, BattleUnitStats stats, int hireCost)
        {
            CharacterId = characterId;
            Name = name;
            MercenaryClass = mercenaryClass;
            ClassLabel = classLabel;
            Stats = stats;
            HireCost = hireCost;
        }
    }
}
