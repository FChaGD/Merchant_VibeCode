using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 디버그로 배치한 도시를 세션(TripMapInteractionCoordinator의 생명주기) 동안 보관하는 인메모리
    /// 저장소. 앱 재시작과 함께 사라진다 - 실제 지역 시스템이 생기면 대체 대상. ID 조회는 Dictionary로
    /// O(1) 처리한다.
    /// </summary>
    internal class InMemoryTripCityRepository : ITripCityRepository
    {
        private readonly Dictionary<string, TripCity> citiesById = new();
        private int nextSequence = 1;

        public event Action<string> CityRemoved;

        public IReadOnlyList<TripCity> GetAll()
        {
            var list = new List<TripCity>(citiesById.Count);
            list.AddRange(citiesById.Values);
            return list;
        }

        public bool TryGet(string cityId, out TripCity city) => citiesById.TryGetValue(cityId, out city);

        public string Add(Vector2 mapPosition)
        {
            var id = $"debug-city-{nextSequence++}";
            citiesById[id] = new TripCity(id, mapPosition);
            return id;
        }

        public void UpdatePosition(string cityId, Vector2 mapPosition)
        {
            if (citiesById.ContainsKey(cityId))
            {
                citiesById[cityId] = new TripCity(cityId, mapPosition);
            }
        }

        public void Remove(string cityId)
        {
            if (citiesById.Remove(cityId))
            {
                CityRemoved?.Invoke(cityId);
            }
        }

        public void Clear()
        {
            var ids = new List<string>(citiesById.Keys);
            citiesById.Clear();
            foreach (var id in ids)
            {
                CityRemoved?.Invoke(id);
            }
        }
    }
}
