namespace Game.Core
{
    /// <summary>
    /// 전투 시뮬레이션 틱을 일시정지/재개할 수 있는 규칙(IBattleResultRule)을 위한 선택적 마커
    /// 인터페이스 - IRequiresFormationReader와 동일한 패턴(BattleManager가 is 캐스팅으로 확인).
    /// PlaceholderBattleResultRule처럼 매 프레임 틱이 없는 규칙은 구현하지 않아도 된다.
    /// </summary>
    public interface IPausableBattleSimulation
    {
        void ResumeSimulation();
    }
}
