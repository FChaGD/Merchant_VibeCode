namespace Game.Core
{
    /// <summary>
    /// IBattleResultRule 구현체가 유닛 상태 저장소를 필요로 하면 이 마커를 구현한다(OCP) -
    /// IRequiresCaravanRoster/IRequiresFormationReader와 같은 패턴. BattleManager가 이 마커를 감지해
    /// 주입하므로, 어떤 규칙이 이 저장소를 쓰는지 BattleManager는 몰라도 된다.
    /// </summary>
    public interface IRequiresUnitConditionRepository
    {
        void SetUnitConditionRepository(IUnitConditionRepository repository);
    }
}
