using UnityEngine;

namespace Game.Core
{
    /// <summary>궤주 날짜 정산(기획 92 §3-4, 설계 93 §4.2) - 전투 마무리 단계만 쓴다. 상행 종료 처리보다 먼저 불러야 한다(종료 신호가 나머지를 버린다).</summary>
    public interface ITripDayRout
    {
        void SettleRout();
    }

    /// <summary>
    /// FieldManager 하위 컴포넌트 - 상행 이동 초로 날짜를 진행한다(기획 92, 설계 93 §3). 구간 시계의 이동 신호(실제 이동 초)에 얹으므로 전투·이벤트·정지 중에는
    /// 별도 코드 없이 멈추고, 재계산·지연·단축의 경과 보정은 세지 않는다. 구간 경계와 무관하게 나머지가 이어지고, 상행 시작·종료 신호에서 버린다. 궤주는 중간 도시 도착 신호부터 센 실제 이동 초로 정산한다.
    /// 날짜 진행자가 없으면(DateManager 미설치) 세기만 하고 날짜는 그대로다. 디버그 계약은 아래 에디터 전용 부분에 둔다 - 빌드에 날짜를 직접 미는 진입점을 남기지 않는다.
    /// </summary>
    public partial class TripDayClock : MonoBehaviour, ITripDayRout
    {
        private readonly TripDayCounter counter = new();
        private ISessionState session;
        private ITripMilestoneSource milestones;
        private IDateAdvancer dateAdvancer;
        private ITripTravelSettingsReader settingsReader;

        private TripTravelSettings Settings => settingsReader?.Settings ?? TripTravelSettings.Default();

        public void Bind(ISessionState session, ITripMilestoneSource milestones, IDateAdvancer dateAdvancer, ITripTravelSettingsReader settingsReader)
        {
            Unbind();
            this.session = session;
            this.milestones = milestones;
            this.dateAdvancer = dateAdvancer;
            this.settingsReader = settingsReader;
            if (session != null) session.OnMoved += HandleMoved;
            if (milestones != null) milestones.Raised += HandleMilestone;
        }

        public void SettleRout()
        {
            Advance(counter.SettleRout(Settings));
        }

        private void HandleMoved(float seconds) => Advance(counter.AddMoved(Settings, seconds));

        private void Advance(int days)
        {
            if (days > 0) dateAdvancer?.AdvanceDays(days);
        }

        private void HandleMilestone(TripMilestone milestone)
        {
            if (milestone == TripMilestone.TripStarted || milestone == TripMilestone.TripEnded) counter.Reset();
            else if (milestone == TripMilestone.MidCityArrived) counter.BeginLeg(); // 궤주 기준 "직전 도시부터"를 다시 센다
        }

        private void Unbind()
        {
            if (session != null) session.OnMoved -= HandleMoved;
            if (milestones != null) milestones.Raised -= HandleMilestone;
        }

        private void OnDestroy() => Unbind();
    }

#if UNITY_EDITOR
    /// <summary>디버그 전용(설계 93 §6) - Field 시간 디버그 표시와 "상행 완료" 버튼만 쓴다.</summary>
    public interface ITripDayClockDebug
    {
        float MovedSeconds { get; }
        float SecondsUntilNextDay { get; }
        int RoutPreviewDays { get; }
        void DebugAddMoved(float seconds);
    }

    public partial class TripDayClock : ITripDayClockDebug
    {
        public float MovedSeconds => counter.MovedSeconds;
        public float SecondsUntilNextDay => counter.SecondsUntilNextDay(Settings);
        public int RoutPreviewDays => counter.PreviewRout(Settings);

        public void DebugAddMoved(float seconds) => HandleMoved(seconds);
    }
#endif
}
