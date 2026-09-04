using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 도착지 지정 로직(정식 기능, 기획 16번 §6). 출발지는 더 이상 이 클래스가 다루지 않는다 -
    /// ITripCurrentLocationReader로 자동 고정되므로, 상태로 들고 있을 필요 없이 매 클릭마다 읽기만
    /// 하면 된다. 옛 TripOriginDestinationAssigner(출발/도착 두 역할 모두를 다루던 상태 머신)를
    /// 대체한다 - 폐기 사유는 기획 16번 §3 참고.
    /// </summary>
    public class TripDestinationAssigner : MonoBehaviour, ITripDestinationAssigner, IManagedComponent
    {
        private ITripCurrentLocationReader currentLocationReader;

        public int? DestinationCityId { get; private set; }
        public bool IsAssigned => DestinationCityId != null;

        public event Action Changed;

        public void RegisterSelf(IDependencyRegistrar registrar) => registrar.Register<ITripDestinationAssigner>(this);

        // ITripCurrentLocationReader 자체는 DI에 등록되지 않는다 - InMemoryTripCurrentLocationRepository는
        // ITripCurrentLocationRepository로만 등록되므로 그 타입으로 조회해 읽기 전용 필드에 대입한다
        // (IFormationReader/IFormationRepository와 같은 판단 기준, CLAUDE.md 참고).
        public void ResolveDependencies(IDependencyRegistrar registrar) => currentLocationReader = registrar.Resolve<ITripCurrentLocationRepository>();

        public void HandleCityClicked(int cityId, ITripRouteReader routeReader)
        {
            var currentCityId = currentLocationReader.CurrentCityId;
            if (cityId == currentCityId)
            {
                return; // 현재 위치는 항상 무효 - 자기 자신으로의 상행은 의미가 없다(기획 §5/§6).
            }

            if (cityId == DestinationCityId)
            {
                DestinationCityId = null;
                Changed?.Invoke();
                return;
            }

            if (routeReader.IsReachable(currentCityId, cityId))
            {
                DestinationCityId = cityId;
                Changed?.Invoke();
            }
            // 도달 불가 - 무시(상태 변화 없음, 기획 §6).
        }

        public void HandleCityDeleted(int cityId)
        {
            if (DestinationCityId != cityId)
            {
                return;
            }

            DestinationCityId = null;
            Changed?.Invoke();
        }

        public void Reset()
        {
            if (DestinationCityId == null)
            {
                return;
            }

            DestinationCityId = null;
            Changed?.Invoke();
        }
    }
}
