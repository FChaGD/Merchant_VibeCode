using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 지도 테이블 무결성 검사(Docs/설계/69번 §3.2). 임포터가 엑셀을 읽은 뒤 호출하고, 테스트가 규칙을 직접 검증한다 - 에디터 어셈블리에
    /// 두면 EditMode 테스트가 부르기 번거로워 런타임 어셈블리의 순수 함수로 둔다. 오류 문구 목록을 돌려주고, 비어 있으면 통과.
    /// </summary>
    public static class WorldMapDataValidator
    {
        public readonly struct CityRow
        {
            public readonly int Id;
            public readonly int RegionId;
            public CityRow(int id, int regionId) { Id = id; RegionId = regionId; }
        }

        public readonly struct GateRow
        {
            public readonly int Id;
            public readonly int RegionId;
            public readonly int PairGateId;
            public GateRow(int id, int regionId, int pairGateId) { Id = id; RegionId = regionId; PairGateId = pairGateId; }
        }

        public static List<string> Validate(IReadOnlyCollection<int> regionIds, IReadOnlyList<CityRow> cities, IReadOnlyList<GateRow> gates, IReadOnlyList<(string A, string B)> roads)
        {
            var errors = new List<string>();
            var regionSet = new HashSet<int>();
            foreach (var id in regionIds)
            {
                if (!regionSet.Add(id)) errors.Add($"RegionData: 지역 Id {id}가 중복됐다.");
            }

            var cityRegion = new Dictionary<int, int>();
            foreach (var city in cities)
            {
                if (!cityRegion.TryAdd(city.Id, city.RegionId)) errors.Add($"CityData: 도시 Id {city.Id}가 중복됐다.");
                if (!regionSet.Contains(city.RegionId)) errors.Add($"CityData: 도시 {city.Id}의 지역 {city.RegionId}이(가) RegionData에 없다.");
            }

            var gateById = new Dictionary<int, GateRow>();
            foreach (var gate in gates)
            {
                if (!gateById.TryAdd(gate.Id, gate)) errors.Add($"GateData: 관문 Id {gate.Id}가 중복됐다.");
                if (!regionSet.Contains(gate.RegionId)) errors.Add($"GateData: 관문 {gate.Id}의 지역 {gate.RegionId}이(가) RegionData에 없다.");
            }

            // 관문 짝: 존재, 서로를 가리킴, 서로 다른 지역(기획 68번 §3.5).
            foreach (var gate in gates)
            {
                if (!gateById.TryGetValue(gate.PairGateId, out var pair))
                {
                    errors.Add($"GateData: 관문 {gate.Id}의 짝 {gate.PairGateId}이(가) 없다.");
                    continue;
                }
                if (pair.PairGateId != gate.Id) errors.Add($"GateData: 관문 {gate.Id}와 짝 {pair.Id}이(가) 서로를 가리키지 않는다.");
                if (pair.RegionId == gate.RegionId) errors.Add($"GateData: 관문 {gate.Id}와 짝 {pair.Id}이(가) 같은 지역 {gate.RegionId}에 있다.");
            }

            // 도로: 형식, 존재, 같은 지역(설계 69번 §9-4).
            foreach (var (a, b) in roads)
            {
                if (!TryResolve(a, cityRegion, gateById, out var regionA, out var errorA)) { errors.Add($"RoadData: {errorA}"); continue; }
                if (!TryResolve(b, cityRegion, gateById, out var regionB, out var errorB)) { errors.Add($"RoadData: {errorB}"); continue; }
                if (regionA != regionB) errors.Add($"RoadData: 도로 {a}-{b}의 두 끝점이 서로 다른 지역({regionA}, {regionB})에 있다 - 지역 사이는 관문 짝이 잇는다.");
            }

            return errors;
        }

        private static bool TryResolve(string text, Dictionary<int, int> cityRegion, Dictionary<int, GateRow> gateById, out int regionId, out string error)
        {
            regionId = 0;
            if (!MapNodeId.TryParse(text, out var node))
            {
                error = $"끝점 '{text}' 형식이 틀렸다 - C(도시)/G(관문) + 번호.";
                return false;
            }

            if (node.IsCity ? cityRegion.TryGetValue(node.Id, out regionId) : TryGateRegion(gateById, node.Id, out regionId))
            {
                error = null;
                return true;
            }

            error = $"끝점 '{text}'이(가) 가리키는 {(node.IsCity ? "도시" : "관문")}이(가) 없다.";
            return false;
        }

        private static bool TryGateRegion(Dictionary<int, GateRow> gateById, int gateId, out int regionId)
        {
            var found = gateById.TryGetValue(gateId, out var gate);
            regionId = found ? gate.RegionId : 0;
            return found;
        }
    }
}
