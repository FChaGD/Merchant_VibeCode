namespace Game.Core
{
    /// <summary>
    /// 상행 준비 UI가 표시할 출발지/도착지/상행 요약 정보를 제공한다.
    /// 실제 구현은 지역 시스템 설계 후 연결한다.
    /// </summary>
    public interface ITripInfoProvider
    {
        ITripLocationInfo GetOrigin();
        ITripLocationInfo GetDestination();
        TripSummary GetTripSummary();
    }
}
