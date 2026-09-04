using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// ITripCurrentLocationRepository의 인메모리 구현(설계 21번) - InMemoryUnitConditionRepository와
    /// 같은 성격(Bootstrap 상주, 앱 재시작 시 소멸 = 이번 Play 세션 동안만 유지, 기획 16번 §4에서
    /// 사용자 확정). 정식 게임 상태라 #if UNITY_EDITOR로 감싸지 않는다 - 실제 게임 빌드에도 항상
    /// 존재하지만, 지금은 도시 지도 자체가 에디터 전용(03/04번 기획)이라 빌드에서는 값이 바뀔 방법이
    /// 없을 뿐이다.
    /// </summary>
    public class InMemoryTripCurrentLocationRepository : MonoBehaviour, ITripCurrentLocationRepository, IManagedComponent
    {
        // 임시값 - 실제 홈/시작 도시를 어떻게 정할지는 후속 과제(기획 16번 §4/§8).
        private const int InitialHomeCityId = 1;

        private int currentCityId = InitialHomeCityId;

        public int CurrentCityId => currentCityId;

        public event Action Changed;

        public void RegisterSelf(IDependencyRegistrar registrar) => registrar.Register<ITripCurrentLocationRepository>(this);

        public void ResolveDependencies(IDependencyRegistrar registrar)
        {
        }

        public void SetCurrentCity(int cityId)
        {
            if (currentCityId == cityId)
            {
                return;
            }

            currentCityId = cityId;
            Changed?.Invoke();
        }
    }
}
