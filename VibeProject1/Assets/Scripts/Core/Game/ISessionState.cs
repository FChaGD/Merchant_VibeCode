using System;

namespace Game.Core
{
    /// <summary>
    /// 상행 진행 상태 조회/시작/재개 인터페이스. 테스트 단계 시간 기반 계산이 실제 거리+변수 기반
    /// 계산으로 바뀌어도 이 인터페이스는 그대로 유지하고 구현체(SessionStateTracker)만 교체하면 된다
    /// (Docs/설계/04-2026-08-25-Field씬_아키텍처.md 3절).
    /// </summary>
    public interface ISessionState : ISessionPauseControl
    {
        float Progress { get; }              // 0(출발) ~ 1(도착)
        event Action<float> OnProgressChanged;
        event Action OnArrived;

        void Begin();
        void Resume();
    }
}
