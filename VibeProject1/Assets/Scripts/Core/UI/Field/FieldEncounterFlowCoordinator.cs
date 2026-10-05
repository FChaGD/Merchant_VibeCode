using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 인카운터 발생부터 전투 뷰 전환·결과 처리까지의 상태 흐름을 전담한다. MonoBehaviour가 아닌
    /// 순수 C# 합성 객체로, FieldUIController가 최초 1회만 생성해 필드로 유지한다. encounterManager/
    /// battleResultSource는 Bootstrap에 상주하는 영속 객체라 Field 씬을 다시 로드할 때마다 재구독하면
    /// 이전 상행의 구독이 해제되지 않고 계속 쌓인다 - 그래서 매니저 이벤트 구독(Bind)은 최초 1회만,
    /// 씬 뷰 참조 교체(RebindViews)는 Field 씬을 로드할 때마다 실행한다(Docs/설계/04-2026-08-25-Field씬_아키텍처.md §5.2).
    /// 전투 결과 처리는 "패배 결과 판정 → 정산 반영 → 결과 정리 플로우 시작"만 하고 단계 진행은 BattleAftermathFlow가 맡는다
    /// (설계 79번 §6). 단계들이 이번 씬의 뷰를 쥐므로 플로우는 RebindViews마다 새로 만든다.
    /// </summary>
    internal class FieldEncounterFlowCoordinator
    {
        private const float WarningDisplaySeconds = 2f;
        private const float TransitionCurtainFadeOutSeconds = 2f; // 사용자 확정값 - Hub↔Field 전환용 커튼과 동일

        private bool eventsBound;

        private IUIManager uiManager;
        private ISessionState sessionState;
        private IBattleController battleController;
        private IDefeatConsequenceSource defeatConsequenceSource;
        private IGameManager gameManager;
        // Field 배치 시간(Docs/설계/25번 §8) 일시정지/재개/강제완료 연동 - 없으면(인스톨러 미실행)
        // null-조건부 호출로 자연히 비활성화된다.
        private IFieldFormationActivityRepository fieldActivityRepository;
        // 전투 정산(설계 79번 §5)·회수 적재(§7)·대열 정리(§8) - 전부 선택적이다. 없으면 정산 없이 요약이 비고(default),
        // 해당 단계는 AppliesTo가 false가 되어 건너뛴다.
        private IBattleAftermathApplier aftermathApplier;
        private ITradeGoodsCargoSettlement cargoSettlement;
        private IFormationRepairMode repairMode;

        private MonoBehaviour coroutineRunner;
        private IFieldUIController fieldUIController;
        private FieldCameraController cameraController;
        private FieldEncounterWarningView warningView;
        private FieldResultPopupView resultPopupView;
        private FieldTransitionCurtainView transitionCurtain;
        private BattleAftermathFlow aftermathFlow;

        // 인카운터 발생 시점부터 전투 뷰 전환이 끝날 때까지 true로 유지한다. 전투 시작(StartBattle)
        // 자체를 전환 완료 시점으로 옮겼지만(HandleEncounterTriggered/TransitionAfterWarning 참고),
        // 실제 전투 로직이 즉시(동기) 결과를 낼 가능성까지 대비해 안전장치로 남겨둔다 - StartBattle()
        // 호출이 그 안에서 곧바로 OnBattleEnded를 발생시켜도 이 큐잉이 순서를 보장한다.
        private bool isTransitioning;
        private BattleResult? pendingResult;

        public void Bind(IUIManager uiManager, ISessionState sessionState, IEncounterManager encounterManager, IBattleController battleController, IBattleResultSource battleResultSource, IDefeatConsequenceSource defeatConsequenceSource, IGameManager gameManager, IFieldFormationActivityRepository fieldActivityRepository, IBattleAftermathApplier aftermathApplier, ITradeGoodsCargoSettlement cargoSettlement, IFormationRepairMode repairMode)
        {
            this.uiManager = uiManager;
            this.sessionState = sessionState;
            this.battleController = battleController;
            this.defeatConsequenceSource = defeatConsequenceSource;
            this.gameManager = gameManager;
            this.fieldActivityRepository = fieldActivityRepository;
            this.aftermathApplier = aftermathApplier;
            this.cargoSettlement = cargoSettlement;
            this.repairMode = repairMode;

            if (eventsBound)
            {
                return;
            }

            encounterManager.OnEncounterTriggered += HandleEncounterTriggered;
            battleResultSource.OnBattleEnded += HandleBattleEnded;
            eventsBound = true;
        }

        public void RebindViews(MonoBehaviour coroutineRunner, IFieldUIController fieldUIController, FieldCameraController cameraController, FieldEncounterWarningView warningView, FieldResultPopupView resultPopupView, FieldTransitionCurtainView transitionCurtain, FieldCargoRecoveryPanel cargoRecoveryPanel)
        {
            this.coroutineRunner = coroutineRunner;
            this.fieldUIController = fieldUIController;
            this.cameraController = cameraController;
            this.warningView = warningView;
            this.resultPopupView = resultPopupView;
            this.transitionCurtain = transitionCurtain;
            aftermathFlow = BuildAftermathFlow(cargoRecoveryPanel);
        }

        // Bind(영속 의존성)가 RebindViews보다 먼저 불리므로(FieldUIController.RegisterFieldUI) 여기서는 두 쪽 참조가 모두 최신이다.
        private BattleAftermathFlow BuildAftermathFlow(FieldCargoRecoveryPanel cargoRecoveryPanel)
        {
            BattleAftermathFlow flow = null;
            var steps = new List<IBattleAftermathStep>
            {
                new ResultPopupStep(resultPopupView, (step, context) => flow.HasStepAfter(step, context)),
                new CargoRecoveryStep(uiManager, cargoSettlement, cargoRecoveryPanel),
                new FormationRepairStep(uiManager, repairMode),
                new FinishStep(fieldUIController, cameraController, fieldActivityRepository, sessionState, gameManager),
            };
            flow = new BattleAftermathFlow(steps);
            return flow;
        }

        private void HandleEncounterTriggered()
        {
            uiManager.Close(UIPanelIds.Formation);   // 열려있지 않아도 안전(Close는 멱등) - 열려 있었다면
                                                      // FormationPanel의 기존 규칙대로 미적용 변경은 자동 폐기됨
            uiManager.Close(UIPanelIds.Tactics);     // 같은 이유로 인카운터 발생 시 함께 닫는다 - 방향성 지시는
                                                      // Apply 버튼 없이 즉시 반영이라(TacticsPanel 요약 주석 참고)
                                                      // 닫아도 버려지는 변경이 없다
            fieldUIController.SetTopLevelButtonsInteractable(false); // 인카운터~전투 진행 중에는 재호출 버튼도
                                                      // 못 누르게 막는다(사용자 확정, 2026-09-07) - 이동 뷰로
                                                      // 복귀하는 슬라이드 전환 직전(결과 정리 플로우의 FinishStep,
                                                      // 승리/도주)에 다시 켠다 - 정리 단계 ①~③ 동안은 계속 꺼 둔다.
            fieldActivityRepository?.PauseAll();     // Field 배치/이동 전부 일시정지(설계 25번 §8.1) -
                                                      // Adding은 전투 시작 시 LiveBattleSimulationRule.ResumeSimulation()이
                                                      // 다시 재개한다(§8.2), Moving은 전투 종료까지 계속 멈춰 있는다.
            isTransitioning = true;
            warningView.Show();
            coroutineRunner.StartCoroutine(TransitionAfterWarning());
        }

        private IEnumerator TransitionAfterWarning()
        {
            yield return new WaitForSeconds(WarningDisplaySeconds);
            warningView.Hide();

            // 커튼 등장(슬라이드 중 전투 뷰와 동행)은 FieldCameraController가 처리한다 - 여기서는
            // 슬라이드가 끝나 화면이 완전히 덮인 뒤, 전투 상태가 실제로 재구성된 시점에 걷기만 한다.
            cameraController.TransitionToBattle(() =>
            {
                // 전투 시작을 여기서 호출한다 - EncounterManager가 인카운터 발생과 동시에 호출하면
                // PlaceholderBattleResultRule의 1초 판정이 2초 경고창 표시 중에 끝나버려, 전투 뷰에
                // 들어가자마자 결과가 즉시 뜨는 문제가 있었다. 전투 뷰 전환이 끝난 시점에 시작해야
                // "전투 뷰 진입 후 1초 뒤 결과"라는 체감 흐름이 만들어진다.
                // StartBattle()은 동기적으로 LiveBattleSimulationRule.Evaluate()→BattleViewPresenter
                // .Present()까지 실행해 유닛 뷰를 새로 갱신한다 - 다만 시뮬레이션은 일시정지 상태로
                // 시작하므로(LiveBattleSimulationRule.paused) 유닛은 배치만 되고 아직 움직이지 않는다.
                // 커튼 페이드 아웃 도중 반투명해진 커튼 너머로 이미 움직이는 전투가 비쳐 보이는 문제를
                // 막기 위해, 실제 틱 재개(ResumeSimulation)는 페이드가 완전히 끝난 뒤로 미룬다(사용자 확정).
                battleController.StartBattle();
                transitionCurtain.FadeOut(coroutineRunner, TransitionCurtainFadeOutSeconds, onComplete: () =>
                {
                    battleController.ResumeSimulation();
                    isTransitioning = false;
                    FlushPendingResultIfAny();
                });
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

        // 정산 반영을 결과 팝업보다 먼저 해야 팝업이 반영 결과를 보여 준다(기획 78번 §4-5). 결과별 마무리(사망=플레이 종료,
        // 도주=상행 속행, 궤주=Hub 귀환, 포로=사망과 동일 - 기획 §14.4)는 FinishStep으로 옮겼다.
        private void ShowResult(BattleResult result)
        {
            DefeatConsequence? consequence = result.Outcome == BattleOutcome.Defeat
                ? defeatConsequenceSource.ResolveDefeatConsequence()
                : null;
            var summary = aftermathApplier?.Apply(result, consequence) ?? default;
            aftermathFlow.Start(new BattleAftermathContext(result, consequence, summary));
        }
    }
}
