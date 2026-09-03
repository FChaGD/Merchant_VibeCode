using System;

namespace Game.Core
{
    /// <summary>
    /// 출발/도착 지정 상태 머신(정식 기능, 02번 기획 3.1/3.1.1절). ITripRouteReader에만 의존해(DIP)
    /// 지금은 디버그 경로 연결(04번 기획)이 그 데이터를 대지만, 실제 경로 시스템이 생겨도 이 클래스는
    /// 손댈 필요가 없다 - 데이터 소스 구현체만 교체하면 된다.
    ///
    /// awaitingRole 필드가 왜 필요한지: 기획의 "반대편이 미배정일 때" 규칙(클릭한 도시가 먼저
    /// 확정되고, *다음* 클릭이 반대편을 채운다)은 activePanelRole 하나만으로는 표현할 수 없다 - 첫
    /// 클릭 직후에도 여전히 같은 역할 모드인 것처럼 보이면 두 번째 클릭이 같은 역할을 다시 덮어쓰게
    /// 된다. "지금 이 클릭이 채워야 할 역할"을 별도로 추적해야 한다(설계 문서 03번 4.1절 참고).
    ///
    /// 도시 Id는 int? - 도시 식별자가 정수로 통일되면서(기획 15번 §8.2, 설계 20번 §9) "미배정"을
    /// 표현하던 string의 null을 nullable int로 그대로 대체했다.
    /// </summary>
    internal class TripOriginDestinationAssigner : ITripOriginDestinationAssigner
    {
        private readonly ITripRouteReader routeReader;

        private TripRole? activePanelRole;
        private TripRole? awaitingRole;

        public TripOriginDestinationAssigner(ITripRouteReader routeReader)
        {
            this.routeReader = routeReader;
        }

        public int? OriginCityId { get; private set; }
        public int? DestinationCityId { get; private set; }
        public bool IsBothAssigned => OriginCityId != null && DestinationCityId != null;

        public event Action Changed;

        public void HandleCityClicked(int cityId)
        {
            if (awaitingRole.HasValue)
            {
                HandleAwaitingPick(cityId);
                return;
            }

            if (activePanelRole.HasValue)
            {
                HandlePanelModePick(cityId);
                return;
            }

            HandleBaseClick(cityId);
        }

        public void HandlePanelClicked(TripRole role)
        {
            if (awaitingRole.HasValue)
            {
                // 대기 중(반대편 지정 대기) 패널을 다시 클릭 = 대기만 취소, 이미 확정된 값은 유지.
                // 어느 쪽 패널을 눌러도 동일하게 처리한다 - 대기 상태에서는 "지금 무엇을 편집 중인지"가
                // 이미 모호해지므로 안전하게 전체 취소로 통일한다.
                awaitingRole = null;
                activePanelRole = null;
                Changed?.Invoke();
                return;
            }

            // 켜져 있는 패널을 다시 클릭하면 취소, 다른 패널을 클릭하면 그쪽으로 전환(상호 배타).
            activePanelRole = activePanelRole == role ? (TripRole?)null : role;
        }

        // 상행 준비 UI "종료" 시 배정을 초기화하는 용도(배치 UI 왕복 중에는 호출되지 않는다 - 그 경우엔
        // 배정이 유지되어야 하므로). Changed를 태워 정보 패널/지도 강조가 자동으로 함께 비워지게 한다.
        public void Reset()
        {
            if (OriginCityId == null && DestinationCityId == null && activePanelRole == null && awaitingRole == null)
            {
                return;
            }

            OriginCityId = null;
            DestinationCityId = null;
            activePanelRole = null;
            awaitingRole = null;
            Changed?.Invoke();
        }

        public void HandleCityDeleted(int cityId)
        {
            var changed = false;

            if (OriginCityId == cityId)
            {
                OriginCityId = null;
                changed = true;
            }

            if (DestinationCityId == cityId)
            {
                DestinationCityId = null;
                changed = true;
            }

            if (changed)
            {
                Changed?.Invoke();
            }
        }

        private void HandleBaseClick(int cityId)
        {
            if (OriginCityId == null)
            {
                OriginCityId = cityId;
            }
            else if (DestinationCityId == null)
            {
                if (cityId == OriginCityId)
                {
                    OriginCityId = null;
                }
                else if (routeReader.HasRoute(OriginCityId.Value, cityId))
                {
                    DestinationCityId = cityId;
                }
                else
                {
                    return; // 경로 없음 - 무시, 상태 변화 없음
                }
            }
            else
            {
                OriginCityId = cityId;
                DestinationCityId = null;
            }

            Changed?.Invoke();
        }

        private void HandlePanelModePick(int clickedCityId)
        {
            var role = activePanelRole.Value;
            var oppositeRole = Opposite(role);
            var current = Get(role);
            var opposite = Get(oppositeRole);

            if (clickedCityId == current)
            {
                Set(role, null);
                activePanelRole = null;
                Changed?.Invoke();
                return;
            }

            if (opposite != null && clickedCityId == opposite)
            {
                // 역할 교환(swap) - 둘 다 이미 배정돼 있었다는 것 자체가 서로 경로가 있다는 뜻이므로
                // 게이팅 검사 없이 무조건 성공한다.
                Set(role, opposite);
                Set(oppositeRole, current);
                activePanelRole = null;
                Changed?.Invoke();
                return;
            }

            if (opposite != null)
            {
                if (routeReader.HasRoute(clickedCityId, opposite.Value))
                {
                    Set(role, clickedCityId);
                    activePanelRole = null;
                    Changed?.Invoke();
                }
                // 경로 없음 - 거부, 모드는 유지(재시도 가능)
                return;
            }

            // 반대편이 미배정 - 비교 대상이 없으므로 게이팅 없이 즉시 확정하고, 다음 클릭이 반대편을
            // 채우도록 대기 상태로 전환한다.
            Set(role, clickedCityId);
            awaitingRole = oppositeRole;
            Changed?.Invoke();
        }

        private void HandleAwaitingPick(int clickedCityId)
        {
            var role = awaitingRole.Value;
            var opposite = Get(Opposite(role));

            if (opposite != null && routeReader.HasRoute(clickedCityId, opposite.Value))
            {
                Set(role, clickedCityId);
                awaitingRole = null;
                activePanelRole = null;
                Changed?.Invoke();
            }
            // 실패 시 awaitingRole 유지 - 이미 확정된 반대편 값은 그대로(부분 성공), 다른 도시로 바로 재시도 가능.
        }

        private int? Get(TripRole role) => role == TripRole.Origin ? OriginCityId : DestinationCityId;

        private void Set(TripRole role, int? cityId)
        {
            if (role == TripRole.Origin)
            {
                OriginCityId = cityId;
            }
            else
            {
                DestinationCityId = cityId;
            }
        }

        private static TripRole Opposite(TripRole role) => role == TripRole.Origin ? TripRole.Destination : TripRole.Origin;
    }
}
