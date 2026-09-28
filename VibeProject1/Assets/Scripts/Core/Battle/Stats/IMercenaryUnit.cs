namespace Game.Core
{
    /// <summary>
    /// 정비창 로스터 유닛 중 전투 캐릭터만 구현하는 확장 계약(ISP) - Formation UI는 IFormationUnit만 알면 되고 직업·캐릭터 개념을
    /// 몰라도 된다. 직업(Class)은 역할군·방향성 지시·팔레트 분류용, 캐릭터 Id(CharacterId)는 스탯·이름 데이터 키다(Docs/설계/54번 §4.1).
    /// 지금은 인스턴스 Id와 캐릭터 Id가 같지만, 실제 로스터 시스템에서 인스턴스 Id를 분리할 수 있게 속성을 따로 둔다.
    /// </summary>
    public interface IMercenaryUnit : IFormationUnit
    {
        string Class { get; }
        string CharacterId { get; }
    }
}
