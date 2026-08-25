namespace Game.Core
{
    /// <summary>
    /// ICaravanRosterProvider가 필요한 IBattleResultRule 구현체만 선택적으로 구현하는 마커 인터페이스.
    /// IRequiresFormationReader와 같은 이유·패턴이다(OCP) - BattleManager는 이 인터페이스 구현 여부만
    /// 확인해 주입하므로, 새 규칙이 추가/교체돼도 BattleManager는 다시 바뀌지 않는다.
    /// </summary>
    public interface IRequiresCaravanRoster
    {
        void SetCaravanRoster(ICaravanRosterProvider provider);
    }
}
