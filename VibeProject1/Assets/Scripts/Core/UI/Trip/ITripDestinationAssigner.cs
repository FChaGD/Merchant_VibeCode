namespace Game.Core
{
    /// <summary>
    /// 도착지 배정을 변경하는 쓰기 인터페이스(기획 16번 §6, 설계 21번 §2.2).
    /// </summary>
    public interface ITripDestinationAssigner : ITripDestinationReader
    {
        /// <summary>
        /// 도시 클릭 판정. routeReader를 호출부가 매번 넘기는 이유: 이 구현체는 정식/전역 싱글턴
        /// (Bootstrap Awake 시점에 생성)인데, 실제 경로 데이터(ITripRouteReader)는 아직 디버그 지도
        /// 도구가 세션마다 들고 있는 로컬 객체라 생성 시점에 주입할 전역 인스턴스가 없다. 호출부(=지금은
        /// 디버그 지도 도구, 유일한 클릭 출처)가 자신이 들고 있는 ITripRouteReader를 그대로 넘기게
        /// 하면, 실제 지역/경로 시스템이 생겨 경로 데이터도 전역이 되더라도 이 메서드 시그니처를 바꿀
        /// 필요 없이 그 시점의 전역 인스턴스를 넘기기만 하면 된다.
        /// </summary>
        void HandleCityClicked(int cityId, ITripRouteReader routeReader);

        void HandleCityDeleted(int cityId);

        /// <summary>도착지 배정만 초기화한다("현재 위치"는 건드리지 않음). 상행 준비 UI 종료 시,
        /// 그리고 도착 판정 성립 시(다음 상행을 위해) 호출한다.</summary>
        void Reset();
    }
}
