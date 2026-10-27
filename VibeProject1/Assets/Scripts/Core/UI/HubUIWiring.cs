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

        // 인벤토리 팝업 패널(plain C#)은 Hub 로드마다 새로 만들어진다 - 창 위치는 영속 컴포넌트인 이 배선이
        // 소유해 게임 실행 중 유지하고(Docs/설계/40번 §5.1), 이전 패널은 저장소 구독 해제를 위해 Dispose한다.
        private readonly PopupWindowPositionStore inventoryWindowPositions = new();
        private readonly List<IDisposable> inventoryPopups = new();
        // 누른 인벤토리 팝업을 맨 위로 올린다(Docs/설계/46번) - 포인터 누르기 구독을 한 번만 하고 Hub 로드마다 창 목록만 교체한다. InputManager가 없으면 만들지 않는다.
        private PopupFocusOnPress inventoryPopupFocus;
        // 시설 화면(무역품 구매·용병·마구간·숙소·상인조합)과 골드 변환 모달 - 바인딩 단위로 한 번 만들고 Hub 로드마다 Bind한다
        // (Docs/Refactor/2026-10-26_씬전용.md F-1). 패널이 Bootstrap 상주 저장소 이벤트를 구독하므로 바인딩이 이전 패널을 해제한다.
        private List<IHubScreenBinding> screenBindings;
        // 휴식 진행 연출 속도(설계 103번 §4.2) - 연출 값이라 휴식 서비스(Placeholder)를 바꿔도 남도록 배선에 두고 숙소 바인딩에 넘긴다.
        [SerializeField] private RestPacingSettings restPacing = new();
        // 인벤토리 상시 호출 버튼·키(설계 99번 §5.1) - 키 구독을 들고 있어 한 번만 만들고 Hub 로드마다 다시 묶는다.
        private HubInventoryShortcutBinder inventoryShortcuts;

        public void Wire(IDependencyResolver registrar, IUIManager uiManager, IPanelRegistrar panelRegistrar, IInventoryPopupRegistrar inventoryRegistrar)
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

            var dateHudController = GetComponent<IGameDateHudController>();
            if (dateHudController == null)
            {
                throw new InvalidOperationException($"{nameof(HubUIWiring)}와 같은 GameObject에 {nameof(IGameDateHudController)} 구현체가 없다.");
            }

            var layerHideGate = GetComponent<ILayerHideGate>();
            if (layerHideGate == null)
            {
                throw new InvalidOperationException($"{nameof(HubUIWiring)}와 같은 GameObject에 {nameof(ILayerHideGate)} 구현체가 없다.");
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
            // 월드 지도(Docs/설계/69번 §4.3) - 지역 지도 표시와 도착지 도달 판정.
            registrar.TryResolve<IWorldMapReader>(out var worldMap);
            registrar.TryResolve<ITripRouteReader>(out var routeReader);
            // 상행 구간 계획 확정(Docs/설계/76번 §5).
            registrar.TryResolve<ITripDeparture>(out var tripDeparture);
            // 골드 보유(설계 83번 §3.3) - 계약마다 따로 등록되어 있어 소비자가 쓰는 타입으로 각각 조회한다.
            registrar.TryResolve<IGoldHoldingsReader>(out var goldHoldings);
            registrar.TryResolve<IGoldDepartureSettlement>(out var goldDeparture);
            registrar.TryResolve<IDateReader>(out var dateReader); // 게임 날짜(설계 85번 §4.1)
            // 마을별 시설 데이터 시스템이 아직 없어(Placeholder) 선택적으로 조회한다 - 없으면 전부 제공으로
            // 간주한다(CurrentTownFacilityFilter 참고).
            registrar.TryResolve<ITownFacilityAvailabilityReader>(out var townFacilityAvailability);
            var townFacilityFilter = new CurrentTownFacilityFilter(townFacilityAvailability, currentLocationRepository);
            // 입력 구독(설계 99번 §5) - 없으면 키 단축키·Q 회전·누른 팝업 맨 위로만 빠지고 클릭은 정상 동작한다.
            registrar.TryResolve<IInputManager>(out var input);
            // 버리기 경고의 골드 상자 줄(설계 102번 §4.2) - 상단 물류품 화면에만 넘긴다. 없으면 골드 줄 없이 버린다.
            registrar.TryResolve<IGoldItemInfo>(out var goldItemInfo);
            // 인벤토리 팝업 저장소(Placeholder라 선택적, Docs/설계/42번 §4.5) - "스펙 → 저장소" 대응표 한 곳(Docs/Refactor/2026-10-26_씬전용.md F-2).
            var inventoryPopupSources = HubInventorySources.Resolve(registrar);

            // 임시 보관이 켜진 팝업의 저장소만 "상행 시작" 경고·버림 대상에 넣는다(설계 40번 §5.5, 2026-10-15 비활성→경고 전환) - 스펙이 조건을 결정하므로
            // 팝업의 임시 보관 여부가 바뀌어도 여기를 고칠 필요가 없다.
            var inventoryStaging = new List<IInventoryStagingDiscarder>();
            foreach (var source in inventoryPopupSources)
            {
                if (source.Spec.HasStaging) inventoryStaging.Add(source.Arrangement);
            }

            hubUIController.RegisterHubUI(sceneUIRoot, uiManager, sceneRevealSignal, townFacilityFilter);
            currencyHudController.RegisterCurrencyUI(sceneUIRoot, goldHoldings, uiManager, caravanRosterProvider);
            dateHudController.RegisterDateUI(sceneUIRoot, dateReader);
            inventoryShortcuts ??= new HubInventoryShortcutBinder(input);
            inventoryShortcuts.Bind(sceneUIRoot, uiManager);

            formationPanel.RegisterFormationUI(sceneUIRoot, caravanRosterProvider, formationRepository, unitConditionRepository, uiManager);
            panelRegistrar.RegisterPopupPanel(formationPanel);

            tripPanel.RegisterTripUI(sceneUIRoot, uiManager, gameManager, formationRepository, tripInfoProvider, sceneRevealSignal, currentLocationRepository, destinationAssigner, inventoryStaging, worldMap, routeReader, tripDeparture, caravanRosterProvider, goldHoldings, goldDeparture);
#if UNITY_EDITOR
            if (registrar.TryResolve<IWorldMapEditor>(out var worldMapEditor)) tripPanel.RegisterDebugMapEditor(worldMapEditor);
#endif
            panelRegistrar.RegisterPopupPanel(tripPanel);

            tacticsPanel.RegisterTacticsUI(sceneUIRoot, tacticsRepository, uiManager);
            panelRegistrar.RegisterPopupPanel(tacticsPanel);

            // 마을 카테고리 depth - 카테고리마다 패널 인스턴스가 따로지만 화면 요소는 하나를 공유한다
            // (Docs/설계/37번 §4.2). 요소가 없으면(인스톨러 미실행) 카테고리 depth만 빠지고 나머지는 정상 동작한다.
            if (TownCategoryDepthElements.TryBind(sceneUIRoot, out var townCategoryDepth))
            {
                foreach (var categoryId in TownFacilityCatalog.CategoryIds)
                {
                    panelRegistrar.RegisterDepthPanel(new TownCategoryPanel(categoryId, townCategoryDepth, uiManager, uiManager, townFacilityFilter));
                }
            }

            RegisterInventoryPopups(sceneUIRoot, uiManager, inventoryRegistrar, inventoryPopupSources, input, goldItemInfo);
            screenBindings ??= new List<IHubScreenBinding>
            {
                new TradeGoodsMarketScreenBinding(),
                new MercenaryContactScreenBinding(),
                new StableScreenBinding(),
                new InnScreenBinding(restPacing),
                new MerchantGuildScreenBinding(),
                new GoldConversionBinding(),
            };
            var screenContext = new HubScreenContext(registrar, sceneUIRoot, uiManager, panelRegistrar, GetTooltipController(), inventoryPopupSources, input, goldItemInfo, currentLocationRepository, this);
            foreach (var binding in screenBindings) binding.Bind(screenContext);

            // 팝업 축(Docs/설계/38번 §5·§6, Docs/Refactor/2026-10-26_공통.md B-4) - 모달 팝업이나 시설 화면이 열리면 DepthLayer/PersistentLayer를 숨긴다.
            // PersistentLayer에는 인벤토리 버튼이 있다. PopupExemptLayer(재화 HUD)는 숨기지 않고 휴식 진행 중 입력만 막는다. PopupLayer는 대상이 아니다.
            var hiddenOnPopup = CollectLayers(sceneUIRoot, HubUIElementIds.DepthLayer, HubUIElementIds.PersistentLayer);
            var hideRules = new List<LayerHideRule>
            {
                new LayerHideRule(UIHideConditions.ModalPopupOpen(uiManager), hiddenOnPopup),
                new LayerHideRule(UIHideConditions.ScreenOpen(uiManager), hiddenOnPopup),
            };
            // 바인딩이 기여하는 규칙(숙소 - 휴식 진행 중 재화·날짜 HUD 입력 차단, 설계 103번 §3).
            foreach (var binding in screenBindings) (binding as IHideRuleSource)?.CollectHideRules(sceneUIRoot, hideRules);
            layerHideGate.Register(hideRules);

            // Hub↔Field 씬 전환 연출(SceneTransitionEffectController)이 다음 전환 때 슬라이드시킬
            // 대상을 등록한다 - 씬을 다시 로드할 때마다 최신 참조로 갱신된다(Docs/설계/10-2026-08-26-씬전환_연출_아키텍처.md §8).
            // 반드시 맨 마지막에 둔다 - 여기서 예외가 나도(예: 설치 도구 미실행) 위 핵심 패널 등록은
            // 이미 끝난 뒤라 Hub UI 자체는 정상 동작한다.
            registrar.Resolve<ISceneTransitionContentRootRegistry>().RegisterContentRoot(ContentSceneId.Hub, hubUIController.ContentRoot);
        }

        // 비모달 인벤토리 팝업(Docs/설계/40번 §6, 42번 §4.5). 화면 요소가 없으면(인스톨러 미실행) 그 팝업만 건너뛴다 -
        // 해당 인벤토리 버튼은 기존처럼 "팝업 미등록" 경고만 낸다.
        private void RegisterInventoryPopups(SceneUIRoot sceneUIRoot, IUIManager uiManager, IInventoryPopupRegistrar inventoryRegistrar, List<InventoryPopupSource> sources, IInputManager input, IGoldItemInfo goldItemInfo)
        {
            foreach (var previous in inventoryPopups) previous.Dispose();
            inventoryPopups.Clear();
            var windowRoots = new List<RectTransform>();

            foreach (var source in sources)
            {
                if (!InventoryPopupElements.TryBind(sceneUIRoot, source.Spec, out var elements)) continue;

                var popup = new InventoryPopupPanel(source.Spec, elements, source.Reader, source.Arrangement, uiManager, inventoryWindowPositions, input, source.Spec.PopupId == InventoryPopupIds.TradeGoods ? goldItemInfo : null);
                inventoryPopups.Add(popup);
                windowRoots.Add(popup.WindowRoot);
                inventoryRegistrar.RegisterInventoryPopup(popup);
            }

            if (input != null) inventoryPopupFocus ??= new PopupFocusOnPress(input);
            inventoryPopupFocus?.Rebind(windowRoots);
        }

        private ITooltipController GetTooltipController()
        {
            var tooltips = GetComponent<ITooltipController>();
            if (tooltips == null)
            {
                throw new InvalidOperationException($"{nameof(HubUIWiring)}와 같은 GameObject에 {nameof(ITooltipController)} 구현체가 없다.");
            }
            return tooltips;
        }

        private void OnDestroy()
        {
            if (screenBindings != null) foreach (var binding in screenBindings) binding.Dispose();
            foreach (var popup in inventoryPopups) popup.Dispose();
            inventoryPopups.Clear();
            inventoryPopupFocus?.Dispose();
            inventoryShortcuts?.Dispose();
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
