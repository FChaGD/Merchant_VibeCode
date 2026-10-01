namespace Game.Core
{
    /// <summary>
    /// 도시 간 도달 가능 여부를 노출하는 읽기 전용 인터페이스. 도착지 결정 로직(TripDestinationAssigner, 정식 기능)이 이 인터페이스에만
    /// 의존한다 - 구현은 월드 지도 모델(WorldMap, Docs/설계/69번 §4.2)이 맡고, 지역을 넘는 관문 짝까지 따라 판정한다.
    /// 예전의 "직접 연결된 도시 목록" 조회는 디버그 도로 그리기만 쓰던 것이라 모델 쪽 읽기 계약으로 옮겼다.
    /// </summary>
    public interface ITripRouteReader
    {
        /// <summary>fromCityId에서 toCityId까지 도로(와 관문 짝)를 거쳐 도달 가능한가(몇 개를 거치든 무관 - 기획 16번 §6.1).</summary>
        bool IsReachable(int fromCityId, int toCityId);
    }
}
