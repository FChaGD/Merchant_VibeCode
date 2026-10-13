using System;

namespace Game.Core
{
    /// <summary>상행 계기 신호(설계 87 §6.4). 전투 종료는 상행을 이어갈 때만, 이벤트 전투면 이벤트가 끝나 재개될 때 보낸다(89).</summary>
    public enum TripMilestone { TripStarted, MidCityArrived, BattleEnded, EventEnded, TripEnded }

    /// <summary>계기 발신 - 상행 진행 조율자(FieldUIController·구간 진행·전투 마무리·이벤트 진행자)만 쓴다.</summary>
    public interface ITripMilestoneSink
    {
        void Raise(TripMilestone milestone);
    }

    /// <summary>계기 수신 - 이력(상행 기록 비우기)·판정 시계(0으로)·정지 조율(시작·종료 시 비우기)·대화(88 계기 대기)가 듣는다.</summary>
    public interface ITripMilestoneSource
    {
        event Action<TripMilestone> Raised;
    }
}
