using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 5초마다 30% 확률로 랜덤 인카운터를 판정한다(Docs/기획/07_인카운터_판정_기획.md §3). 판정 시점은
    /// SessionStateTracker가 이미 구현한 pause-aware 이벤트(OnProgressChanged)에 얹혀서 얻는다 - 전투
    /// 중/도착 후에는 이 이벤트 자체가 발행되지 않으므로 판정 타이머도 별도 코드 없이 함께 멈춘다
    /// (Docs/설계/05_인카운터_판정_아키텍처.md §3).
    /// </summary>
    public class EncounterManager : MonoBehaviour, IEncounterManager, IManagedComponent
    {
        [SerializeField] private float checkIntervalSeconds = 5f;

        public event Action OnEncounterTriggered;

        private ISessionState sessionState;
        private readonly IEncounterRule randomRule = new RandomEncounterRule();
        private float elapsed;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IEncounterManager>(this);
        }

        public void ResolveDependencies(IDependencyRegistrar registrar)
        {
            // Pause()만 쓰던 기존 의존성(ISessionPauseControl)을, 주기 판정에 OnProgressChanged 구독이
            // 필요해진 지금 ISessionState 전체로 확장했다(Docs/설계/05_인카운터_판정_아키텍처.md §3, §6 ISP 재검토).
            sessionState = registrar.Resolve<ISessionState>();
            sessionState.OnProgressChanged += HandleProgressChanged;
        }

        private void HandleProgressChanged(float progress)
        {
            // 도착(Progress>=1f)과 같은 틱에 판정이 겹치면 SessionStateTracker.Update()가 그 틱 안에서
            // OnProgressChanged(→인카운터 발생 가능)와 OnArrived를 연달아 발행해, 상행 성공 처리와
            // 인카운터 발생 처리가 동시에 진행되는 버그가 있었다 - 도착 틱은 판정 자체를 건너뛴다.
            if (progress >= 1f)
            {
                return;
            }

            elapsed += Time.deltaTime;
            if (elapsed < checkIntervalSeconds)
            {
                return;
            }

            elapsed = 0f;   // 판정 성공 여부와 무관하게 리셋 - 매 주기 독립 판정(Docs/기획/07_인카운터_판정_기획.md §3)
            if (randomRule.ShouldTrigger())
            {
                TriggerEncounter();
            }
        }

        private void TriggerEncounter()
        {
            // 전투 시작 요청(IBattleController.StartBattle)은 더 이상 여기서 호출하지 않는다 -
            // FieldEncounterFlowCoordinator가 경고창 표시 + 전투 뷰 전환이 끝난 시점에 직접 호출한다
            // (Docs/제작/02_인카운터_전투전환_구현.md 참고). 여기서 즉시 호출하면 전투 뷰에 들어가기도
            // 전에 결과 판정이 끝나버리는 문제가 있었다.
            sessionState.Pause();
            OnEncounterTriggered?.Invoke();
        }

        private void OnDestroy()
        {
            if (sessionState != null)
            {
                sessionState.OnProgressChanged -= HandleProgressChanged;
            }
        }
    }
}
