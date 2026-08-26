using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 지역 시스템이 아직 없어, 상행 준비 UI를 테스트하기 위한 임시 상행 요약 제공자.
    /// 실제 데이터 시스템이 생기면 대체된다.
    /// </summary>
    public class PlaceholderTripInfoProvider : MonoBehaviour, ITripInfoProvider, IManagedComponent
    {
        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<ITripInfoProvider>(this);
        }

        public void ResolveDependencies(IDependencyRegistrar registrar)
        {
            // 다른 매니저에 대한 의존성이 없다.
        }

        public TripSummary GetTripSummary() => new(
            estimatedDurationDistanceText: "값 없음",
            dangerText: "값 없음",
            rewardText: "값 없음");
    }
}
