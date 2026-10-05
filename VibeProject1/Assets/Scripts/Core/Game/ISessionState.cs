using System;

namespace Game.Core
{
    /// <summary>
    /// 상행 진행 상태 조회/시작/재개 인터페이스. 진행 단위는 상행 구간 하나(다음 도시까지)다 - 소요시간은 구간마다
    /// 거리·난이도로 계산해 시작할 때 넘긴다(Docs/설계/76번 §6.1).
    /// </summary>
    public interface ISessionState : ISessionPauseControl
    {
        float Progress { get; }              // 0(출발) ~ 1(도착)
        event Action<float> OnProgressChanged;
        event Action OnArrived;

        void Begin(float durationSeconds);
        void Resume();

        /// <summary>
        /// 진행 중인 구간의 소요시간을 늘린다(전투 마차 파괴 지연, Docs/설계/79번 §5.4). 경과는 유지하므로 진행도가 그만큼 뒤로
        /// 물러난다(§15-11). 이미 도착한 구간이거나 0 이하면 무시한다.
        /// </summary>
        void ExtendDuration(float seconds);
    }
}
