using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 랜덤 인카운터를 판정한다. 확률은 현재 상행 구간 난이도의 5분 평균 횟수에서 계산한다(Docs/기획/75번 §4-8, 설계 76번 §6.3 - 예전엔 30% 상수,
    /// 기획 07번 §3). 판정 시점은 FieldManager의 판정 시계가 낸다(설계 87번 §6.3 - 전투 0초 / 대화 +1.25초 / 이벤트 +2.5초로 어긋나게 둔 한 시간 축).
    /// 시계는 상행 진행 이벤트에 얹혀 있어 전투 중·도착 후엔 판정도 별도 코드 없이 멈춘다(Docs/설계/05-2026-08-25-인카운터_판정_아키텍처.md §3).
    /// 상행 정지는 정지 조율자를 거친다(설계 87번 §6.2) - 이벤트 중 전투처럼 원인이 겹쳐도 한쪽 해제가 다른 쪽 정지를 풀지 않게.
    /// </summary>
    public class EncounterManager : MonoBehaviour, IEncounterManager, IManagedComponent
    {
        public event Action OnEncounterTriggered;

        private ISessionState sessionState;
        private ITripHaltControl halt;          // 없으면(Bootstrap 미재빌드) 상행 시계를 직접 멈춘다
        private ITripCheckSignal checks;
        private readonly RandomEncounterRule randomRule = new();
        // 여정 상태가 없으면(인스톨러 미실행) 보통 등급 빈도로 판정한다.
        private IEncounterRateSource rateSource;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IEncounterManager>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            sessionState = registrar.Resolve<ISessionState>();
            registrar.TryResolve(out rateSource);
            registrar.TryResolve(out halt);
            if (checks != null) checks.BattleCheck -= HandleBattleCheck;
            if (registrar.TryResolve(out checks)) checks.BattleCheck += HandleBattleCheck;
            else Debug.LogWarning($"{nameof(EncounterManager)}: 판정 시계(ITripCheckSignal)가 없어 랜덤 전투가 발생하지 않는다(Tools > Game > Build Bootstrap Scene).");
        }

        // 판정 시계의 전투 판정 신호(5초 축 0초 지점). 도착 틱 제외는 시계가 처리한다. 판정 성공 여부와 무관하게 매 주기 독립 판정(기획 07번 §3).
        private void HandleBattleCheck()
        {
            var encountersPerFiveMinutes = rateSource?.EncountersPerFiveMinutes ?? TripTravelRules.ResolveGrade(TripTravelSettings.Default(), TripTravelSettings.DefaultRoadDifficulty).EncountersPerFiveMinutes;
            randomRule.Probability = TripTravelRules.ComputeCheckProbability(encountersPerFiveMinutes, TripCheckSchedule.BattleIntervalSeconds);
            if (randomRule.ShouldTrigger())
            {
                TriggerEncounter();
            }
        }

#if UNITY_EDITOR
        // 디버깅 전용(TripDebugView "인카운터 발생") - 확률 판정을 건너뛰고 정상 인카운터 흐름을 즉시 시작한다. 디버그 도구를 걷어낼 때 이 메서드도 지운다.
        public void DebugTriggerEncounter()
        {
            TriggerEncounter();
        }
#endif

        private void TriggerEncounter()
        {
            // 전투 시작 요청(IBattleController.StartBattle)은 더 이상 여기서 호출하지 않는다 -
            // FieldEncounterFlowCoordinator가 경고창 표시 + 전투 뷰 전환이 끝난 시점에 직접 호출한다
            // (Docs/제작/02-2026-08-25-인카운터_전투전환_구현.md 참고). 여기서 즉시 호출하면 전투 뷰에 들어가기도
            // 전에 결과 판정이 끝나버리는 문제가 있었다.
            if (halt != null) halt.Acquire(TripHaltReason.Battle);
            else sessionState.Pause();
            OnEncounterTriggered?.Invoke();
        }

        private void OnDestroy()
        {
            if (checks != null) checks.BattleCheck -= HandleBattleCheck;
        }
    }
}
