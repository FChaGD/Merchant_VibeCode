using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// Field 씬의 이동 뷰(진행 게이지, 배경, 정비창 재호출)를 조율한다. 인카운터 발생 시 경고창 점멸,
    /// 전투 뷰 전환, 결과 팝업 처리는 FieldEncounterFlowCoordinator에 위임한다(SRP) - 도착 처리
    /// (게이지 100%↔OnArrived)만은 인카운터/전투와 무관한 단순 이벤트→뷰 반영이라 이 클래스가 직접
    /// 처리한다(Docs/설계/04-2026-08-25-Field씬_아키텍처.md §5.3).
    /// </summary>
    public class FieldUIController : MonoBehaviour, IFieldUIController
    {
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private BattleCharacterUnitView battleCharacterViewPrefab;
        [SerializeField] private BattleProtectedUnitView battleProtectedViewPrefab;
        [SerializeField] private BattlePendingReinforcementView battlePendingReinforcementViewPrefab;

        /// <summary>
        /// Hub↔Field 씬 전환 연출(SceneTransitionEffectController)이 슬라이드시킬 대상. Field는 이번
        /// 연출 전용 요소를 새로 만들지 않고 기존 이동 뷰 루트를 그대로 재사용한다 - Hub↔Field 왕복
        /// 시점엔 항상 이동 뷰가 화면에 보이는 상태이기 때문이다(Docs/설계/10-2026-08-26-씬전환_연출_아키텍처.md §8).
        /// </summary>
        public RectTransform MovementViewRoot => movementViewRoot;

        private FieldProgressGaugeView gaugeView;
        private FieldLegArrivalNoticeView legArrivalNoticeView;
        // 구간 단위 진행(Docs/설계/76번 §6.2) - 씬을 로드할 때마다 이번 씬의 뷰로 새로 만든다.
        private FieldTripLegCoordinator legCoordinator;
        private Button formationButton;
        private Button tacticsButton;
        private FieldEncounterWarningView warningView;
        private RectTransform movementViewRoot;
        private RectTransform battleViewRoot;
        // 월드 오브젝트 전환(Docs/설계/13번) - UI 마커가 아니라 BattleWorldRoot 산하 Transform이다.
        private BattleWorldRoot battleWorldRoot;
        private Transform battleAllyLayer;
        private Transform battleEnemyLayer;
        private BattleFieldWorldCameraView battleCameraView;
        private BattleBackgroundGridView battleBackgroundView;
        private FieldResultPopupView resultPopupView;
        private FieldTransitionCurtainView transitionCurtain;
        private FieldEncounterFlowCoordinator flowCoordinator;
        private BattleViewPresenter viewPresenter;
        private IGameManager gameManager;
        private ISessionState sessionState;
        private IUIManager uiManager;
        // 상행 시작/종료(허브 복귀) 시 보유 유닛 HP를 리셋하는 데 쓴다(설계 15번) - 없어도(TryResolve
        // 실패) 상행 진행 자체는 정상 동작한다(null-조건부 호출).
        private IUnitConditionRepository unitConditionRepository;
        // 도착 판정 성립 시 "현재 위치"를 이번 상행의 도착지로 갱신하는 데 쓴다(기획 16번 §4, 설계
        // 21번 §7) - 없어도(TryResolve 실패) 상행 진행 자체는 정상 동작한다(null-조건부 호출).
        private ITripCurrentLocationRepository currentLocationRepository;
        private ITripDestinationAssigner destinationAssigner;
        // 도착(상행 종료) 시점에 진행 중이던 Field 배치/이동을 즉시 도착지로 확정하는 데 쓴다(HandleArrived) -
        // 그러지 않으면 이동 중이던 유닛의 FormationLayout이 여전히 출발 슬롯에 남아 있어(설계 25번 §3.2)
        // Hub 진입 시 출발지로 되돌아간 것처럼 보이는 버그가 있었다(실전 확인, 2026-09-06).
        private IFieldFormationActivityRepository fieldActivityRepository;
        // 상행 종료 처리(설계 81번 §6.4) - 사망 캐릭터 영구 제거 + 상태 초기화. null이면 초기화만 한다.
        private TripEndProcessor tripEndProcessor;

        public void RegisterFieldUI(SceneUIRoot sceneUIRoot, IUIManager uiManager, ISessionState sessionState, IEncounterManager encounterManager, IBattleController battleController, IBattleResultSource battleResultSource, IDefeatConsequenceSource defeatConsequenceSource, IBattleSimulationEvents battleSimulationEvents, IGameManager gameManager, ISceneRevealSignal sceneRevealSignal, IUnitConditionRepository unitConditionRepository, ITripCurrentLocationRepository currentLocationRepository, ITripDestinationAssigner destinationAssigner, IFieldFormationActivityRepository fieldActivityRepository, ITripItinerary itinerary, IWorldMapReader worldMap, IBattleAftermathApplier aftermathApplier, ITradeGoodsCargoSettlement cargoSettlement, IFormationRepairMode repairMode, FieldCargoRecoveryPanel cargoRecoveryPanel, TripEndProcessor tripEndProcessor)
        {
            if (!TryBind(sceneUIRoot))
            {
                return;
            }

            this.gameManager = gameManager;
            this.sessionState = sessionState;
            this.uiManager = uiManager;
            this.unitConditionRepository = unitConditionRepository;
            this.currentLocationRepository = currentLocationRepository;
            this.destinationAssigner = destinationAssigner;
            this.fieldActivityRepository = fieldActivityRepository;
            this.tripEndProcessor = tripEndProcessor;
            legCoordinator = new FieldTripLegCoordinator(sessionState, itinerary, currentLocationRepository, worldMap, gaugeView, legArrivalNoticeView, HandleTripArrived);

            formationButton.onClick.RemoveAllListeners();
            formationButton.onClick.AddListener(() => uiManager.Open(UIPanelIds.Formation));

            tacticsButton.onClick.RemoveAllListeners();
            tacticsButton.onClick.AddListener(() => uiManager.Open(UIPanelIds.Tactics));

            // 화면이 완전히 드러나기 전까지는 정비창/방향성 지시 재호출을 막는다(사용자 확정) -
            // HandleSceneRevealed에서 다시 켠다. 전환 없이 로드된 경우(최초 진입 등)엔 사실상 바로 다시 켜진다.
            SetTopLevelButtonsInteractable(false);
            sceneRevealSignal.SceneRevealed -= HandleSceneRevealed;
            sceneRevealSignal.SceneRevealed += HandleSceneRevealed;

            // Field 씬은 상행마다 다시 로드되지만 sessionState(SessionStateTracker)는 Bootstrap에 상주하는
            // 영속 객체다 - 재구독 전 항상 먼저 해제해 상행을 반복할수록 구독이 누적되는 것을 막는다
            // (Docs/설계/04-2026-08-25-Field씬_아키텍처.md §5 이벤트 구독 수명주기 참고).
            sessionState.OnProgressChanged -= HandleProgressChanged;
            sessionState.OnProgressChanged += HandleProgressChanged;
            sessionState.OnArrived -= HandleLegArrived;
            sessionState.OnArrived += HandleLegArrived;

            // encounterManager/battleResultSource는 Bootstrap 상주 영속 객체다 - flowCoordinator를
            // Field 재방문 시 재생성하지 않아야 이전 상행의 구독이 쌓이지 않는다
            // (Docs/설계/04-2026-08-25-Field씬_아키텍처.md §5.2). cameraController는 이번 Field 씬의 뷰 참조를
            // 담고 있어 매번 새로 만든다.
            flowCoordinator ??= new FieldEncounterFlowCoordinator();
            flowCoordinator.Bind(uiManager, sessionState, encounterManager, battleController, battleResultSource, defeatConsequenceSource, gameManager, fieldActivityRepository, aftermathApplier, cargoSettlement, repairMode);
            var cameraController = new FieldCameraController(this, movementViewRoot, battleViewRoot, battleWorldRoot.gameObject, battleCameraView, transitionCurtain);
            // 회수 적재 패널도 이번 씬의 화면 요소로 만든 것이라(FieldUIWiring) 뷰와 함께 매번 다시 넘긴다.
            flowCoordinator.RebindViews(this, this, cameraController, warningView, resultPopupView, transitionCurtain, cargoRecoveryPanel);

            // battleSimulationEvents도 Bootstrap 상주 영속 객체(BattleManager)라 같은 이유로
            // viewPresenter를 재생성하지 않는다 - Bind(이벤트 구독)는 최초 1회, RebindViews(이번 씬의
            // 유닛 레이어/프리팹 참조)는 Field 씬을 로드할 때마다 실행한다.
            viewPresenter ??= new BattleViewPresenter();
            viewPresenter.Bind(battleSimulationEvents);
            viewPresenter.RebindViews(battleAllyLayer, battleEnemyLayer, battleCharacterViewPrefab, battleProtectedViewPrefab, battlePendingReinforcementViewPrefab, battleCameraView, battleBackgroundView);

            // sessionState.Begin()은 여기서 바로 부르지 않는다 - 화면이 완전히 드러난 뒤(HandleSceneRevealed)에
            // 시작해야 "전투 시작" 준비(=상행 진행 시작)가 페이드 아웃 완료 이후로 미뤄진다(사용자 확정).
        }

        private void HandleSceneRevealed(ContentSceneId sceneId)
        {
            if (sceneId != ContentSceneId.Field)
            {
                return;
            }

            SetTopLevelButtonsInteractable(true);
            unitConditionRepository?.ResetAllToFull(); // 상행 시작 = 전원 만피로 출발(기획 13번 §4-1, 설계 15번 §4)
            legCoordinator.BeginFirstLeg(); // 구간마다 소요시간이 다르다(Docs/설계/76번 §6.2)
        }

        public void FinishTrip()
        {
            if (tripEndProcessor != null) tripEndProcessor.Finish();
            else unitConditionRepository?.ResetAllToFull(); // 처리기 없이도 기존 상행 종료 회복은 유지한다(기획 13번 §4-3).
        }

        public void SetTopLevelButtonsInteractable(bool interactable)
        {
            if (formationButton != null)
            {
                formationButton.interactable = interactable;
            }

            if (tacticsButton != null)
            {
                tacticsButton.interactable = interactable;
            }
        }

        private void HandleProgressChanged(float progress)
        {
            gaugeView.SetProgress(progress);
        }

        // 구간 하나가 끝날 때마다 불린다 - 중간 도시인지 최종 도착인지는 구간 진행 조율자가 가른다(Docs/설계/76번 §6.2).
        private void HandleLegArrived() => legCoordinator?.HandleLegArrived();

        // 최종 도착 처리(Docs/설계/04-2026-08-25-Field씬_아키텍처.md §5.3) - 전투 승/패와 같은 resultPopupView를
        // 재사용한다(문구·버튼 라벨·콜백만 다름). 중간 도시 도착은 여기 오지 않는다(FieldTripLegCoordinator).
        private void HandleTripArrived()
        {
            fieldActivityRepository?.ForceCompleteAll(); // 이동 중이던 배치를 도착지로 즉시 확정(위 필드 선언부 주석 참고)
            FinishTrip(); // 상행 종료 = 사망 제거 + 전원 회복(기획 13번 §4-3, 설계 81번 §6.4)

            // "현재 위치"를 이번 상행의 도착지로 갱신한다(기획 16번 §4) - 빌드에서는 도착지를 고를
            // 방법이 없어 DestinationCityId가 항상 null이므로 자연히 아무 일도 일어나지 않는다.
            if (destinationAssigner?.DestinationCityId is { } destinationCityId)
            {
                currentLocationRepository?.SetCurrentCity(destinationCityId);
                destinationAssigner.Reset(); // 다음 상행 준비 UI가 빈 도착지로 시작하도록.
            }

            // 인카운터 발생 시 FieldEncounterFlowCoordinator.HandleEncounterTriggered가 배치/방향성
            // 지시를 닫는 것과 같은 이유 - 열려있는 채로 도착 팝업이 뜨면 그 위로 안 닫힌 패널이 남는다.
            // 둘 다 Close가 멱등이라 열려있지 않아도 안전하다.
            uiManager.Close(UIPanelIds.Formation);
            uiManager.Close(UIPanelIds.Tactics);
            resultPopupView.Show("도착 성공", "도시 입장", onConfirm: () => gameManager.RequestSceneTransition(ContentSceneId.Hub));
        }

        private bool TryBind(SceneUIRoot sceneUIRoot)
        {
            if (!sceneUIRoot.TryGetElement<FieldProgressGaugeView>(FieldUIElementIds.ProgressGauge, out gaugeView))
            {
                WarnMissing(FieldUIElementIds.ProgressGauge);
                return false;
            }

            // 구간 도착 알림은 없어도 진행은 된다(알림 없이 바로 다음 구간) - 인스톨러 미실행만 경고한다.
            if (!sceneUIRoot.TryGetElement(FieldUIElementIds.LegArrivalNotice, out legArrivalNoticeView))
            {
                WarnMissing(FieldUIElementIds.LegArrivalNotice + " (Tools > Game > Build Field Scene)");
            }

            if (!sceneUIRoot.TryGetElement<Image>(FieldUIElementIds.Background, out var background))
            {
                WarnMissing(FieldUIElementIds.Background);
                return false;
            }

            if (backgroundSprite != null)
            {
                background.sprite = backgroundSprite;
            }

            if (!sceneUIRoot.TryGetElement<Button>(FieldUIElementIds.FormationButton, out formationButton))
            {
                WarnMissing(FieldUIElementIds.FormationButton);
                return false;
            }

            if (!sceneUIRoot.TryGetElement<Button>(FieldUIElementIds.TacticsButton, out tacticsButton))
            {
                WarnMissing(FieldUIElementIds.TacticsButton);
                return false;
            }

            if (!sceneUIRoot.TryGetElement<FieldEncounterWarningView>(FieldUIElementIds.EncounterWarning, out warningView))
            {
                WarnMissing(FieldUIElementIds.EncounterWarning);
                return false;
            }

            if (!sceneUIRoot.TryGetElement<RectTransform>(FieldUIElementIds.MovementViewRoot, out movementViewRoot))
            {
                WarnMissing(FieldUIElementIds.MovementViewRoot);
                return false;
            }

            if (!sceneUIRoot.TryGetElement<RectTransform>(FieldUIElementIds.BattleViewRoot, out battleViewRoot))
            {
                WarnMissing(FieldUIElementIds.BattleViewRoot);
                return false;
            }

            // 전투 카메라는 Field 씬의 Main Camera에 부착된다(새 카메라를 만들지 않고 재사용 -
            // Docs/설계/13번 §6). Main Camera는 Canvas 하위가 아니라 UIElementMarker/SceneUIRoot로는
            // 조회할 수 없어 Camera.main으로 직접 찾는다.
            battleCameraView = Camera.main != null ? Camera.main.GetComponent<BattleFieldWorldCameraView>() : null;
            if (battleCameraView == null)
            {
                WarnMissing(nameof(BattleFieldWorldCameraView) + " (Main Camera)");
                return false;
            }

            // 전투 유닛(캐릭터/보호목표) 스프라이트의 루트도 Canvas 밖 씬 루트에 독립적으로 있어
            // (Docs/설계/13번 §2) UIElementMarker가 아니라 BattleWorldRoot 마커로 조회한다.
            battleWorldRoot = Object.FindFirstObjectByType<BattleWorldRoot>(FindObjectsInactive.Include);
            if (battleWorldRoot == null)
            {
                WarnMissing(nameof(BattleWorldRoot));
                return false;
            }

            battleAllyLayer = battleWorldRoot.transform.Find("AllyLayer");
            if (battleAllyLayer == null)
            {
                WarnMissing(nameof(BattleWorldRoot) + "/AllyLayer");
                return false;
            }

            battleEnemyLayer = battleWorldRoot.transform.Find("EnemyLayer");
            if (battleEnemyLayer == null)
            {
                WarnMissing(nameof(BattleWorldRoot) + "/EnemyLayer");
                return false;
            }

            battleBackgroundView = battleWorldRoot.GetComponent<BattleBackgroundGridView>();
            if (battleBackgroundView == null)
            {
                WarnMissing(nameof(BattleWorldRoot) + " (BattleBackgroundGridView)");
                return false;
            }

            if (!sceneUIRoot.TryGetElement<FieldResultPopupView>(FieldUIElementIds.ResultPopup, out resultPopupView))
            {
                WarnMissing(FieldUIElementIds.ResultPopup);
                return false;
            }

            if (!sceneUIRoot.TryGetElement<FieldTransitionCurtainView>(FieldUIElementIds.TransitionCurtain, out transitionCurtain))
            {
                WarnMissing(FieldUIElementIds.TransitionCurtain);
                return false;
            }

            return true;
        }

        private static void WarnMissing(string id)
        {
            Debug.LogWarning($"Field UI에서 '{id}' 요소를 찾을 수 없다. {nameof(UIElementMarker)}가 부착되어 있는지 확인하라.");
        }
    }
}
