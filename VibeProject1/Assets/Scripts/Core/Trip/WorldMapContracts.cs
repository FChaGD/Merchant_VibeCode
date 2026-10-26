using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>지역 1개(Docs/기획/68번 §3.1). 이름은 엑셀 RegionStrings에서 온다.</summary>
    public sealed class WorldRegion
    {
        public int Id { get; }
        public string Name { get; internal set; }
        public Vector2 Size { get; }

        public WorldRegion(int id, string name, Vector2 size)
        {
            Id = id;
            Name = name;
            Size = size;
        }
    }

    /// <summary>도시 1개. 좌표는 속한 지역 지도의 콘텐츠 좌표(중심 기준). 이름·설명·규모는 엑셀 값(저장 시 보존용).</summary>
    public sealed class WorldCity
    {
        public int Id { get; }
        public int RegionId { get; }
        public Vector2 Position { get; internal set; }
        public string Name { get; }
        public string Description { get; }
        public string Scale { get; }

        public WorldCity(int id, int regionId, Vector2 position, string name, string description, string scale)
        {
            Id = id;
            RegionId = regionId;
            Position = position;
            Name = name;
            Description = description;
            Scale = scale ?? string.Empty;
        }
    }

    /// <summary>관문(지역 아이콘) 1개. 다른 지역의 짝 관문과 서로를 가리킨다 - 둘이 지역 사이 통로다(기획 68번 §3.3·§3.5).</summary>
    public sealed class WorldGate
    {
        public int Id { get; }
        public int RegionId { get; }
        public Vector2 Position { get; internal set; }
        public int PairGateId { get; }

        public WorldGate(int id, int regionId, Vector2 position, int pairGateId)
        {
            Id = id;
            RegionId = regionId;
            Position = position;
            PairGateId = pairGateId;
        }
    }

    /// <summary>
    /// 월드 지도 읽기 계약(Docs/설계/69번 §4.2). 지도 표시(TripMapPresenter)와 저장이 쓴다. 지역별 조회는 지역마다 따로 둔 목록을
    /// 돌므로 지도를 그릴 때 전체를 훑지 않는다.
    /// </summary>
    public interface IWorldMapReader
    {
        IReadOnlyList<WorldRegion> Regions { get; }
        bool TryGetRegion(int regionId, out WorldRegion region);
        bool TryGetCity(int cityId, out WorldCity city);
        bool TryGetGate(int gateId, out WorldGate gate);
        IEnumerable<WorldCity> GetCities(int regionId);
        IEnumerable<WorldGate> GetGates(int regionId);
        IEnumerable<(MapNodeId A, MapNodeId B)> GetRoads(int regionId);
        IEnumerable<MapNodeId> GetConnectedNodes(MapNodeId node);
        bool TryGetNodePosition(MapNodeId node, out int regionId, out Vector2 position);
        /// <summary>도로 난이도 기본값(데이터). 게임 중 바뀐 값은 IRoadDifficultyReader로 읽는다(Docs/설계/76번 §4.4).</summary>
        int GetRoadBaseDifficulty(RoadKey road);

        IEnumerable<WorldCity> AllCities { get; }
        IEnumerable<WorldGate> AllGates { get; }
        IEnumerable<(MapNodeId A, MapNodeId B)> AllRoads { get; }

        /// <summary>도시·관문·도로·지역이 늘거나 줄었을 때(디버그 편집). 위치 이동은 NodeMoved.</summary>
        event Action Changed;
        event Action<MapNodeId, Vector2> NodeMoved;
    }

    public enum RegionRemovalCheck
    {
        Allowed,
        NotFound,
        LastRegion,
        HasCurrentLocation,
    }

    /// <summary>
    /// 월드 지도 편집 계약(디버그 도구 전용, 설계 69번 §4.2). 규칙(관문 짝 자동 생성·동반 삭제, 같은 지역 도로만, 지역 삭제 조건)은
    /// 이 계약 뒤의 모델이 지킨다 - 디버그 UI가 규칙을 다시 구현하지 않게.
    /// </summary>
    public interface IWorldMapEditor : IWorldMapReader
    {
        int AddCity(int regionId, Vector2 position);
        void MoveNode(MapNodeId node, Vector2 position);
        bool RemoveCity(int cityId);
        /// <summary>관문을 놓고 대상 지역 지도 중앙에 짝을 만든다(기획 68번 §3.5). 대상이 같은 지역이거나 없으면 -1.</summary>
        int AddGatePair(int regionId, Vector2 position, int targetRegionId);
        /// <summary>관문과 그 짝, 두 관문에 이어진 도로를 함께 지운다.</summary>
        bool RemoveGate(int gateId);
        /// <summary>같은 지역 끝점끼리만(설계 69번 §9-4). 자기 자신·중복·없는 끝점은 false.</summary>
        bool TryAddRoad(MapNodeId a, MapNodeId b);
        bool RemoveRoad(MapNodeId a, MapNodeId b);
        void ClearCities(int regionId);
        void ClearRoads(int regionId);
        int AddRegion();
        RegionRemovalCheck CheckRegionRemoval(int regionId, int currentCityId);
        /// <summary>그 지역 도시·관문, 다른 지역 짝 관문, 연결 도로를 함께 지운다(기획 68번 §3.6). 조건에 걸리면 false.</summary>
        bool RemoveRegion(int regionId, int currentCityId);

        /// <summary>도시가 지워질 때마다(개별·전체·지역 삭제) - 도착지 해제용.</summary>
        event Action<int> CityRemoved;
    }
}
