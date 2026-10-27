using System;

namespace Game.Core
{
    /// <summary>최종 도착 알림 발신 - 상행 생명주기(FieldTripLifecycle)만 쓴다.</summary>
    public interface ITripArrivalSink
    {
        void NotifyFinalArrival(int cityId);
    }

    /// <summary>
    /// 최종 도착 수신(Docs/설계/107번 §6.2) - 퀘스트 도착·즉시 완료 판정용. 현재 위치 변경 신호는 경유 도시 도착마다 오고, 상행 계기(TripEnded)는 패배 종료와
    /// 구분되지 않아 따로 둔다. 궤주 귀환은 최종 도착이 아니다.
    /// </summary>
    public interface ITripArrivalSource
    {
        event Action<int> FinalArrived;
    }
}
