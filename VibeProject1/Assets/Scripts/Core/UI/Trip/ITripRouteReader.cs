using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// "두 도시 사이에 경로가 있는가?"만 노출하는 읽기 전용 인터페이스(무방향 - (A,B)와 (B,A) 동일 취급).
    /// 출발/도착 결정 로직(정식 기능, 후속 구현 단계)이 이 인터페이스에만 의존하도록 해, 지금은 디버그
    /// 경로 연결(ITripRouteRepository)이 구현하지만 실제 경로 시스템이 생기면 구현체만 교체하면 된다.
    /// </summary>
    public interface ITripRouteReader
    {
        bool HasRoute(int cityIdA, int cityIdB);

        /// <summary>cityId와 연결된 상대 도시 id 전체. 연결이 없으면 빈 컬렉션.</summary>
        IReadOnlyCollection<int> GetConnectedCityIds(int cityId);
    }
}
