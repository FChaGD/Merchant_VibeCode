using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// ④ 마무리 - 기존 결과별 마무리를 그대로 옮겼다(기획 §14.4: 사망=플레이 종료, 도주=상행 속행, 궤주=Hub 귀환, 포로=콘텐츠 미구현이라
    /// 사망과 동일). ①~③은 전투 뷰 위에서 진행하고 이동 뷰 슬라이드는 여기서만 한다(설계 79번 §15-7).
    /// </summary>
    internal sealed class FinishStep : IBattleAftermathStep
    {
        private readonly IFieldUIController fieldUIController;
        private readonly FieldCameraController cameraController;
        private readonly IFieldFormationActivityRepository fieldActivityRepository; // 선택적 - 없으면 null-조건부 호출로 건너뜀
        private readonly ISessionState sessionState;
        private readonly IGameManager gameManager;

        public FinishStep(IFieldUIController fieldUIController, FieldCameraController cameraController, IFieldFormationActivityRepository fieldActivityRepository, ISessionState sessionState, IGameManager gameManager)
        {
            this.fieldUIController = fieldUIController;
            this.cameraController = cameraController;
            this.fieldActivityRepository = fieldActivityRepository;
            this.sessionState = sessionState;
            this.gameManager = gameManager;
        }

        public bool AppliesTo(BattleAftermathContext context) => true;

        public void Run(BattleAftermathContext context, Action onDone)
        {
            if (context.ContinuesTrip)
            {
                // 상단 버튼은 ①~③ 동안 비활성으로 두고 슬라이드 시작 직전에 되돌린다(사용자 확정) - 정리 중 다른 화면을 열 수 없게.
                // ResumeAll(Moving 포함, 설계 25번 §8.4)은 슬라이드가 끝난 뒤 - 판정 즉시 재개하면 전환 도중 진행된 이동이 뒤로 밀려
                // 어색해진다(사용자 확정).
                fieldUIController.SetTopLevelButtonsInteractable(true);
                cameraController.TransitionToMovement(onComplete: () =>
                {
                    fieldActivityRepository?.ResumeAll();
                    sessionState.Resume();
                    onDone();
                });
                return;
            }

            // 도주를 제외한 패배는 진행 중이던 배치/이동을 즉시 완료 처리한다(기획 20번 §3.2/§3.3, 설계 25번 §8.4).
            fieldActivityRepository?.ForceCompleteAll();

            if (context.Consequence == DefeatConsequence.Rout)
            {
                // 전투 유닛은 월드 오브젝트라 씬 전환 슬라이드로 함께 밀어낼 수 없다 - 이동 뷰로 먼저 돌아온 뒤 전환해야 "씬 전환은 항상
                // 이동 뷰에서 시작한다"는 전제가 성립한다(Docs/설계/38번 §10.4 A안). 상행을 재개하지 않으므로 버튼 활성/세션 재개는 없다.
                cameraController.TransitionToMovement(onComplete: () =>
                {
                    onDone();
                    fieldUIController.FinishTrip(); // 궤주 귀환도 상행 종료다(설계 81번 §6.4)
                    gameManager.RequestSceneTransition(ContentSceneId.Hub);
                });
                return;
            }

            // 사망·포로: 플레이 종료.
            onDone();
            Application.Quit();
        }
    }
}
