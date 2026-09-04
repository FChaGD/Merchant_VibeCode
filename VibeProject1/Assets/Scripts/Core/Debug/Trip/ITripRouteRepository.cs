#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 디버그 경로 연결의 생성/삭제를 담당하는 쓰기 인터페이스. RouteAdded/RouteRemoved는 연결선 뷰
    /// 생성·정리(및 도시 삭제 시 연쇄 삭제)를 코디네이터가 이벤트만으로 처리할 수 있게 한다.
    /// </summary>
    public interface ITripRouteRepository : ITripRouteReader
    {
        bool TryAddRoute(int cityIdA, int cityIdB);
        void RemoveRoute(int cityIdA, int cityIdB);
        void RemoveAllRoutesFor(int cityId);
        void Clear();
        // 저장 기능 전용 - 정식 ITripRouteReader(TripDestinationAssigner가 의존)에는 얹지 않는다
        // (ISP, Docs/설계/19번 §2.2).
        IReadOnlyCollection<(int CityIdA, int CityIdB)> GetAllRoutes();
        event Action<int, int> RouteAdded;
        event Action<int, int> RouteRemoved;
    }
}
#endif
