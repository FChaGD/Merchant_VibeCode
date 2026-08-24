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
        bool TryAddRoute(string cityIdA, string cityIdB);
        void RemoveRoute(string cityIdA, string cityIdB);
        void RemoveAllRoutesFor(string cityId);
        void Clear();
        IReadOnlyList<(string CityIdA, string CityIdB)> GetAll();
        event Action<string, string> RouteAdded;
        event Action<string, string> RouteRemoved;
    }
}
#endif
