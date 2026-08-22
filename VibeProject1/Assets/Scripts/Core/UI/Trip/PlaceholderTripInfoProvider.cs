using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 지역 시스템이 아직 없어, 상행 준비 UI를 테스트하기 위한 임시 출발지/도착지/상행 요약 제공자.
    /// 실제 데이터 시스템이 생기면 대체된다.
    /// </summary>
    public class PlaceholderTripInfoProvider : MonoBehaviour, ITripInfoProvider, IManagedComponent
    {
        [SerializeField] private Sprite originIcon;
        [SerializeField] private Sprite destinationIcon;

        private ITripLocationInfo origin;
        private ITripLocationInfo destination;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<ITripInfoProvider>(this);
        }

        public void ResolveDependencies(IDependencyRegistrar registrar)
        {
            origin = new PlaceholderTripLocationInfo("origin-hub", "Hub 지역", "상행이 출발하는 지역입니다.", originIcon);
            destination = new PlaceholderTripLocationInfo("destination-01", "임시 목적지", "상행이 향할 지역입니다.", destinationIcon);
        }

        public ITripLocationInfo GetOrigin() => origin;

        public ITripLocationInfo GetDestination() => destination;

        public TripSummary GetTripSummary() => new(
            estimatedDurationDistanceText: "값 없음",
            dangerText: "값 없음",
            rewardText: "값 없음");
    }
}
