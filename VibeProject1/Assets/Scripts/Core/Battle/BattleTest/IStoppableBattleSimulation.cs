namespace Game.Core
{
    /// <summary>
    /// 배틀 테스트 씬 전용 - 진행 중인 시뮬레이션 틱을 다시 일시정지한다(재개는 실제 게임과 동일한
    /// IPausableBattleSimulation.ResumeSimulation()을 그대로 재사용). 실제 게임은 "재정지"가 필요
    /// 없어 이 인터페이스는 LiveBattleSimulationRule과 무관한 별도 마커다 - IRequiresFormationReader와
    /// 같은 패턴(BattleTestController가 is 캐스팅으로 확인).
    /// </summary>
    public interface IStoppableBattleSimulation
    {
        void Pause();
    }
}
