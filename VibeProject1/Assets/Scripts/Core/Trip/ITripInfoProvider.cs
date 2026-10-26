namespace Game.Core
{
    /// <summary>
    /// 상행 준비 UI가 표시할 상행 요약 정보를 제공한다. 출발/도착지 표시는 디버그 도시 마커
    /// (TripMapInteractionCoordinator.BuildLocationInfo)가 대신하므로 여기서는 다루지 않는다.
    /// 실제 구현은 지역 시스템 설계 후 연결한다.
    /// </summary>
    public interface ITripInfoProvider
    {
        TripSummary GetTripSummary();
    }
}
