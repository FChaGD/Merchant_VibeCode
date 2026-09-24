using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// UIManager 산하 컴포넌트. Hub 씬이 로드될 때 필요한 UI 배선만 담당한다 - UIManager가 Hub 전용
    /// 패널/데이터 의존성을 직접 알지 않도록 분리했다(SRP, Docs/Refactor/2026-08-26-공통.md 3단계 수정안).
    /// </summary>
    public class HubUIWiring : MonoBehaviour, IContentSceneUIWiring
    {
        public ContentSceneId SceneId => ContentSceneId.Hub;

        public void Wire(IDependencyResolver registrar, IUIManager uiManager, IPanelRegistrar panelRegistrar)
        {
            // 이 씬의 SceneUIRoot를 여기서 한 번만 찾아 아래 4개 컴포넌트 전부에 넘긴다 - 예전엔
            // 각자 SceneManager.GetSceneByName부터 다시 조회했다(DRY, Docs/Refactor/2026-09-08_Hub.md
            // §3 수정 J).
            if (!SceneUIRootLocator.TryFind(SceneNames.Hub, out var sceneUIRoot))
            {
                return;
            }

            var hubUIController = GetComponent<IHubUIController>();
            if (hubUIController == null)
            {
                throw new InvalidOperationException($"{nameof(HubUIWiring)}와 같은 GameObject에 {nameof(IHubUIController)} 구현체가 없다.");
            }

            var formationPanel = GetComponent<HubFormationPanel>();
            if (formationPanel == null)
            {
                throw new InvalidOperationException($"{nameof(HubUIWiring)}와 같은 GameObject에 {nameof(HubFormationPanel)} 구현체가 없다.");
            }

            var tripPanel = GetComponent<ITripPanel>();
            if (tripPanel == null)
            {
                throw new InvalidOperationException($"{nameof(HubUIWiring)}와 같은 GameObject에 {nameof(ITripPanel)} 구현체가 없다.");
            }

            var tacticsPanel = GetComponent<ITacticsPanel>();
            if (tacticsPanel == null)
            {
                throw new InvalidOperationException($"{nameof(HubUIWiring)}와 같은 GameObject에 {nameof(ITacticsPanel)} 구현체가 없다.");
            }

            var currencyHudController = GetComponent<IPlayerCurrencyHudController>();
            if (currencyHudController == null)
            {
                throw new InvalidOperationException($"{nameof(HubUIWiring)}와 같은 GameObject에 {nameof(IPlayerCurrencyHudController)} 구현체가 없다.");
            }

            var popupLayerGate = GetComponent<IPopupLayerGate>();
            if (popupLayerGate == null)
            {
                throw new InvalidOperationException($"{nameof(HubUIWiring)}와 같은 GameObject에 {nameof(IPopupLayerGate)} 구현체가 없다.");
            }

            var gameManager = registrar.Resolve<IGameManager>();
            // "상행 시작"/"상행 준비"/"배치" 버튼을 씬 전환 커튼이 완전히 걷힐 때까지 비활성화하는 데
            // 쓴다(사용자 확정) - HubUIController/TripPanel 둘 다 필요하므로 여기서 한 번만 조회한다.
            var sceneRevealSignal = registrar.Resolve<ISceneRevealSignal>();

            // 상행 관리 데이터 시스템이 아직 없어 선택적으로 조회한다 - 등록되면 자동으로 연결된다.
            registrar.TryResolve<ICaravanRosterProvider>(out var caravanRosterProvider);
            registrar.TryResolve<IFormationRepository>(out var formationRepository);
            registrar.TryResolve<IUnitConditionRepository>(out var unitConditionRepository);
            registrar.TryResolve<ITripInfoProvider>(out var tripInfoProvider);
            registrar.TryResolve<ITacticsRepository>(out var tacticsRepository);
            // ITripCurrentLocationReader 자체는 DI에 등록되지 않는다 - InMemoryTripCurrentLocationRepository는
            // ITripCurrentLocationRepository로만 등록되므로(IFormationReader/IFormationRepository와
            // 같은 판단 기준) 그 타입으로 조회해 읽기 전용 매개변수에 넘긴다.
            registrar.TryResolve<ITripCurrentLocationRepository>(out var currentLocationRepository);
            registrar.TryResolve<ITripDestinationAssigner>(out var destinationAssigner);
            // IPlayerCurrencyWallet으로 등록되어 있다(InMemoryPlayerCurrencyWallet.RegisterSelf) - 이
            // 컨트롤러는 조회 전용만 필요하므로 IPlayerCurrencyReader 타입으로만 넘긴다(ISP).
            registrar.TryResolve<IPlayerCurrencyWallet>(out var currencyWallet);
            // 마을별 시설 데이터 시스템이 아직 없어(Placeholder) 선택적으로 조회한다 - 없으면 전부 제공으로
            // 간주한다(CurrentTownFacilityFilter 참고).
            registrar.TryResolve<ITownFacilityAvailabilityReader>(out var townFacilityAvailability);
            var townFacilityFilter = new CurrentTownFacilityFilter(townFacilityAvailability, currentLocationRepository);

            hubUIController.RegisterHubUI(sceneUIRoot, uiManager, sceneRevealSignal, townFacilityFilter);
            currencyHudController.RegisterCurrencyUI(sceneUIRoot, currencyWallet);
            HubInventoryShortcutBinder.Bind(sceneUIRoot, uiManager);

            formationPanel.RegisterFormationUI(sceneUIRoot, caravanRosterProvider, formationRepository, unitConditionRepository, uiManager);
            panelRegistrar.RegisterPopupPanel(formationPanel);

            tripPanel.RegisterTripUI(sceneUIRoot, uiManager, gameManager, formationRepository, tripInfoProvider, sceneRevealSignal, currentLocationRepository, destinationAssigner);
            panelRegistrar.RegisterPopupPanel(tripPanel);

            tacticsPanel.RegisterTacticsUI(sceneUIRoot, tacticsRepository, uiManager);
            panelRegistrar.RegisterPopupPanel(tacticsPanel);

            // 마을 카테고리 depth - 카테고리마다 패널 인스턴스가 따로지만 화면 요소는 하나를 공유한다
            // (Docs/설계/37번 §4.2). 요소가 없으면(인스톨러 미실행) 카테고리 depth만 빠지고 나머지는 정상 동작한다.
            if (TownCategoryDepthElements.TryBind(sceneUIRoot, out var townCategoryDepth))
            {
                foreach (var categoryId in TownFacilityCatalog.CategoryIds)
                {
                    panelRegistrar.RegisterDepthPanel(new TownCategoryPanel(categoryId, townCategoryDepth, uiManager, townFacilityFilter));
                }
            }

            // 팝업 축(Docs/설계/38번 §5·§6) - 모달 팝업이 열리면 DepthLayer/PersistentLayer를 숨긴다.
            // PersistentLayer에는 인벤토리 버튼이 있다. PopupExemptLayer(재화 HUD)와 PopupLayer는 대상이 아니다.
            popupLayerGate.Register(uiManager, CollectLayers(sceneUIRoot, HubUIElementIds.DepthLayer, HubUIElementIds.PersistentLayer));

            // Hub↔Field 씬 전환 연출(SceneTransitionEffectController)이 다음 전환 때 슬라이드시킬
            // 대상을 등록한다 - 씬을 다시 로드할 때마다 최신 참조로 갱신된다(Docs/설계/10-2026-08-26-씬전환_연출_아키텍처.md §8).
            // 반드시 맨 마지막에 둔다 - 여기서 예외가 나도(예: 설치 도구 미실행) 위 핵심 패널 등록은
            // 이미 끝난 뒤라 Hub UI 자체는 정상 동작한다.
            registrar.Resolve<ISceneTransitionContentRootRegistry>().RegisterContentRoot(ContentSceneId.Hub, hubUIController.ContentRoot);
        }

        private static List<CanvasGroup> CollectLayers(SceneUIRoot sceneUIRoot, params string[] layerIds)
        {
            var layers = new List<CanvasGroup>();
            foreach (var id in layerIds)
            {
                if (sceneUIRoot.TryGetElement<CanvasGroup>(id, out var layer))
                {
                    layers.Add(layer);
                }
                else
                {
                    Debug.LogWarning($"Hub UI에서 '{id}' 레이어(CanvasGroup)를 찾을 수 없다(Tools > Game > Build Hub Scene).");
                }
            }
            return layers;
        }
    }
}
