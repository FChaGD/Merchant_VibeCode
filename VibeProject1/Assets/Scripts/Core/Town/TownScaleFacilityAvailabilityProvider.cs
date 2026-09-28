using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 마을 규모로 시설 제공 여부를 정하는 제공자(Docs/기획/57번, 설계 58번 §5.4). 기존 계약(ITownFacilityAvailabilityReader)을 그대로
    /// 구현해 마을 화면 코드는 바뀌지 않는다 - 구현만 임시 제공자(모든 마을 공통 체크 목록)에서 교체했다.
    /// 규모 목록과 시설별 최소 규모는 테이블이 아니라 이 컴포넌트의 인스펙터 값이다(기획 57번 §3 사용자 결정). 인스톨러가
    /// TownScaleDefaults로 빈 항목만 채우고, 사용자가 바꾼 값은 유지한다. 마을별 규모는 도시 테이블(City.xlsx CityData의 Scale 열)에서 온다.
    /// </summary>
    public class TownScaleFacilityAvailabilityProvider : MonoBehaviour, ITownFacilityAvailabilityReader, ITownScaleReader, IManagedComponent
    {
        [Serializable]
        private class ScaleEntry
        {
            public string id = string.Empty;
            public string label = string.Empty;
        }

        [Serializable]
        private class FacilityMinScaleEntry
        {
            public string facilityId = string.Empty;
            public string minScaleId = string.Empty;
        }

        [Tooltip("작은 규모 → 큰 규모 순서. 순서가 곧 등급이다 - 규모를 추가하려면 크기 순서에 맞는 위치에 끼운다.")]
        [SerializeField] private List<ScaleEntry> scales = new();
        [SerializeField] private List<FacilityMinScaleEntry> facilityMinScales = new();
        [SerializeField] private TripCityMapAsset cityMap;

        private TownScaleFacilityRule rule;

        public IReadOnlyList<TownScale> Scales => Rule.Scales;

        private TownScaleFacilityRule Rule => rule ??= BuildRule();

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<ITownFacilityAvailabilityReader>(this);
            registrar.Register<ITownScaleReader>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            // 다른 매니저에 대한 의존성이 없다. 판정 규칙은 인스펙터 값이 확정된 뒤 첫 조회 시점에 만든다.
            rule = null;
        }

        public bool IsFacilityAvailable(int cityId, string facilityId) => Rule.IsFacilityAvailable(cityId, facilityId);

        public TownScale GetScale(int cityId) => Rule.GetScale(cityId);

        private TownScaleFacilityRule BuildRule()
        {
            var scaleList = new List<(string Id, string Label)>(scales.Count);
            foreach (var entry in scales) scaleList.Add((entry.id, entry.label));

            var minScales = new Dictionary<string, string>();
            foreach (var entry in facilityMinScales)
            {
                if (!string.IsNullOrEmpty(entry.facilityId)) minScales[entry.facilityId] = entry.minScaleId;
            }

            var scaleByCityId = new Dictionary<int, string>();
            if (cityMap != null)
            {
                foreach (var city in cityMap.Cities) scaleByCityId[city.CityId] = city.Scale;
            }
            else
            {
                Debug.LogWarning($"{nameof(TownScaleFacilityAvailabilityProvider)}: 도시 자산이 배선되지 않아 모든 마을을 가장 작은 규모로 취급한다(Tools > Game > Build Bootstrap Scene).", this);
            }

            return new TownScaleFacilityRule(scaleList, minScales, scaleByCityId, message => Debug.LogWarning($"{nameof(TownScaleFacilityAvailabilityProvider)}: {message}", this));
        }
    }
}
