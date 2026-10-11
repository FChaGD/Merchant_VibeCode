namespace Game.Core
{
    /// <summary>
    /// 상행 이동 시간을 게임 날짜에 반영한다(Docs/기획/84번 §4.2, 설계 85번 §3.3). 경과 시간은 SessionStateTracker가 이미 센다(전투 중 정지,
    /// 도주 후 같은 구간 이어짐) - 여기서 따로 세지 않고 가산 시점에 읽기만 한다. 도주는 호출하지 않는다(다음 도착 때 함께 계산).
    /// 구간 도착은 경과가 아니라 최종 소요시간을 쓴다 - 도착 프레임 경과는 소요를 약간 넘을 수 있고, 소요 기준이어야 패널 예상과 같다.
    /// 반드시 다음 구간 Begin 전에 불러야 한다(Begin이 경과를 0으로 되돌린다). 날짜 진행 계약이 없으면 아무것도 하지 않는다.
    /// </summary>
    public sealed class TripDateAccrual
    {
        private readonly ISessionState sessionState;
        private readonly IDateAdvancer dateAdvancer;
        private readonly ITripTravelSettingsReader settingsReader;

        public TripDateAccrual(ISessionState sessionState, IDateAdvancer dateAdvancer, ITripTravelSettingsReader settingsReader)
        {
            this.sessionState = sessionState;
            this.dateAdvancer = dateAdvancer;
            this.settingsReader = settingsReader;
        }

        public void AccrueLegArrival() => Advance(TravelDayRules.ToDays(Settings, sessionState.DurationSeconds));

        public void AccrueRout() => Advance(TravelDayRules.ToRoutDays(Settings, sessionState.ElapsedSeconds));

        private TripTravelSettings Settings => settingsReader?.Settings ?? TripTravelSettings.Default();

        private void Advance(int days)
        {
            if (dateAdvancer == null || days <= 0) return;
            dateAdvancer.AdvanceDays(days);
        }
    }
}
