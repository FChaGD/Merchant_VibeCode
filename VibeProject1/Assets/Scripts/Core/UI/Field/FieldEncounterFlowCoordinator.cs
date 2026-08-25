using System.Collections;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 인카운터 발생부터 전투 뷰 전환·결과 처리까지의 상태 흐름을 전담한다. MonoBehaviour가 아닌
    /// 순수 C# 합성 객체로, FieldUIController가 최초 1회만 생성해 필드로 유지한다. encounterManager/
    /// battleResultSource는 Bootstrap에 상주하는 영속 객체라 Field 씬을 다시 로드할 때마다 재구독하면
    /// 이전 상행의 구독이 해제되지 않고 계속 쌓인다 - 그래서 매니저 이벤트 구독(Bind)은 최초 1회만,
    /// 씬 뷰 참조 교체(RebindViews)는 Field 씬을 로드할 때마다 실행한다(Docs/설계/04_Field씬_아키텍처.md §5.2).
    /// </summary>
    internal class FieldEncounterFlowCoordinator
    {
        private const float WarningDisplaySeconds = 2f;

        private bool eventsBound;

        private IUIManager uiManager;
        private ISessionState sessionState;
        private IBattleController battleController;

        private MonoBehaviour coroutineRunner;
        private FieldCameraController cameraController;
        private FieldEncounterWarningView warningView;
        private FieldResultPopupView resultPopupView;

        // 인카운터 발생 시점부터 전투 뷰 전환이 끝날 때까지 true로 유지한다. 전투 시작(StartBattle)
        // 자체를 전환 완료 시점으로 옮겼지만(HandleEncounterTriggered/TransitionAfterWarning 참고),
        // 실제 전투 로직이 즉시(동기) 결과를 낼 가능성까지 대비해 안전장치로 남겨둔다 - StartBattle()
        // 호출이 그 안에서 곧바로 OnBattleEnded를 발생시켜도 이 큐잉이 순서를 보장한다.
        private bool isTransitioning;
        private BattleResult? pendingResult;

        public void Bind(IUIManager uiManager, ISessionState sessionState, IEncounterManager encounterManager, IBattleController battleController, IBattleResultSource battleResultSource)
        {
            this.uiManager = uiManager;
            this.sessionState = sessionState;
            this.battleController = battleController;

            if (eventsBound)
            {
                return;
            }

            encounterManager.OnEncounterTriggered += HandleEncounterTriggered;
            battleResultSource.OnBattleEnded += HandleBattleEnded;
            eventsBound = true;
        }

        public void RebindViews(MonoBehaviour coroutineRunner, FieldCameraController cameraController, FieldEncounterWarningView warningView, FieldResultPopupView resultPopupView)
        {
            this.coroutineRunner = coroutineRunner;
            this.cameraController = cameraController;
            this.warningView = warningView;
            this.resultPopupView = resultPopupView;
        }

        private void HandleEncounterTriggered()
        {
            uiManager.Close(UIPanelIds.Formation);   // 열려있지 않아도 안전(Close는 멱등) - 열려 있었다면
                                                      // FormationPanel의 기존 규칙대로 미적용 변경은 자동 폐기됨
            isTransitioning = true;
            warningView.Show();
            coroutineRunner.StartCoroutine(TransitionAfterWarning());
        }

        private IEnumerator TransitionAfterWarning()
        {
            yield return new WaitForSeconds(WarningDisplaySeconds);
            warningView.Hide();
            cameraController.TransitionToBattle(() =>
            {
                // 전투 시작을 여기서 호출한다 - EncounterManager가 인카운터 발생과 동시에 호출하면
                // PlaceholderBattleResultRule의 1초 판정이 2초 경고창 표시 중에 끝나버려, 전투 뷰에
                // 들어가자마자 결과가 즉시 뜨는 문제가 있었다. 전투 뷰 전환이 끝난 시점에 시작해야
                // "전투 뷰 진입 후 1초 뒤 결과"라는 체감 흐름이 만들어진다.
                battleController.StartBattle();
                isTransitioning = false;
                FlushPendingResultIfAny();
            });
        }

        private void HandleBattleEnded(BattleResult result)
        {
            // 카메라가 아직 전투 뷰로 전환되지 않았으면 결과를 큐잉했다가 전환 완료 후 처리한다.
            if (isTransitioning)
            {
                pendingResult = result;
                return;
            }

            ShowResult(result);
        }

        private void FlushPendingResultIfAny()
        {
            if (!pendingResult.HasValue)
            {
                return;
            }

            var result = pendingResult.Value;
            pendingResult = null;   // ShowResult 호출 전에 먼저 비운다 - 재진입 시 중복 소비 방지
            ShowResult(result);
        }

        private void ShowResult(BattleResult result)
        {
            switch (result.Outcome)
            {
                case BattleOutcome.Victory:
                    // TransitionToMovement의 onComplete 시점에는 isTransitioning=false, pendingResult=null
                    // 상태가 이미 확보돼 있다 - 별도 리셋 없이 다음 인카운터를 곧바로 처리할 수 있다.
                    resultPopupView.Show("승리", "확인", onConfirm: () =>
                        cameraController.TransitionToMovement(onComplete: sessionState.Resume));
                    break;
                case BattleOutcome.Defeat:
                    resultPopupView.Show("패배", "플레이 종료", onConfirm: Application.Quit);
                    break;
                // 궤주/포로/사망/도주 등 판정 종류가 늘어나면([[01_게임개요_핵심루프]] 6절) 이 switch에
                // case를 추가해야 한다 - 지금은 승리/패배 2종뿐이라 전략 패턴으로 미리 분리하지 않았다.
            }
        }
    }
}
