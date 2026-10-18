using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// UIManager 산하 컴포넌트. Field 씬이 로드될 때 필요한 UI 배선만 담당한다 - Battle 도메인
    /// 인터페이스(IBattleController 등)를 아는 곳을 UIManager에서 이 클래스로 옮겨, 공통 UI 프레임워크가
    /// 특정 씬의 구체 도메인을 몰라도 되게 한다(DIP, Docs/Refactor/2026-08-26-공통.md 3단계 수정안).
    /// </summary>
    public class FieldUIWiring : MonoBehaviour, IContentSceneUIWiring
    {
        public ContentSceneId SceneId => ContentSceneId.Field;

        // 회수 적재 패널(plain C#, 설계 79번 §7)은 Field 로드마다 이번 씬의 화면 요소로 새로 만든다 - 저장소(Bootstrap 상주) 이벤트를
        // 구독하므로 이전 패널은 Dispose한다(HubUIWiring의 시설 화면과 같은 방식).
        private FieldCargoRecoveryPanel cargoRecoveryPanel;
        private FieldMovementViewHider movementViewHider;

        public void Wire(IDependencyResolver registrar, IUIManager uiManager, IPanelRegistrar panelRegistrar)
        {
            // 이 씬의 SceneUIRoot를 여기서 한 번만 찾아 아래 3개 컴포넌트 전부에 넘긴다 - 예전엔
            // 각자 SceneManager.GetSceneByName부터 다시 조회했다(DRY, Docs/Refactor/2026-09-08_Hub.md
            // §3 수정 J).
            if (!SceneUIRootLocator.TryFind(SceneNames.Field, out var sceneUIRoot))
            {
                return;
            }

            var formationPanel = GetComponent<FieldFormationPanel>();
            if (formationPanel == null)
            {
                throw new InvalidOperationException($"{nameof(FieldUIWiring)}와 같은 GameObject에 {nameof(FieldFormationPanel)} 구현체가 없다.");
            }

            var fieldUIController = GetComponent<IFieldUIController>();
            if (fieldUIController == null)
            {
                throw new InvalidOperationException($"{nameof(FieldUIWiring)}와 같은 GameObject에 {nameof(IFieldUIController)} 구현체가 없다.");
            }

            var tacticsPanel = GetComponent<ITacticsPanel>();
            if (tacticsPanel == null)
            {
                throw new InvalidOperationException($"{nameof(FieldUIWiring)}와 같은 GameObject에 {nameof(ITacticsPanel)} 구현체가 없다.");
            }

            // 상행 관리 데이터 시스템이 아직 없어 선택적으로 조회한다 - 등록되면 자동으로 연결된다.
            registrar.TryResolve<ICaravanRosterProvider>(out var caravanRosterProvider);
            registrar.TryResolve<IFormationRepository>(out var formationRepository);
            registrar.TryResolve<IUnitConditionRepository>(out var unitConditionRepository);
            registrar.TryResolve<ITacticsRepository>(out var tacticsRepository);
            registrar.TryResolve<ITripCurrentLocationRepository>(out var currentLocationRepository);
            registrar.TryResolve<ITripDestinationAssigner>(out var destinationAssigner);
            registrar.TryResolve<IFieldFormationActivityRepository>(out var fieldActivityRepository);
            registrar.TryResolve<ITripItinerary>(out var tripItinerary);
            registrar.TryResolve<IWorldMapReader>(out var worldMap);
            // 전투 정산·회수 적재(설계 79번 §5·§7) - 없으면 정산 없이 결과 팝업·마무리만 진행한다.
            registrar.TryResolve<IBattleAftermathApplier>(out var aftermathApplier);
            registrar.TryResolve<ITradeGoodsCargoSettlement>(out var cargoSettlement);
            registrar.TryResolve<ITradeGoodsInventoryRepository>(out var tradeGoodsInventory);

            var sessionState = registrar.Resolve<ISessionState>();
            var encounterManager = registrar.Resolve<IEncounterManager>();
            var battleController = registrar.Resolve<IBattleController>();
            var battleResultSource = registrar.Resolve<IBattleResultSource>();
            var defeatConsequenceSource = registrar.Resolve<IDefeatConsequenceSource>();
            var battleSimulationEvents = registrar.Resolve<IBattleSimulationEvents>();
            var gameManager = registrar.Resolve<IGameManager>();
            var sceneRevealSignal = registrar.Resolve<ISceneRevealSignal>();

            // Formation UI(정비창)는 Hub 전용이 아니다 - Field도 자신만의 화면 요소를 갖고 있어
            // (FieldUIInstaller 참고) 여기서도 다시 등록해야 "정비창 재호출"이 동작한다.
            formationPanel.RegisterFieldFormationUI(sceneUIRoot, caravanRosterProvider, formationRepository, unitConditionRepository, fieldActivityRepository, uiManager);
            panelRegistrar.RegisterPopupPanel(formationPanel);

            tacticsPanel.RegisterTacticsUI(sceneUIRoot, tacticsRepository, uiManager);
            panelRegistrar.RegisterPopupPanel(tacticsPanel);

            RegisterCargoRecovery(sceneUIRoot, panelRegistrar, tradeGoodsInventory, cargoSettlement);

            // 대열 정리 모드(설계 79번 §8)는 정비창 패널이 구현한다 - 위에서 이미 확보한 같은 GameObject의 패널에서 꺼낸다.
            var repairMode = formationPanel.GetComponent<IFormationRepairMode>();

            // 상행 종료 처리(설계 81번 §6.4) - 의존성이 없으면 그 단계만 건너뛴다.
            registrar.TryResolve<IHiredCharacterRemover>(out var hiredCharacterRemover);
            registrar.TryResolve<IDeceasedCharacterRecorder>(out var deceasedRecorder);
            var tripEndProcessor = new TripEndProcessor(caravanRosterProvider, unitConditionRepository, formationRepository, hiredCharacterRemover, deceasedRecorder);

            // 궤주 날짜 정산(설계 93 §4.4) - 이동 중 날짜 진행은 FieldManager의 날짜 시계가 스스로 한다. 없으면(Bootstrap 미재빌드) 날짜 없이 진행한다.
            registrar.TryResolve<ITripDayRout>(out var dayRout);

            // 상행 정지 조율·계기 신호(설계 87번 §6) - FieldManager가 없으면(Bootstrap 미재빌드) 둘 다 null, 조율자들이 예전 동작으로 물러난다.
            var tripSignals = FieldTripSignals.Resolve(registrar);
            registrar.TryResolve<ICurrentLegDifficulty>(out var legDifficulty); // 구간 라벨 등급명 갱신(설계 89 §4.3)

            fieldUIController.RegisterFieldUI(sceneUIRoot, uiManager, sessionState, encounterManager, battleController, battleResultSource, defeatConsequenceSource, battleSimulationEvents, gameManager, sceneRevealSignal, unitConditionRepository, currentLocationRepository, destinationAssigner, fieldActivityRepository, tripItinerary, worldMap, aftermathApplier, cargoSettlement, repairMode, cargoRecoveryPanel, tripEndProcessor, dayRout, tripSignals, legDifficulty);
            BindMovementViewHider(registrar, fieldUIController.MovementViewRoot);
            BindTripDialogue(registrar, sceneUIRoot);
            BindEventEncounter(registrar, sceneUIRoot, uiManager, panelRegistrar, fieldUIController, formationPanel.GetComponent<IFormationViewOnlyMode>());

            // Hub↔Field 씬 전환 연출(SceneTransitionEffectController)이 다음 전환 때 슬라이드시킬 대상을
            // 등록한다. 예전엔 이동 뷰 루트만 등록해 전투 뷰/패널/결과 팝업이 전환 중 제자리에 남았다 - 이제
            // Field UI 전체를 감싸는 전환 루트를 등록한다(Docs/설계/38번 §10). 인스톨러 미실행으로 전환 루트가
            // 없으면 예전 동작(이동 뷰만)으로 물러난다.
            if (!sceneUIRoot.TryGetElement<RectTransform>(FieldUIElementIds.ContentRoot, out var transitionRoot))
            {
                Debug.LogWarning($"Field UI에서 '{FieldUIElementIds.ContentRoot}' 요소를 찾을 수 없어 이동 뷰만 씬 전환 대상으로 등록한다(Tools > Game > Build Field Scene 실행 필요).");
                transitionRoot = fieldUIController.MovementViewRoot;
            }
            registrar.Resolve<ISceneTransitionContentRootRegistry>().RegisterContentRoot(ContentSceneId.Field, transitionRoot);
        }

        // 모달 팝업 채널에 등록한다(정비창·방향성 지시와 같은 채널) - 결과 정리 단계가 UIManager.Open/Close로만 연다.
        // 의존성이나 화면 요소가 없으면(인스톨러 미실행) 등록하지 않는다 - ② 회수 적재 단계는 건너뛰고 회수 물품은 임시보관에 남는다.
        private void RegisterCargoRecovery(SceneUIRoot sceneUIRoot, IPanelRegistrar panelRegistrar, ITradeGoodsInventoryRepository inventory, ITradeGoodsCargoSettlement settlement)
        {
            cargoRecoveryPanel?.Dispose();
            cargoRecoveryPanel = null;

            if (inventory == null || settlement == null)
            {
                Debug.LogWarning($"회수 적재 패널에 필요한 {nameof(ITradeGoodsInventoryRepository)}/{nameof(ITradeGoodsCargoSettlement)}가 연결되어 있지 않아 등록하지 못했다 - 전투 후 회수 적재 단계를 건너뛴다(Tools > Game > Build Bootstrap Scene).");
                return;
            }

            var spec = InventoryPopupSpecs.TradeGoods;
            var ok = InventoryArrangementElements.TryBind(sceneUIRoot, FieldCargoRecoveryUIElementIds.InventoryPrefix, spec.HasStaging, spec.HasSections, out var elements)
                & InventoryArrangementElements.TryGet(sceneUIRoot, FieldCargoRecoveryUIElementIds.DoneButton, out UnityEngine.UI.Button doneButton)
                & InventoryArrangementElements.TryGet(sceneUIRoot, FieldCargoRecoveryUIElementIds.ConfirmDialog, out ConfirmDialogView dialog);
            if (!ok)
            {
                Debug.LogWarning("회수 적재 패널 화면 요소가 없어 등록하지 못했다 - 전투 후 회수 적재 단계를 건너뛴다(Tools > Game > Build Field Scene).");
                return;
            }

            cargoRecoveryPanel = new FieldCargoRecoveryPanel(elements, doneButton, dialog, inventory, settlement);
            panelRegistrar.RegisterPopupPanel(cargoRecoveryPanel);
        }

        // 상행 대화 화면(설계 88 §2 씬 연결) - 진행자는 Bootstrap 상주라 Field를 로드할 때마다 이번 씬의 화면으로 바꿔 끼운다.
        private static void BindTripDialogue(IDependencyResolver registrar, SceneUIRoot sceneUIRoot)
        {
            if (!registrar.TryResolve<ITripDialogueViewBinder>(out var binder)) return;
            var hasView = sceneUIRoot.TryGetElement(FieldDialogueUIElementIds.Root, out FieldDialogueView dialogueView);
            var hasManual = sceneUIRoot.TryGetElement(FieldDialogueUIElementIds.ManualTalk, out FieldManualTalkView manualTalkView);
            if (!hasView || !hasManual)
            {
                Debug.LogWarning("상행 대화 화면 요소가 없어 대화가 표시되지 않는다(Tools > Game > Build Field Scene).");
            }
            binder.BindViews(hasView ? dialogueView : null, hasManual ? manualTalkView : null);
        }

        // 이벤트 화면(설계 89 §6.3) - 진행자는 Bootstrap 상주라 Field를 로드할 때마다 이번 씬의 화면 묶음으로 바꿔 끼운다. 두 화면은 정비창과 같은 모달 묶음.
        // 회수 적재 패널(무역품 공간 부족 정리)은 위에서 이번 씬 것으로 만든 것을 넘긴다 - 없으면 넘친 무역품이 임시 보관에 남는다.
        private void BindEventEncounter(IDependencyResolver registrar, SceneUIRoot sceneUIRoot, IUIManager uiManager, IPanelRegistrar panelRegistrar, IFieldUIController fieldUIController, IFormationViewOnlyMode viewOnly)
        {
            if (!registrar.TryResolve<IEventEncounterViewBinder>(out var binder)) return;
            var ok = sceneUIRoot.TryGetElement(FieldEventUIElementIds.DialogueRoot, out FieldEventDialogueView dialogue)
                & sceneUIRoot.TryGetElement(FieldEventUIElementIds.ChoiceRoot, out FieldEventChoiceView choice);
            if (!ok)
            {
                Debug.LogWarning("이벤트 화면 요소가 없어 이벤트가 발생하지 않는다(Tools > Game > Build Field Scene).");
                binder.BindScreens(null);
                return;
            }

            var dialoguePanel = new ViewRootPanel(UIPanelIds.EventDialogue, dialogue.gameObject);
            var choicePanel = new ViewRootPanel(UIPanelIds.EventChoice, choice.gameObject);
            panelRegistrar.RegisterPopupPanel(dialoguePanel);
            panelRegistrar.RegisterPopupPanel(choicePanel);
            binder.BindScreens(new FieldEventScreens(uiManager, dialogue, choice, dialoguePanel, choicePanel, cargoRecoveryPanel, fieldUIController, viewOnly));
        }

        // 이벤트 중 이동 뷰 가림(설계 89 §13.2) - 가림 상태는 Bootstrap 상주라 Field 로드마다 이번 씬의 이동 뷰로 새로 잇고 이전 객체 구독은 푼다.
        private void BindMovementViewHider(IDependencyResolver registrar, RectTransform movementViewRoot)
        {
            movementViewHider?.Dispose();
            movementViewHider = null;
            if (movementViewRoot == null || !registrar.TryResolve<IFieldUIHideReader>(out var reader)) return;
            var group = movementViewRoot.GetComponent<CanvasGroup>();
            if (group == null)
            {
                Debug.LogWarning("이동 뷰에 CanvasGroup이 없어 이벤트 중 가림이 동작하지 않는다(Tools > Game > Build Field Scene).");
                return;
            }
            movementViewHider = new FieldMovementViewHider(group, reader);
        }

        private void OnDestroy()
        {
            cargoRecoveryPanel?.Dispose();
            movementViewHider?.Dispose();
        }
    }
}
