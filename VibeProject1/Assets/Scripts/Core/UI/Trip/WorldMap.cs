using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 월드 지도 모델(Docs/설계/69번 §4.2) - 지역·도시·관문·도로. 예전엔 디버그 전용 저장소 두 개(도시·도로)에 흩어져 에디터에서만
    /// 존재했지만, 지도 표시·도착지 선택이 빌드에도 들어가도록 정식 모델로 올렸다. 편집(IWorldMapEditor)은 디버그 도구만 쓴다.
    ///
    /// 도달 판정은 끝점 그래프(도로 + 관문 짝)를 BFS로 돈다 - 지역을 넘는 통로는 관문 짝이 잇고, 도로는 같은 지역 안에만 있다.
    /// 규모가 도시·관문 수십 개라 인접 목록 BFS면 충분하다(예전 도로 저장소와 같은 판단).
    /// </summary>
    public sealed class WorldMap : IWorldMapEditor, ITripRouteReader
    {
        public static readonly Vector2 DefaultRegionSize = new(TripMapView.ContentSize, TripMapView.ContentSize);
        private const string RegionNamePrefix = "지역";

        private readonly List<WorldRegion> regions = new();
        private readonly Dictionary<int, WorldRegion> regionsById = new();
        private readonly Dictionary<int, WorldCity> citiesById = new();
        private readonly Dictionary<int, WorldGate> gatesById = new();
        private readonly Dictionary<int, HashSet<int>> cityIdsByRegion = new();
        private readonly Dictionary<int, HashSet<int>> gateIdsByRegion = new();
        private readonly HashSet<(MapNodeId A, MapNodeId B)> roads = new();
        // 도로별 난이도 기본값(Docs/설계/76번 §3.1) - 데이터(RoadData.Difficulty)에서 온 값. 게임 중 바뀐 값은 RoadDifficultyState가 따로 든다.
        private readonly Dictionary<RoadKey, int> roadDifficulty = new();
        private readonly Dictionary<MapNodeId, HashSet<MapNodeId>> adjacency = new();
        private int nextCityId = 1;
        private int nextGateId = 1;

        public event Action Changed;
        public event Action<MapNodeId, Vector2> NodeMoved;
        public event Action<int> CityRemoved;

        public IReadOnlyList<WorldRegion> Regions => regions;
        public IEnumerable<WorldCity> AllCities => citiesById.Values;
        public IEnumerable<WorldGate> AllGates => gatesById.Values;
        public IEnumerable<(MapNodeId A, MapNodeId B)> AllRoads => roads;

        // ==================== 불러오기(자산 → 모델) - 변경 이벤트 없이 채운다 ====================

        public void RestoreRegion(int regionId, string name, Vector2 size)
        {
            if (regionsById.ContainsKey(regionId)) return;
            var region = new WorldRegion(regionId, string.IsNullOrEmpty(name) ? $"{RegionNamePrefix}{regionId}" : name, size);
            regionsById[regionId] = region;
            regions.Add(region);
            regions.Sort((a, b) => a.Id.CompareTo(b.Id));
            cityIdsByRegion[regionId] = new HashSet<int>();
            gateIdsByRegion[regionId] = new HashSet<int>();
        }

        public void RestoreCity(int cityId, int regionId, Vector2 position, string name, string description, string scale)
        {
            if (!regionsById.ContainsKey(regionId) || citiesById.ContainsKey(cityId)) return;
            citiesById[cityId] = new WorldCity(cityId, regionId, position, name, description, scale);
            cityIdsByRegion[regionId].Add(cityId);
            nextCityId = Mathf.Max(nextCityId, cityId + 1);
        }

        public void RestoreGate(int gateId, int regionId, Vector2 position, int pairGateId)
        {
            if (!regionsById.ContainsKey(regionId) || gatesById.ContainsKey(gateId)) return;
            gatesById[gateId] = new WorldGate(gateId, regionId, position, pairGateId);
            gateIdsByRegion[regionId].Add(gateId);
            nextGateId = Mathf.Max(nextGateId, gateId + 1);
        }

        public void RestoreRoad(MapNodeId a, MapNodeId b, int difficulty = TripTravelSettings.DefaultRoadDifficulty)
        {
            if (AddRoadInternal(a, b)) roadDifficulty[RoadKey.Of(a, b)] = TripTravelRules.ClampDifficulty(difficulty);
        }

        // ==================== 읽기 ====================

        public bool TryGetRegion(int regionId, out WorldRegion region) => regionsById.TryGetValue(regionId, out region);
        public bool TryGetCity(int cityId, out WorldCity city) => citiesById.TryGetValue(cityId, out city);
        public bool TryGetGate(int gateId, out WorldGate gate) => gatesById.TryGetValue(gateId, out gate);

        public IEnumerable<WorldCity> GetCities(int regionId)
            => cityIdsByRegion.TryGetValue(regionId, out var ids) ? ids.Select(id => citiesById[id]) : Enumerable.Empty<WorldCity>();

        public IEnumerable<WorldGate> GetGates(int regionId)
            => gateIdsByRegion.TryGetValue(regionId, out var ids) ? ids.Select(id => gatesById[id]) : Enumerable.Empty<WorldGate>();

        // 도로는 같은 지역 끝점끼리만 있으므로 한쪽 끝점의 지역으로 거른다.
        public IEnumerable<(MapNodeId A, MapNodeId B)> GetRoads(int regionId)
            => roads.Where(road => TryGetNodePosition(road.A, out var region, out _) && region == regionId);

        public int GetRoadBaseDifficulty(RoadKey road)
            => roadDifficulty.TryGetValue(road, out var value) ? value : TripTravelSettings.DefaultRoadDifficulty;

        public IEnumerable<MapNodeId> GetConnectedNodes(MapNodeId node)
            => adjacency.TryGetValue(node, out var set) ? set : Enumerable.Empty<MapNodeId>();

        public bool TryGetNodePosition(MapNodeId node, out int regionId, out Vector2 position)
        {
            if (node.IsCity && citiesById.TryGetValue(node.Id, out var city))
            {
                regionId = city.RegionId;
                position = city.Position;
                return true;
            }
            if (!node.IsCity && gatesById.TryGetValue(node.Id, out var gate))
            {
                regionId = gate.RegionId;
                position = gate.Position;
                return true;
            }
            regionId = 0;
            position = default;
            return false;
        }

        // 도로 간선 + 관문 짝 간선으로 BFS - 지역을 넘어 판정한다(기획 68번 §3.3).
        public bool IsReachable(int fromCityId, int toCityId)
        {
            if (fromCityId == toCityId) return true;
            var start = MapNodeId.City(fromCityId);
            var goal = MapNodeId.City(toCityId);
            if (!citiesById.ContainsKey(fromCityId) || !citiesById.ContainsKey(toCityId)) return false;

            var visited = new HashSet<MapNodeId> { start };
            var queue = new Queue<MapNodeId>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var next in Neighbors(current))
                {
                    if (next == goal) return true;
                    if (visited.Add(next)) queue.Enqueue(next);
                }
            }
            return false;
        }

        private IEnumerable<MapNodeId> Neighbors(MapNodeId node)
        {
            foreach (var next in GetConnectedNodes(node)) yield return next;
            if (!node.IsCity && gatesById.TryGetValue(node.Id, out var gate) && gatesById.ContainsKey(gate.PairGateId))
            {
                yield return MapNodeId.Gate(gate.PairGateId);
            }
        }

        // ==================== 편집(디버그) ====================

        public int AddCity(int regionId, Vector2 position)
        {
            if (!regionsById.ContainsKey(regionId)) return -1;
            var id = nextCityId++;
            citiesById[id] = new WorldCity(id, regionId, position, null, null, string.Empty);
            cityIdsByRegion[regionId].Add(id);
            Changed?.Invoke();
            return id;
        }

        public void MoveNode(MapNodeId node, Vector2 position)
        {
            if (node.IsCity && citiesById.TryGetValue(node.Id, out var city)) city.Position = position;
            else if (!node.IsCity && gatesById.TryGetValue(node.Id, out var gate)) gate.Position = position;
            else return;
            NodeMoved?.Invoke(node, position);
        }

        public bool RemoveCity(int cityId)
        {
            if (!RemoveCityInternal(cityId)) return false;
            Changed?.Invoke();
            return true;
        }

        public int AddGatePair(int regionId, Vector2 position, int targetRegionId)
        {
            if (regionId == targetRegionId || !regionsById.ContainsKey(regionId) || !regionsById.ContainsKey(targetRegionId)) return -1;

            var gateId = nextGateId++;
            var pairId = nextGateId++;
            // 짝은 대상 지역 지도 중앙(콘텐츠 좌표 원점)에 만든다(기획 68번 §3.5).
            RestoreGate(gateId, regionId, position, pairId);
            RestoreGate(pairId, targetRegionId, Vector2.zero, gateId);
            Changed?.Invoke();
            return gateId;
        }

        public bool RemoveGate(int gateId)
        {
            if (!gatesById.TryGetValue(gateId, out var gate)) return false;
            RemoveGateInternal(gate.PairGateId);
            RemoveGateInternal(gateId);
            Changed?.Invoke();
            return true;
        }

        public bool TryAddRoad(MapNodeId a, MapNodeId b)
        {
            if (a == b) return false;
            if (!TryGetNodePosition(a, out var regionA, out _) || !TryGetNodePosition(b, out var regionB, out _) || regionA != regionB) return false;
            if (!AddRoadInternal(a, b)) return false;
            // 디버그 편집기로 그린 새 도로는 기본 난이도 - 난이도 편집은 엑셀에서 한다(설계 76번 §10-7).
            roadDifficulty[RoadKey.Of(a, b)] = TripTravelSettings.DefaultRoadDifficulty;
            Changed?.Invoke();
            return true;
        }

        public bool RemoveRoad(MapNodeId a, MapNodeId b)
        {
            if (!RemoveRoadInternal(a, b)) return false;
            Changed?.Invoke();
            return true;
        }

        public void ClearCities(int regionId)
        {
            if (!cityIdsByRegion.TryGetValue(regionId, out var ids) || ids.Count == 0) return;
            foreach (var id in ids.ToList()) RemoveCityInternal(id);
            Changed?.Invoke();
        }

        public void ClearRoads(int regionId)
        {
            var targets = GetRoads(regionId).ToList();
            if (targets.Count == 0) return;
            foreach (var (a, b) in targets) RemoveRoadInternal(a, b);
            Changed?.Invoke();
        }

        public int AddRegion()
        {
            var id = regions.Count == 0 ? 1 : regions.Max(region => region.Id) + 1;
            RestoreRegion(id, null, DefaultRegionSize);
            Changed?.Invoke();
            return id;
        }

        public RegionRemovalCheck CheckRegionRemoval(int regionId, int currentCityId)
        {
            if (!regionsById.ContainsKey(regionId)) return RegionRemovalCheck.NotFound;
            if (regions.Count <= 1) return RegionRemovalCheck.LastRegion;
            if (citiesById.TryGetValue(currentCityId, out var current) && current.RegionId == regionId) return RegionRemovalCheck.HasCurrentLocation;
            return RegionRemovalCheck.Allowed;
        }

        public bool RemoveRegion(int regionId, int currentCityId)
        {
            if (CheckRegionRemoval(regionId, currentCityId) != RegionRemovalCheck.Allowed) return false;

            foreach (var gateId in gateIdsByRegion[regionId].ToList())
            {
                if (gatesById.TryGetValue(gateId, out var gate)) RemoveGateInternal(gate.PairGateId);
                RemoveGateInternal(gateId);
            }
            foreach (var cityId in cityIdsByRegion[regionId].ToList()) RemoveCityInternal(cityId);

            regions.Remove(regionsById[regionId]);
            regionsById.Remove(regionId);
            cityIdsByRegion.Remove(regionId);
            gateIdsByRegion.Remove(regionId);
            Changed?.Invoke();
            return true;
        }

        // ==================== 내부 ====================

        private bool RemoveCityInternal(int cityId)
        {
            if (!citiesById.Remove(cityId, out var city)) return false;
            cityIdsByRegion[city.RegionId].Remove(cityId);
            RemoveAllRoadsFor(MapNodeId.City(cityId));
            CityRemoved?.Invoke(cityId);
            return true;
        }

        private void RemoveGateInternal(int gateId)
        {
            if (!gatesById.Remove(gateId, out var gate)) return;
            gateIdsByRegion[gate.RegionId].Remove(gateId);
            RemoveAllRoadsFor(MapNodeId.Gate(gateId));
        }

        private void RemoveAllRoadsFor(MapNodeId node)
        {
            if (!adjacency.TryGetValue(node, out var neighbors)) return;
            foreach (var other in neighbors.ToList()) RemoveRoadInternal(node, other);
            adjacency.Remove(node);
        }

        private bool AddRoadInternal(MapNodeId a, MapNodeId b)
        {
            if (a == b || !roads.Add(Key(a, b))) return false;
            Adjacent(a).Add(b);
            Adjacent(b).Add(a);
            return true;
        }

        private bool RemoveRoadInternal(MapNodeId a, MapNodeId b)
        {
            if (!roads.Remove(Key(a, b))) return false;
            roadDifficulty.Remove(RoadKey.Of(a, b));
            if (adjacency.TryGetValue(a, out var setA)) setA.Remove(b);
            if (adjacency.TryGetValue(b, out var setB)) setB.Remove(a);
            return true;
        }

        private HashSet<MapNodeId> Adjacent(MapNodeId node)
        {
            if (!adjacency.TryGetValue(node, out var set)) adjacency[node] = set = new HashSet<MapNodeId>();
            return set;
        }

        // 무방향 - 종류·Id 순으로 정렬한 키.
        private static (MapNodeId A, MapNodeId B) Key(MapNodeId a, MapNodeId b)
        {
            var aFirst = a.Kind < b.Kind || (a.Kind == b.Kind && a.Id <= b.Id);
            return aFirst ? (a, b) : (b, a);
        }
    }
}
