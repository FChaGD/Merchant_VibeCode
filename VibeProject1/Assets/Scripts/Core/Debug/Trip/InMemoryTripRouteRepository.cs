#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 디버그로 그린 도시 간 연결선을 세션 동안 보관하는 인메모리 저장소. 경로는 무방향으로 취급해
    /// 정렬된 키("A|B", A&lt;B)로 정규화한 HashSet에 저장한다(HasRoute/Add/Remove O(1)). 도시별 인접
    /// 목록을 함께 유지해 RemoveAllRoutesFor가 전체 스캔 없이 그 도시의 연결 개수만큼만 순회하게 한다.
    /// </summary>
    internal class InMemoryTripRouteRepository : ITripRouteRepository
    {
        private readonly HashSet<string> routeKeys = new();
        private readonly Dictionary<string, HashSet<string>> adjacency = new();

        public event Action<string, string> RouteAdded;
        public event Action<string, string> RouteRemoved;

        public bool HasRoute(string cityIdA, string cityIdB) => routeKeys.Contains(Key(cityIdA, cityIdB));

        public bool TryAddRoute(string cityIdA, string cityIdB)
        {
            if (string.IsNullOrEmpty(cityIdA) || string.IsNullOrEmpty(cityIdB) || cityIdA == cityIdB)
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

        public void RemoveRoute(string cityIdA, string cityIdB)
        {
            if (!routeKeys.Remove(Key(cityIdA, cityIdB)))
            {
                return;
            }

            RemoveAdjacency(cityIdA, cityIdB);
            RemoveAdjacency(cityIdB, cityIdA);
            RouteRemoved?.Invoke(cityIdA, cityIdB);
        }

        public void RemoveAllRoutesFor(string cityId)
        {
            if (!adjacency.TryGetValue(cityId, out var neighbors))
            {
                return;
            }

            var neighborList = new List<string>(neighbors);
            foreach (var neighbor in neighborList)
            {
                RemoveRoute(cityId, neighbor);
            }
        }

        public void Clear()
        {
            var keys = new List<string>(routeKeys);
            routeKeys.Clear();
            adjacency.Clear();
            foreach (var key in keys)
            {
                var parts = key.Split('|');
                RouteRemoved?.Invoke(parts[0], parts[1]);
            }
        }

        public IReadOnlyList<(string CityIdA, string CityIdB)> GetAll()
        {
            var list = new List<(string, string)>(routeKeys.Count);
            foreach (var key in routeKeys)
            {
                var parts = key.Split('|');
                list.Add((parts[0], parts[1]));
            }
            return list;
        }

        private void AddAdjacency(string from, string to)
        {
            if (!adjacency.TryGetValue(from, out var set))
            {
                set = new HashSet<string>();
                adjacency[from] = set;
            }
            set.Add(to);
        }

        private void RemoveAdjacency(string from, string to)
        {
            if (adjacency.TryGetValue(from, out var set))
            {
                set.Remove(to);
            }
        }

        private static string Key(string a, string b) => string.CompareOrdinal(a, b) < 0 ? $"{a}|{b}" : $"{b}|{a}";
    }
}
#endif
