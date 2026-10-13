using System;

namespace Game.Core
{
    /// <summary>상행 정지 원인(설계 87 §6.2). EventPending = 이벤트가 떴지만 정비창·방향성 지시가 닫히기를 기다리는 중.</summary>
    public enum TripHaltReason { Battle, Event, EventPending }

    /// <summary>정지 상태 조회 - 대화 진행자(88)는 정지 시작을 듣고 대화를 끝낸다. 걸기·풀기 권한은 주지 않는다(ISP).</summary>
    public interface ITripHaltReader
    {
        bool IsHalted { get; }
        bool IsHeld(TripHaltReason reason);
        event Action HaltStarted;
        event Action HaltEnded;
    }

    /// <summary>
    /// 상행 정지 걸기·풀기. 상행 시계와 배치 활동을 멈추고 재개하는 곳은 이 계약 하나뿐이다 - 원인이 겹칠 때(이벤트 중 전투) 한쪽 해제가 다른 쪽
    /// 정지를 풀어 버리는 문제를 막기 위해서다.
    /// </summary>
    public interface ITripHaltControl : ITripHaltReader
    {
        void Acquire(TripHaltReason reason);
        void Release(TripHaltReason reason);
        /// <summary>상행 종료 - 원인을 모두 지우되 재개하지 않는다(다음 구간 시작이 시계를 다시 돌린다).</summary>
        void ClearAll();
    }
}
