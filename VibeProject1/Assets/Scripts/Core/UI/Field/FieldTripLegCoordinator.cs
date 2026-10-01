using System;

namespace Game.Core
{
    /// <summary>
    /// 상행을 구간(다음 도시까지) 단위로 진행한다(Docs/기획/75번 §3.2-1, 설계 76번 §6.2). FieldUIController가 소유하는 plain C# 객체 - 뷰 조율
    /// (게이지·버튼·전투 전환)과 여정 진행(구간 시작·중간 도착·다음 구간)을 나누기 위해 뽑았다(SRP).
    /// 중간 도시에서는 현재 위치만 갱신하고 회복·배치 확정·Hub 진입 없이 알림 뒤 다음 구간을 출발한다(기획 75번 §4-17·§4-18) - 마지막 구간 도착만
    /// 기존 도착 흐름(onTripArrived)으로 넘긴다. 정비창 배치·이동 타이머는 시간 단위라 구간이 바뀌어도 그대로 이어진다(§4-21).
    /// 여정이 없으면(Field에서 바로 Play 등) 예전처럼 구간 하나(30초)로 진행한다.
    /// </summary>
    public sealed class FieldTripLegCoordinator
    {
        private const float LegArrivalNoticeSeconds = 2f;

        private readonly ISessionState sessionState;
        private readonly ITripItinerary itinerary;
        private readonly ITripCurrentLocationRepository currentLocation;
        private readonly IWorldMapReader worldMap;
        private readonly FieldProgressGaugeView gaugeView;
        private readonly FieldLegArrivalNoticeView noticeView;
        private readonly Action onTripArrived;

        public FieldTripLegCoordinator(ISessionState sessionState, ITripItinerary itinerary, ITripCurrentLocationRepository currentLocation, IWorldMapReader worldMap,
            FieldProgressGaugeView gaugeView, FieldLegArrivalNoticeView noticeView, Action onTripArrived)
        {
            this.sessionState = sessionState;
            this.itinerary = itinerary;
            this.currentLocation = currentLocation;
            this.worldMap = worldMap;
            this.gaugeView = gaugeView;
            this.noticeView = noticeView;
            this.onTripArrived = onTripArrived;
        }

        public void BeginFirstLeg() => BeginCurrentLeg();

        // SessionStateTracker.OnArrived - 구간 하나가 끝났다(진행은 이미 멈춘 상태).
        public void HandleLegArrived()
        {
            if (itinerary == null || !itinerary.HasItinerary || !itinerary.HasNextLeg)
            {
                itinerary?.Clear();
                onTripArrived();
                return;
            }

            var arrivedCityId = itinerary.CurrentLeg.ToCityId;
            currentLocation?.SetCurrentCity(arrivedCityId); // 구간마다 갱신 - 상행이 도중에 끝나도 실제로 도착한 곳에 있다(기획 75번 §4-18)
            itinerary.AdvanceLeg();
            var message = $"구간 도착: {CityName(arrivedCityId)}";
            if (noticeView != null) noticeView.Show(message, LegArrivalNoticeSeconds, BeginCurrentLeg);
            else BeginCurrentLeg();
        }

        private void BeginCurrentLeg()
        {
            if (itinerary == null || !itinerary.HasItinerary)
            {
                gaugeView.SetLegLabel(string.Empty);
                sessionState.Begin(TripTravelSettings.LegacyDurationSeconds);
                return;
            }

            var leg = itinerary.CurrentLeg;
            gaugeView.SetLegLabel($"구간 {itinerary.CurrentLegIndex + 1}/{itinerary.LegCount} · {leg.GradeName}");
            sessionState.Begin(leg.DurationSeconds);
        }

        private string CityName(int cityId)
            => worldMap != null && worldMap.TryGetCity(cityId, out var city) && !string.IsNullOrEmpty(city.Name) ? city.Name : $"디버그 도시 {cityId}";
    }
}
