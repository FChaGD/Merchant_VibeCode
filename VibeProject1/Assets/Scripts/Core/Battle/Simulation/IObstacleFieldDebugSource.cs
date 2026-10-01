namespace Game.Core
{
    /// <summary>
    /// 디버깅 전용 - 전투 장애물 기즈모(BattleObstacleGizmoView)가 이번 전투의 장애물 정보를 읽는 통로. 시뮬레이션 루프는
    /// 장애물을 모르므로(설계 72번 §5) 루프가 아니라 전투 규칙 컴포넌트가 노출한다. 기즈모를 걷어내면 이 파일과
    /// LiveBattleSimulationRule의 구현 프로퍼티만 지우면 된다.
    /// </summary>
    public interface IObstacleFieldDebugSource
    {
        BattleObstacleField DebugObstacleField { get; }
    }
}
