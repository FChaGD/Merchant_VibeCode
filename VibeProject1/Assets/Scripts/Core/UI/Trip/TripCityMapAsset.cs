using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    [Serializable]
    public struct TripCityMapRegionEntry
    {
        public int RegionId;
        public Vector2 Size; // 지역 지도 크기(Docs/기획/68번 §4-2 — 전 지역 2400 × 2400으로 시작)
    }

    [Serializable]
    public struct TripCityMapCityEntry
    {
        public int CityId;
        public int RegionId;
        public Vector2 MapPosition; // 그 지역 지도 콘텐츠 좌표(중심 기준)
        public string Scale; // 마을 규모 Id(Docs/기획/57번). 빈 값 = 미지정 - 판정은 TownScaleFacilityRule이 가장 작은 규모로 처리한다
    }

    [Serializable]
    public struct TripCityMapGateEntry
    {
        public int GateId;
        public int RegionId;
        public Vector2 MapPosition;
        public int PairGateId; // 다른 지역에 있는 짝 관문 - 둘이 서로를 가리킨다(Docs/기획/68번 §3.5)
    }

    [Serializable]
    public struct TripCityMapRoadEntry
    {
        public MapNodeId A;
        public MapNodeId B;
    }

    /// <summary>
    /// 월드 지도 데이터의 컴파일된 스냅샷(Docs/설계/69번 §3.3) - 지역·도시·관문(지역 아이콘)·도로. 원본은
    /// Assets/Table/City/City.xlsx(RegionData/CityData/GateData/RoadData 시트)이고 CityTableImporter가 채운다. 런타임에서는
    /// WorldMapProvider(지도 모델)와 마을 규모 판정(TownScaleFacilityAvailabilityProvider)이 읽는다. 타입 이름은 기존(TripCityMap)을
    /// 유지한다 - 규모 판정의 인스펙터 배선을 그대로 두기 위함(설계 69번 §9-1).
    /// 도로 끝점은 도시·관문을 모두 가리킬 수 있어 MapNodeId로 둔다(예전엔 도시 Id 쌍).
    /// </summary>
    [CreateAssetMenu(fileName = "TripCityMap", menuName = "Game/Trip/Trip City Map")]
    public class TripCityMapAsset : ScriptableObject
    {
        [SerializeField] private List<TripCityMapRegionEntry> regions = new();
        [SerializeField] private List<TripCityMapCityEntry> cities = new();
        [SerializeField] private List<TripCityMapGateEntry> gates = new();
        [SerializeField] private List<TripCityMapRoadEntry> roads = new();

        public IReadOnlyList<TripCityMapRegionEntry> Regions => regions;
        public IReadOnlyList<TripCityMapCityEntry> Cities => cities;
        public IReadOnlyList<TripCityMapGateEntry> Gates => gates;
        public IReadOnlyList<TripCityMapRoadEntry> Roads => roads;
    }
}
