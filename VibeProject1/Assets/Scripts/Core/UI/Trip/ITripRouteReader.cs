using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 도시 간 경로 데이터를 노출하는 읽기 전용 인터페이스(무방향 - (A,B)와 (B,A) 동일 취급). 도착지
    /// 결정 로직(TripDestinationAssigner, 정식 기능)이 이 인터페이스에만 의존하도록 해, 지금은 디버그
    /// 경로 연결(ITripRouteRepository)이 구현하지만 실제 경로 시스템이 생기면 구현체만 교체하면 된다.
    /// </summary>
    public interface ITripRouteReader
    {
        /// <summary>fromCityId에서 toCityId까지 경로를 거쳐 도달 가능한가(직접 연결이 아니어도, 몇
        /// 개를 거치든 무관 - 기획 16번 §6.1).</summary>
        bool IsReachable(int fromCityId, int toCityId);

        /// <summary>cityId와 직접 연결된 상대 도시 id 전체. 연결이 없으면 빈 컬렉션.</summary>
        IReadOnlyCollection<int> GetConnectedCityIds(int cityId);
    }
}
