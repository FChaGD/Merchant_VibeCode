using System;

namespace Game.Core
{
    /// <summary>
    /// "지금 있는 마을"에서 시설/카테고리가 제공되는지 판단한다. 제공 여부 데이터와 현재 위치 둘 다
    /// 아직 선택적 의존성(TryResolve)이라, null 처리와 "카테고리 = 제공 시설 1개 이상" 파생 규칙을
    /// 소비자(루트 depth 카테고리 열, 카테고리 depth 패널)마다 반복하지 않도록 여기 한 곳에 모았다.
    /// 둘 중 하나라도 없으면 전부 제공으로 본다(현재 동작 보존).
    /// </summary>
    public class CurrentTownFacilityFilter
    {
        private readonly ITownFacilityAvailabilityReader availabilityReader;
        private readonly ITripCurrentLocationReader currentLocationReader;

        public CurrentTownFacilityFilter(ITownFacilityAvailabilityReader availabilityReader, ITripCurrentLocationReader currentLocationReader)
        {
            this.availabilityReader = availabilityReader;
            this.currentLocationReader = currentLocationReader;
        }

        /// <summary>
        /// 현재 위치(마을)가 바뀔 때 발생한다. 구독은 현재 위치 저장소에 그대로 전달되므로, 필터 인스턴스가
        /// 씬 로드마다 새로 만들어져도 같은 핸들러로 -=/+= 하면 중복 구독되지 않는다.
        /// </summary>
        public event Action Changed
        {
            add
            {
                if (currentLocationReader != null) currentLocationReader.Changed += value;
            }
            remove
            {
                if (currentLocationReader != null) currentLocationReader.Changed -= value;
            }
        }

        public bool IsFacilityAvailable(string facilityId)
        {
            if (availabilityReader == null || currentLocationReader == null)
            {
                return true;
            }

            return availabilityReader.IsFacilityAvailable(currentLocationReader.CurrentCityId, facilityId);
        }

        public bool IsCategoryAvailable(string categoryId)
        {
            foreach (var facilityId in TownFacilityCatalog.GetFacilityIds(categoryId))
            {
                if (IsFacilityAvailable(facilityId))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
