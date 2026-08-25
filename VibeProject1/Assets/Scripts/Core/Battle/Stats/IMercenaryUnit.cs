namespace Game.Core
{
    /// <summary>
    /// 정비창 로스터 유닛 중 전투 직업 정보를 갖는 것만 구현하는 확장 계약(ISP) - Formation UI는
    /// IFormationUnit만 알면 되고 MercenaryClass 개념을 몰라도 된다. Battle 쪽(LiveBattleSimulationRule)
    /// 에서만 이 인터페이스로 캐스팅해 직업을 조회한다.
    /// </summary>
    public interface IMercenaryUnit : IFormationUnit
    {
        MercenaryClass Class { get; }
    }
}
