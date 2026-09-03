#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 디버그로 배치한 도시를 세션(TripMapInteractionCoordinator의 생명주기) 동안 보관하는 인메모리
    /// 저장소. 앱 재시작과 함께 사라진다 - 실제 지역 시스템이 생기면 대체 대상. ID 조회는 Dictionary로
    /// O(1) 처리한다. Id가 int라(기획 15번 §8.2, 설계 20번 §9) 순번 재개 로직이 문자열 파싱 없이
    /// 단순 비교로 끝난다.
    /// </summary>
    internal class InMemoryTripCityRepository : ITripCityRepository
    {
        private readonly Dictionary<int, TripCity> citiesById = new();
        private int nextSequence = 1;

        public event Action<int> CityRemoved;

        public int Add(Vector2 mapPosition)
        {
            var id = nextSequence++;
            citiesById[id] = new TripCity(id, mapPosition);
            return id;
        }

        public void AddWithId(int cityId, Vector2 mapPosition)
        {
            citiesById[cityId] = new TripCity(cityId, mapPosition);

            // 불러온 뒤 팔레트로 새 도시를 추가해도 기존 Id와 충돌하지 않게 순번을 재개한다
            // (Docs/설계/19번 §3, 20번 §9.2).
            if (cityId >= nextSequence)
            {
                nextSequence = cityId + 1;
            }
        }

        public IReadOnlyList<TripCity> GetAll()
        {
            var result = new List<TripCity>(citiesById.Count);
            result.AddRange(citiesById.Values);
            return result;
        }

        public void UpdatePosition(int cityId, Vector2 mapPosition)
        {
            if (citiesById.ContainsKey(cityId))
            {
                citiesById[cityId] = new TripCity(cityId, mapPosition);
            }
        }

        public void Remove(int cityId)
        {
            if (citiesById.Remove(cityId))
            {
                CityRemoved?.Invoke(cityId);
            }
        }

        public void Clear()
        {
            var ids = new List<int>(citiesById.Keys);
            citiesById.Clear();
            foreach (var id in ids)
            {
                CityRemoved?.Invoke(id);
            }
        }
    }
}
#endif
