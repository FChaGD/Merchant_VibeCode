namespace Game.Core
{
    /// <summary>
    /// Field 상행의 시작·종료·최종 도착 처리(Docs/Refactor/2026-10-26_씬전용.md C-1, SS-1). 이동 화면 조율자(FieldUIController)가 화면 바인딩과 함께
    /// 직접 하던 상태 변경을 떼어 냈다 - 화면 없이 테스트할 수 있고, 조율자는 화면·버튼·게이지만 다룬다.
    /// Field 로드마다 배선이 새로 만든다. 상태 저장소는 Bootstrap 상주지만 이 객체는 구독하지 않고 호출만 하므로 해제할 것이 없다.
    /// 의존성은 전부 선택적이다 - 없으면 그 처리만 건너뛴다(예전 null-조건부 호출과 같음).
    /// </summary>
    public sealed class FieldTripLifecycle : ITripFinisher
    {
        private readonly IUnitConditionRepository conditions;
        private readonly ITripMilestoneSink milestones;
        private readonly TripEndProcessor endProcessor;
        private readonly IFieldFormationActivityRepository activities;
        private readonly ITripCurrentLocationRepository currentLocation;
        private readonly ITripDestinationAssigner destination;
        private readonly ITripArrivalSink arrival;

        public FieldTripLifecycle(IUnitConditionRepository conditions, ITripMilestoneSink milestones, TripEndProcessor endProcessor, IFieldFormationActivityRepository activities, ITripCurrentLocationRepository currentLocation, ITripDestinationAssigner destination, ITripArrivalSink arrival = null)
        {
            this.conditions = conditions;
            this.milestones = milestones;
            this.endProcessor = endProcessor;
            this.activities = activities;
            this.currentLocation = currentLocation;
            this.destination = destination;
            this.arrival = arrival;
        }

        /// <summary>상행 시작(화면이 드러난 뒤) - 전원 만피로 출발(기획 13번 §4-1, 설계 15번 §4), 이번 상행 기록·판정 시계·정지 원인 초기화(설계 87번 §6.4).</summary>
        public void BeginTrip()
        {
            conditions?.ResetAllToFull();
            milestones?.Raise(TripMilestone.TripStarted);
        }

        /// <summary>상행 종료 = 사망 제거 + 전원 회복(기획 13번 §4-3, 설계 81번 §6.4). 처리기가 없어도 회복은 유지한다.</summary>
        public void FinishTrip()
        {
            if (endProcessor != null) endProcessor.Finish();
            else conditions?.ResetAllToFull();
            milestones?.Raise(TripMilestone.TripEnded);
        }

        /// <summary>
        /// 최종 도착. 진행 중이던 배치·이동을 도착지로 즉시 확정한다 - 그러지 않으면 이동 중이던 유닛이 출발 칸에 남아 Hub에서 되돌아간 것처럼
        /// 보였다(2026-09-06 실전 확인, 설계 25번 §3.2). 현재 위치를 도착지로 바꾸고(기획 16번 §4) 다음 상행 준비가 빈 도착지로 시작하게 한다.
        /// 최종 도착 신호(퀘스트 판정, 설계 107번 §6.2)는 위치 갱신 뒤에 보낸다 - 수신자가 현재 위치를 읽어도 도착지와 일치하게.
        /// </summary>
        public void CompleteArrival()
        {
            activities?.ForceCompleteAll();
            FinishTrip();
            if (destination?.DestinationCityId is { } destinationCityId)
            {
                currentLocation?.SetCurrentCity(destinationCityId);
                destination.Reset();
                arrival?.NotifyFinalArrival(destinationCityId);
            }
        }
    }
}
