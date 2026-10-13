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
        private readonly TripDateAccrual dateAccrual; // 선택적 - 없으면 날짜 없이 진행
        private readonly FieldTripSignals tripSignals; // 정지 조율·계기 신호(설계 87 §6) - 없으면 예전처럼 직접 재개

        public FinishStep(IFieldUIController fieldUIController, FieldCameraController cameraController, IFieldFormationActivityRepository fieldActivityRepository, ISessionState sessionState, IGameManager gameManager, TripDateAccrual dateAccrual, FieldTripSignals tripSignals)
        {
            this.dateAccrual = dateAccrual;
            this.tripSignals = tripSignals ?? new FieldTripSignals(null, null);
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
                    ResumeTrip(tripSignals, () =>
                    {
                        fieldActivityRepository?.ResumeAll();
                        sessionState.Resume();
                    });
                    onDone();
                });
                return;
            }

            // 도주를 제외한 패배는 진행 중이던 배치/이동을 즉시 완료 처리한다(기획 20번 §3.2/§3.3, 설계 25번 §8.4).
            fieldActivityRepository?.ForceCompleteAll();

            if (context.Consequence == DefeatConsequence.Rout)
            {
                // 상행 종료(FinishTrip)·씬 전환 전에 정산한다 - 직전 도시부터 경과 × 1.5(Docs/기획/84번 §4-6).
                dateAccrual?.AccrueRout();
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

        /// <summary>
        /// 상행을 이어갈 때의 재개(설계 87 §6.2·§6.4). 정지 조율자가 있으면 "전투" 원인만 푼다 - 다른 원인(이벤트)이 남아 있으면 조율자가 정지를 유지한다.
        /// 없으면(Bootstrap 미재빌드) 예전처럼 직접 재개한다. 이벤트 전투의 신호 생략은 설계 89에서 이 메서드에 더한다.
        /// </summary>
        internal static void ResumeTrip(FieldTripSignals tripSignals, Action legacyResume)
        {
            if (tripSignals?.Halt != null) tripSignals.Halt.Release(TripHaltReason.Battle);
            else legacyResume();
            tripSignals?.Milestones?.Raise(TripMilestone.BattleEnded);
        }
    }
}
