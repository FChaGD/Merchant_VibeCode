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
    /// TripCityMapPersistence(Core/Debug/Trip/, 에디터 전용)만 이 에셋을 쓰고 읽는다.
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
