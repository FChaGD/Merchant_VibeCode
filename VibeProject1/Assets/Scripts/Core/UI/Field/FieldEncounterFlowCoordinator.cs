using System.Collections;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 인카운터 발생부터 전투 뷰 전환·결과 처리까지의 상태 흐름을 전담한다. MonoBehaviour가 아닌
    /// 순수 C# 합성 객체로, FieldUIController가 최초 1회만 생성해 필드로 유지한다. encounterManager/
    /// battleResultSource는 Bootstrap에 상주하는 영속 객체라 Field 씬을 다시 로드할 때마다 재구독하면
    /// 이전 상행의 구독이 해제되지 않고 계속 쌓인다 - 그래서 매니저 이벤트 구독(Bind)은 최초 1회만,
    /// 씬 뷰 참조 교체(RebindViews)는 Field 씬을 로드할 때마다 실행한다(Docs/설계/04-2026-08-25-Field씬_아키텍처.md §5.2).
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

        private MonoBehaviour coroutineRunner;
        private IFieldUIController fieldUIController;
        private FieldCameraController cameraController;
        private FieldEncounterWarningView warningView;
        private FieldResultPopupView resultPopupView;
        private FieldTransitionCurtainView transitionCurtain;

        // 인카운터 발생 시점부터 전투 뷰 전환이 끝날 때까지 true로 유지한다. 전투 시작(StartBattle)
        // 자체를 전환 완료 시점으로 옮겼지만(HandleEncounterTriggered/TransitionAfterWarning 참고),
        // 실제 전투 로직이 즉시(동기) 결과를 낼 가능성까지 대비해 안전장치로 남겨둔다 - StartBattle()
        // 호출이 그 안에서 곧바로 OnBattleEnded를 발생시켜도 이 큐잉이 순서를 보장한다.
        private bool isTransitioning;
        private BattleResult? pendingResult;

        public void Bind(IUIManager uiManager, ISessionState sessionState, IEncounterManager encounterManager, IBattleController battleController, IBattleResultSource battleResultSource, IDefeatConsequenceSource defeatConsequenceSource, IGameManager gameManager, IFieldFormationActivityRepository fieldActivityRepository)
        {
            this.uiManager = uiManager;
            this.sessionState = sessionState;
            this.battleController = battleController;
            this.defeatConsequenceSource = defeatConsequenceSource;
            this.gameManager = gameManager;
            this.fieldActivityRepository = fieldActivityRepository;

            if (eventsBound)
            {
                return;
            }

            encounterManager.OnEncounterTriggered += HandleEncounterTriggered;
            battleResultSource.OnBattleEnded += HandleBattleEnded;
            eventsBound = true;
        }

        public void RebindViews(MonoBehaviour coroutineRunner, IFieldUIController fieldUIController, FieldCameraController cameraController, FieldEncounterWarningView warningView, FieldResultPopupView resultPopupView, FieldTransitionCurtainView transitionCurtain)
        {
            this.coroutineRunner = coroutineRunner;
            this.fieldUIController = fieldUIController;
            this.cameraController = cameraController;
            this.warningView = warningView;
            this.resultPopupView = resultPopupView;
            this.transitionCurtain = transitionCurtain;
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
                                                      // 복귀하는 슬라이드 전환 직전(ShowResult/ShowDefeatConsequence의
                                                      // Victory/Flee 케이스)에 다시 켠다.
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

        private void ShowResult(BattleResult result)
        {
            switch (result.Outcome)
            {
                case BattleOutcome.Victory:
                    // TransitionToMovement의 onComplete 시점에는 isTransitioning=false, pendingResult=null
                    // 상태가 이미 확보돼 있다 - 별도 리셋 없이 다음 인카운터를 곧바로 처리할 수 있다.
                    // ResumeAll(Moving까지 포함해 전부 재개, 설계 25번 §8.4)은 이동 뷰 전환 슬라이드가
                    // 끝난 뒤에 호출한다 - 전환 중에는 배치/이동 애니메이션이 보이지 않아야 하므로,
                    // 판정 즉시 재개하면 전환 애니메이션 도중 이미 진행된 이동이 뒤로 밀려 어색해진다(사용자 확정).
                    resultPopupView.Show("승리", "확인", onConfirm: () =>
                    {
                        fieldUIController.SetTopLevelButtonsInteractable(true); // 슬라이드 전환 시작 전에 되돌린다(사용자 확정).
                        cameraController.TransitionToMovement(onComplete: () =>
                        {
                            fieldActivityRepository?.ResumeAll();
                            sessionState.Resume();
                        });
                    });
                    break;
                case BattleOutcome.Defeat:
                    ShowDefeatConsequence(defeatConsequenceSource.ResolveDefeatConsequence());
                    break;
            }
        }

        // 기획 §14.4 "궤주/포로/도주 각각의 상행 반영 규칙"을 확정해 반영한다 - 사망=플레이 종료,
        // 도주=상행 속행(Victory와 동일한 흐름), 궤주=Hub로 귀환. 포로는 아직 "포로 콘텐츠" 자체가
        // 없어(테스트 단계) 사망과 동일하게 임시로 플레이 종료 처리한다 - 콘텐츠가 생기면 이 case만
        // 교체하면 된다.
        private void ShowDefeatConsequence(DefeatConsequence consequence)
        {
            switch (consequence)
            {
                case DefeatConsequence.Death:
                    fieldActivityRepository?.ForceCompleteAll(); // 곧 Application.Quit이라 무의미하지만 일관성 위해 호출
                    resultPopupView.Show("패배 - 전멸", "플레이 종료", onConfirm: Application.Quit);
                    break;
                case DefeatConsequence.Flee:
                    // Victory와 동일한 흐름(이동 뷰 복귀 + 상행 재개, ResumeAll 타이밍도 동일하게
                    // 전환 슬라이드 완료 후) - §14.4에서 도주는 "상행 속행"으로 정했다.
                    resultPopupView.Show("패배 - 도주", "상행 속행", onConfirm: () =>
                    {
                        fieldUIController.SetTopLevelButtonsInteractable(true); // 슬라이드 전환 시작 전에 되돌린다(사용자 확정).
                        cameraController.TransitionToMovement(onComplete: () =>
                        {
                            fieldActivityRepository?.ResumeAll(); // 기획 20번 §3.2/§3.3 - 승리와 동일하게 재개
                            sessionState.Resume();
                        });
                    });
                    break;
                case DefeatConsequence.Rout:
                    // 진행 중이던 배치/이동을 Hub 귀환 전에 즉시 완료 처리한다(기획 20번 §3.2/§3.3,
                    // 설계 25번 §8.4 - 도주를 제외한 패배는 전부 이 경로).
                    fieldActivityRepository?.ForceCompleteAll();
                    // 먼저 전투 뷰 → 이동 뷰 슬라이드를 끝낸 뒤 씬 전환한다(Docs/설계/38번 §10.4 A안). 전투 유닛은
                    // UI가 아닌 월드 오브젝트(전투 카메라 렌더링)라 씬 전환 슬라이드로 함께 밀어낼 수 없다 - 이동 뷰로
                    // 돌아온 뒤 전환하면 "씬 전환은 항상 이동 뷰에서 시작한다"는 전제가 다시 성립한다. 승리/도주와 달리
                    // 상행을 재개하지 않으므로 상단 버튼 활성화/세션 재개는 하지 않는다.
                    resultPopupView.Show("패배 - 궤주", "귀환", onConfirm: () =>
                        cameraController.TransitionToMovement(onComplete: () =>
                            gameManager.RequestSceneTransition(ContentSceneId.Hub)));
                    break;
                case DefeatConsequence.Captured:
                default:
                    // 포로 콘텐츠 미구현(테스트 단계) - 확정되기 전까지 사망과 동일하게 처리한다.
                    fieldActivityRepository?.ForceCompleteAll();
                    resultPopupView.Show("패배 - 포로", "플레이 종료", onConfirm: Application.Quit);
                    break;
            }
        }
    }
}
