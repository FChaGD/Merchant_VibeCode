using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    [Serializable]
    public struct TripCityMapCityEntry
    {
        public int CityId;
        public Vector2 MapPosition;
        public string Scale; // 마을 규모 Id(Docs/기획/57번). 빈 값 = 미지정 - 판정은 TownScaleFacilityRule이 가장 작은 규모로 처리한다
    }

    [Serializable]
    public struct TripCityMapRouteEntry
    {
        public int CityIdA;
        public int CityIdB;
    }

    /// <summary>
    /// 도시 좌표+경로 연결의 저장된 스냅샷(Docs/기획/15번, Docs/설계/19번 §4) - 배치 도구(팔레트 드래그
    /// 등)는 디버그 성격을 유지하지만, 이 에셋 타입 자체는 향후 정식 지역 시스템이 이어받을 "기본 도시
    /// 지도" 데이터라 #if UNITY_EDITOR로 감싸지 않는다. TripCity(디버그 전용 struct)를 그대로 쓰지
    /// 않고 독립된 직렬화 구조체를 쓴다 - 에셋이 디버그 어셈블리 경계에 묶이지 않게 하기 위함.
    /// 에디터에선 TripCityMapPersistence(Core/Debug/Trip/)가, 런타임에선 마을 규모 판정(TownScaleFacilityAvailabilityProvider)이
    /// 읽는다. 원본은 Assets/Table/City/City.xlsx(CityData/CityDetailData 시트, Docs/설계/58번 §2).
    /// </summary>
    [CreateAssetMenu(fileName = "TripCityMap", menuName = "Game/Trip/Trip City Map")]
    public class TripCityMapAsset : ScriptableObject
    {
        [SerializeField] private List<TripCityMapCityEntry> cities = new();
        [SerializeField] private List<TripCityMapRouteEntry> routes = new();

        public IReadOnlyList<TripCityMapCityEntry> Cities => cities;
        public IReadOnlyList<TripCityMapRouteEntry> Routes => routes;
    }
}
