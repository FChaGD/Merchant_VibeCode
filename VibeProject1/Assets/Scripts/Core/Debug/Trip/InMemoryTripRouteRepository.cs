#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 디버그로 그린 도시 간 연결선을 세션 동안 보관하는 인메모리 저장소. 경로는 무방향으로 취급해
    /// 정렬된 (int,int) 튜플 키(A&lt;=B)로 정규화한 HashSet에 저장한다(HasRoute/Add/Remove O(1)). 도시별
    /// 인접 목록을 함께 유지해 RemoveAllRoutesFor가 전체 스캔 없이 그 도시의 연결 개수만큼만 순회하게
    /// 한다. Id가 int가 되면서(설계 20번 §9.3) 문자열 연결/분해("A|B") 없이 튜플로 바로 관리한다 -
    /// 경로 추가/조회마다 문자열 생성 비용이 들던 v1보다 가볍다.
    /// </summary>
    internal class InMemoryTripRouteRepository : ITripRouteRepository
    {
        private readonly HashSet<(int A, int B)> routeKeys = new();
        private readonly Dictionary<int, HashSet<int>> adjacency = new();

        public event Action<int, int> RouteAdded;
        public event Action<int, int> RouteRemoved;

        public bool HasRoute(int cityIdA, int cityIdB) => routeKeys.Contains(Key(cityIdA, cityIdB));

        public IReadOnlyCollection<int> GetConnectedCityIds(int cityId)
        {
            return adjacency.TryGetValue(cityId, out var neighbors) ? neighbors : Array.Empty<int>();
        }

        // 저장 기능 전용(Docs/설계/19번 §2.2) - 튜플 키를 그대로 순회하면 되니 문자열 분해가 필요 없다.
        public IReadOnlyCollection<(int CityIdA, int CityIdB)> GetAllRoutes()
        {
            var result = new List<(int, int)>(routeKeys.Count);
            foreach (var key in routeKeys)
            {
                result.Add((key.A, key.B));
            }
            return result;
        }

        public bool TryAddRoute(int cityIdA, int cityIdB)
        {
            if (cityIdA == cityIdB)
            {
                return false;
            }

            if (!routeKeys.Add(Key(cityIdA, cityIdB)))
            {
                return false; // 이미 연결선이 있음 - 이 디버그 기능은 단일 경로만 허용
            }

            AddAdjacency(cityIdA, cityIdB);
            AddAdjacency(cityIdB, cityIdA);
            RouteAdded?.Invoke(cityIdA, cityIdB);
            return true;
        }

        public void RemoveRoute(int cityIdA, int cityIdB)
        {
            if (!routeKeys.Remove(Key(cityIdA, cityIdB)))
            {
                return;
            }

            RemoveAdjacency(cityIdA, cityIdB);
            RemoveAdjacency(cityIdB, cityIdA);
            RouteRemoved?.Invoke(cityIdA, cityIdB);
        }

        public void RemoveAllRoutesFor(int cityId)
        {
            if (!adjacency.TryGetValue(cityId, out var neighbors))
            {
                return;
            }

            var neighborList = new List<int>(neighbors);
            foreach (var neighbor in neighborList)
            {
                RemoveRoute(cityId, neighbor);
            }
        }

        public void Clear()
        {
            var keys = new List<(int A, int B)>(routeKeys);
            routeKeys.Clear();
            adjacency.Clear();
            foreach (var key in keys)
            {
                RouteRemoved?.Invoke(key.A, key.B);
            }
        }

        private void AddAdjacency(int from, int to)
        {
            if (!adjacency.TryGetValue(from, out var set))
            {
                set = new HashSet<int>();
                adjacency[from] = set;
            }
            set.Add(to);
        }

        private void RemoveAdjacency(int from, int to)
        {
            if (adjacency.TryGetValue(from, out var set))
            {
                set.Remove(to);
            }
        }

        private static (int A, int B) Key(int a, int b) => a <= b ? (a, b) : (b, a);
    }
}
#endif
