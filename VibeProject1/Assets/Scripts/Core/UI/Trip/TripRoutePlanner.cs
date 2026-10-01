using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 현재 위치 → 도착지 최단 경로를 구하고 도시마다 잘라 구간 목록을 만든다(Docs/설계/76번 §4.3). 간선 = 도로(가중치 = 같은 지역 안 두 끝점의
    /// 직선 거리) + 관문 짝(가중치 0 - 서로 다른 지도라 거리를 잴 수 없다). 도달 판정(WorldMap.IsReachable)과 같은 그래프라 결과가 일치한다.
    /// 구간 = 다음 도시까지(기획 75번 §4-15) - 사이에 관문이 끼면 지역을 넘는 구간으로 긴 거리 공식, 난이도는 구간 안 도로들의 길이 가중 평균
    /// (설계 76번 §10-2). 노드가 수십 개라 단순 다익스트라(선형 최소 탐색)로 충분하다.
    /// </summary>
    public sealed class TripRoutePlanner : ITripPlanner
    {
        private readonly IWorldMapReader map;
        private readonly IRoadDifficultyReader difficulties;
        private readonly Func<TripTravelSettings> settings;

        public TripRoutePlanner(IWorldMapReader map, IRoadDifficultyReader difficulties, Func<TripTravelSettings> settings)
        {
            this.map = map;
            this.difficulties = difficulties;
            this.settings = settings;
        }

        public bool TryPlan(int fromCityId, int toCityId, out TripPlan plan)
        {
            plan = null;
            if (fromCityId == toCityId || !map.TryGetCity(fromCityId, out _) || !map.TryGetCity(toCityId, out _)) return false;
            if (!TryFindShortestPath(MapNodeId.City(fromCityId), MapNodeId.City(toCityId), out var path)) return false;

            plan = new TripPlan(BuildLegs(path, settings()));
            return true;
        }

        private bool TryFindShortestPath(MapNodeId start, MapNodeId goal, out List<MapNodeId> path)
        {
            var distance = new Dictionary<MapNodeId, float> { [start] = 0f };
            var previous = new Dictionary<MapNodeId, MapNodeId>();
            var open = new List<MapNodeId> { start };
            var closed = new HashSet<MapNodeId>();

            while (open.Count > 0)
            {
                var bestIndex = 0;
                for (var i = 1; i < open.Count; i++)
                {
                    if (distance[open[i]] < distance[open[bestIndex]]) bestIndex = i;
                }
                var current = open[bestIndex];
                open.RemoveAt(bestIndex);
                if (current == goal) break;
                if (!closed.Add(current)) continue;

                foreach (var (next, cost) in Edges(current))
                {
                    if (closed.Contains(next)) continue;
                    var candidate = distance[current] + cost;
                    if (distance.TryGetValue(next, out var known) && known <= candidate) continue;
                    distance[next] = candidate;
                    previous[next] = current;
                    if (!open.Contains(next)) open.Add(next);
                }
            }

            path = null;
            if (!distance.ContainsKey(goal)) return false;
            path = new List<MapNodeId> { goal };
            while (path[path.Count - 1] != start) path.Add(previous[path[path.Count - 1]]);
            path.Reverse();
            return true;
        }

        private IEnumerable<(MapNodeId Next, float Cost)> Edges(MapNodeId node)
        {
            foreach (var next in map.GetConnectedNodes(node)) yield return (next, RoadLength(node, next));
            if (!node.IsCity && map.TryGetGate(node.Id, out var gate) && map.TryGetGate(gate.PairGateId, out _))
            {
                yield return (MapNodeId.Gate(gate.PairGateId), 0f);
            }
        }

        private float RoadLength(MapNodeId a, MapNodeId b)
            => map.TryGetNodePosition(a, out _, out var pa) && map.TryGetNodePosition(b, out _, out var pb) ? Vector2.Distance(pa, pb) : 0f;

        private List<TripLeg> BuildLegs(List<MapNodeId> path, TripTravelSettings travel)
        {
            var legs = new List<TripLeg>();
            var legStart = 0;
            for (var i = 1; i < path.Count; i++)
            {
                if (!path[i].IsCity) continue;
                legs.Add(BuildLeg(path, legStart, i, travel));
                legStart = i;
            }
            return legs;
        }

        private TripLeg BuildLeg(List<MapNodeId> path, int startIndex, int endIndex, TripTravelSettings travel)
        {
            var distance = 0f;
            var weightedDifficulty = 0f;
            var crossesRegion = false;
            for (var i = startIndex; i < endIndex; i++)
            {
                var a = path[i];
                var b = path[i + 1];
                if (!a.IsCity && !b.IsCity && IsGatePair(a, b))
                {
                    crossesRegion = true; // 관문 짝 이동 - 거리 0
                    continue;
                }
                var length = RoadLength(a, b);
                distance += length;
                weightedDifficulty += difficulties.GetDifficulty(RoadKey.Of(a, b)) * length;
            }

            // 길이 0인 도로만 있는 비정상 데이터면 첫 도로 난이도로 대신한다.
            var difficulty = distance > 0f
                ? Mathf.RoundToInt(weightedDifficulty / distance)
                : difficulties.GetDifficulty(RoadKey.Of(path[startIndex], path[startIndex + 1]));
            difficulty = TripTravelRules.ClampDifficulty(difficulty);

            var band = travel.DistanceBands[TripTravelRules.ResolveBandIndex(travel, distance, crossesRegion)];
            var grade = TripTravelRules.ResolveGrade(travel, difficulty);
            var duration = TripTravelRules.ComputeDurationSeconds(travel, distance, difficulty, crossesRegion);
            return new TripLeg(path[startIndex].Id, path[endIndex].Id, distance, difficulty, crossesRegion, band.Name, grade.Name, duration, grade.EncountersPerFiveMinutes);
        }

        private bool IsGatePair(MapNodeId a, MapNodeId b) => map.TryGetGate(a.Id, out var gate) && gate.PairGateId == b.Id;
    }
}
