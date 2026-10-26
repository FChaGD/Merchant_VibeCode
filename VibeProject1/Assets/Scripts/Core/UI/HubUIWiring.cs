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
        // 무역품 구매 화면(Docs/설계/50번 §6.5) - 인벤토리 팝업과 같은 이유로 Hub 로드마다 새로 만들고 이전 것은 Dispose한다.
        private TradeGoodsMarketPanel tradeGoodsMarketPanel;
        // 용병단 접촉 화면(Docs/설계/54번 §8.4) - 무역품 구매 화면과 같은 이유로 Hub 로드마다 새로 만들고 이전 것은 Dispose한다.
        private MercenaryContactPanel mercenaryContactPanel;
        // 마구간 화면(Docs/설계/56번 §7.2) - 같은 이유로 Hub 로드마다 새로 만들고 이전 것은 Dispose한다.
        private StablePanel stablePanel;
        // 골드 변환 모달(설계 83번 §6.2) - 시설 화면과 같은 이유로 Hub 로드마다 새로 만들고 이전 것은 Dispose한다.
        private GoldConversionPanel goldConversionPanel;
        // 숙소·휴식 진행·상인조합(설계 103번 §4.7) - 시설 화면과 같은 이유로 Hub 로드마다 새로 만들고 이전 것은 Dispose한다.
        private InnPanel innPanel;
        private InnRestProgressPanel innRestProgressPanel;
        private MerchantGuildPanel merchantGuildPanel;
        // 휴식 진행 연출 속도(설계 103번 §4.2) - 연출 값이라 휴식 서비스(Placeholder)를 바꿔도 남도록 배선에 둔다.
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
            registrar.TryResolve<IGoldSpender>(out var goldSpender);
            registrar.TryResolve<IGoldBoxConverter>(out var goldConverter);
            registrar.TryResolve<IGoldDepartureSettlement>(out var goldDeparture);
            registrar.TryResolve<IDateReader>(out var dateReader); // 게임 날짜(설계 85번 §4.1)
            // 마을별 시설 데이터 시스템이 아직 없어(Placeholder) 선택적으로 조회한다 - 없으면 전부 제공으로
            // 간주한다(CurrentTownFacilityFilter 참고).
            registrar.TryResolve<ITownFacilityAvailabilityReader>(out var townFacilityAvailability);
            var townFacilityFilter = new CurrentTownFacilityFilter(townFacilityAvailability, currentLocationRepository);
            // 인벤토리 데이터 시스템이 아직 Placeholder라 선택적으로 조회한다(Docs/설계/42번 §4.5, 44번 §3). 새 인벤토리
            // 팝업이 생기면 스펙과 저장소 쌍을 이 목록에 추가만 한다.
            registrar.TryResolve<ITradeGoodsInventoryRepository>(out var tradeGoodsInventory);
            registrar.TryResolve<IEquipmentInventoryRepository>(out var equipmentInventory);
            registrar.TryResolve<IConsumableInventoryRepository>(out var consumableInventory);
            registrar.TryResolve<IPersonalItemInventoryRepository>(out var personalItemInventory);
            // 시설 판매 목록과 마을 재고(설계 81번 §3.5) - 없으면 해당 화면을 등록하지 않거나 재고 차감 없이 동작한다.
            registrar.TryResolve<ITownShopStockReader>(out var townShopStockReader);
            registrar.TryResolve<ITownStockConsumer>(out var townStockConsumer);
            registrar.TryResolve<ITownStockReader>(out var townStockReader);
            // 캐릭터 고용(설계 54번 §8.4) - 로스터·후보 시스템이 아직 Placeholder라 선택적으로 조회한다.
            registrar.TryResolve<IMercenaryCandidateReader>(out var mercenaryCandidateReader);
            registrar.TryResolve<IHiredCharacterRoster>(out var hiredCharacterRoster);
            registrar.TryResolve<ICharacterCatalogReader>(out var characterCatalog);
            registrar.TryResolve<IMercenaryClassIconReader>(out var mercenaryClassIconReader);
            // 마차·시설 구매(설계 56번 §7.2) - 로스터·판매 시스템이 아직 Placeholder라 선택적으로 조회한다.
            // 숙소 휴식·상인조합 게시판·마구간 매각(설계 103번 §4.7) - Placeholder라 선택적으로 조회한다.
            registrar.TryResolve<IInnRestService>(out var innRestService);
            registrar.TryResolve<IGuildQuestBoard>(out var guildQuestBoard);
            registrar.TryResolve<ICaravanAssetSaleService>(out var caravanAssetSaleService);
            registrar.TryResolve<ICaravanAssetCandidateReader>(out var caravanAssetCandidateReader);
            registrar.TryResolve<IOwnedCaravanAssetRoster>(out var ownedCaravanAssetRoster);
            registrar.TryResolve<ICaravanAssetCatalogReader>(out var caravanAssetCatalog);
            registrar.TryResolve<ICaravanAssetIconReader>(out var caravanAssetIconReader);
            // 마차·시설 내구도(설계 91번 §5.2) - 없으면 마구간 보유 영역이 "값 없음"을 보이고 수리를 막는다.
            registrar.TryResolve<ICaravanAssetDurabilityReader>(out var caravanAssetDurabilityReader);
            registrar.TryResolve<ICaravanAssetDurabilityWriter>(out var caravanAssetDurabilityWriter);
            // 입력 구독(설계 99번 §5) - 없으면 키 단축키·Q 회전·누른 팝업 맨 위로만 빠지고 클릭은 정상 동작한다.
            registrar.TryResolve<IInputManager>(out var input);
            // 버리기 경고의 골드 상자 줄(설계 102번 §4.2) - 상단 물류품 화면에만 넘긴다. 없으면 골드 줄 없이 버린다.
            registrar.TryResolve<IGoldItemInfo>(out var goldItemInfo);
            var inventoryPopupSources = new List<InventoryPopupSource>();
            AddInventoryPopupSource(inventoryPopupSources, InventoryPopupSpecs.TradeGoods, tradeGoodsInventory, tradeGoodsInventory, nameof(ITradeGoodsInventoryRepository));
            AddInventoryPopupSource(inventoryPopupSources, InventoryPopupSpecs.Equipment, equipmentInventory, equipmentInventory, nameof(IEquipmentInventoryRepository));
            AddInventoryPopupSource(inventoryPopupSources, InventoryPopupSpecs.Consumable, consumableInventory, consumableInventory, nameof(IConsumableInventoryRepository));
            AddInventoryPopupSource(inventoryPopupSources, InventoryPopupSpecs.PersonalItem, personalItemInventory, personalItemInventory, nameof(IPersonalItemInventoryRepository));

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
            RegisterTradeGoodsMarket(sceneUIRoot, uiManager, panelRegistrar, tradeGoodsInventory, goldSpender, townShopStockReader, townStockConsumer, currentLocationRepository, input, goldItemInfo);
            RegisterMercenaryContact(sceneUIRoot, uiManager, panelRegistrar, goldSpender, mercenaryCandidateReader, hiredCharacterRoster, characterCatalog, mercenaryClassIconReader, currentLocationRepository);
            RegisterStable(sceneUIRoot, uiManager, panelRegistrar, goldSpender, caravanAssetCandidateReader, ownedCaravanAssetRoster, caravanAssetCatalog, caravanAssetIconReader, townStockReader, townStockConsumer, currentLocationRepository, caravanAssetDurabilityReader, caravanAssetDurabilityWriter, caravanAssetSaleService, tradeGoodsInventory);
            RegisterInn(sceneUIRoot, uiManager, panelRegistrar, inventoryPopupSources, innRestService, goldHoldings, dateReader, input);
            RegisterMerchantGuild(sceneUIRoot, uiManager, panelRegistrar, guildQuestBoard);
            RegisterGoldConversion(sceneUIRoot, uiManager, panelRegistrar, goldHoldings, goldConverter);

            // 팝업 축(Docs/설계/38번 §5·§6, Docs/Refactor/2026-10-26_공통.md B-4) - 모달 팝업이나 시설 화면이 열리면 DepthLayer/PersistentLayer를 숨긴다.
            // PersistentLayer에는 인벤토리 버튼이 있다. PopupExemptLayer(재화 HUD)와 PopupLayer는 대상이 아니다.
            var hiddenOnPopup = CollectLayers(sceneUIRoot, HubUIElementIds.DepthLayer, HubUIElementIds.PersistentLayer);
            layerHideGate.Register(new[]
            {
                new LayerHideRule(UIHideConditions.ModalPopupOpen(uiManager), hiddenOnPopup),
                new LayerHideRule(UIHideConditions.ScreenOpen(uiManager), hiddenOnPopup),
            });

            // Hub↔Field 씬 전환 연출(SceneTransitionEffectController)이 다음 전환 때 슬라이드시킬
            // 대상을 등록한다 - 씬을 다시 로드할 때마다 최신 참조로 갱신된다(Docs/설계/10-2026-08-26-씬전환_연출_아키텍처.md §8).
            // 반드시 맨 마지막에 둔다 - 여기서 예외가 나도(예: 설치 도구 미실행) 위 핵심 패널 등록은
            // 이미 끝난 뒤라 Hub UI 자체는 정상 동작한다.
            registrar.Resolve<ISceneTransitionContentRootRegistry>().RegisterContentRoot(ContentSceneId.Hub, hubUIController.ContentRoot);
        }

        // 인벤토리 팝업 1종을 만드는 데 필요한 것 - 저장소는 조회(IInventoryReader)와 정리 조작(IInventoryArrangement)을
        // 같은 객체가 구현하지만, 패널이 요구하는 계약만 드러나도록 따로 담는다.
        private readonly struct InventoryPopupSource
        {
            public readonly InventoryPopupSpec Spec;
            public readonly IInventoryReader Reader;
            public readonly IInventoryArrangement Arrangement;

            public InventoryPopupSource(InventoryPopupSpec spec, IInventoryReader reader, IInventoryArrangement arrangement)
            {
                Spec = spec;
                Reader = reader;
                Arrangement = arrangement;
            }
        }

        private static void AddInventoryPopupSource(List<InventoryPopupSource> sources, InventoryPopupSpec spec, IInventoryReader reader, IInventoryArrangement arrangement, string repositoryName)
        {
            if (reader == null || arrangement == null)
            {
                Debug.LogWarning($"{repositoryName}가 연결되어 있지 않아 '{spec.Title}' 팝업을 등록하지 못했다.");
                return;
            }

            sources.Add(new InventoryPopupSource(spec, reader, arrangement));
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

        // 시설 화면은 시설 화면 채널로 등록한다(Docs/Refactor/2026-10-26_공통.md B-1) - 재화 패널 외 UI 숨김은 레이어 숨김 규칙이, 복귀는 채널 스택이 처리하고, 모달(골드 변환 등)이 열려도 닫히지 않는다.
        // 의존성이나 화면 요소가 없으면(인스톨러 미실행) 등록하지 않는다 - 시설 버튼은 "등록되지 않은 패널" 경고만 낸다.
        private void RegisterTradeGoodsMarket(SceneUIRoot sceneUIRoot, IUIManager uiManager, IPanelRegistrar panelRegistrar, ITradeGoodsInventoryRepository inventory, IGoldSpender gold, ITownShopStockReader stockReader, ITownStockConsumer stockConsumer, ITripCurrentLocationReader currentLocation, IInputManager input, IGoldItemInfo goldItemInfo)
        {
            tradeGoodsMarketPanel?.Dispose();
            tradeGoodsMarketPanel = null;

            if (inventory == null || gold == null || stockReader == null)
            {
                Debug.LogWarning($"무역품 구매 화면에 필요한 {nameof(ITradeGoodsInventoryRepository)}/{nameof(IGoldSpender)}/{nameof(ITownShopStockReader)}가 연결되어 있지 않아 등록하지 못했다(Tools > Game > Build Bootstrap Scene).");
                return;
            }

            if (!TradeGoodsMarketElements.TryBind(sceneUIRoot, InventoryPopupSpecs.TradeGoods.HasStaging, InventoryPopupSpecs.TradeGoods.HasSections, out var elements)) return;

            // 구매 버튼 비활성 사유 툴팁(Docs/Refactor/2026-10-26_공통.md D-1) - 요소가 없으면 사유만 빠진다.
            var buyTooltip = ButtonTooltip.TryRegister(GetTooltipController(), elements.BuyButton, sceneUIRoot, TradeGoodsMarketUIElementIds.BuyTooltip, TradeGoodsMarketUIElementIds.BuyTooltipText);
            tradeGoodsMarketPanel = new TradeGoodsMarketPanel(elements, inventory, gold, stockReader, stockConsumer, currentLocation, uiManager, buyTooltip, input, goldItemInfo);
            panelRegistrar.RegisterScreenPanel(tradeGoodsMarketPanel);
        }

        // 무역품 구매 화면과 같은 등록 방식(시설 화면 채널). 직업 아이콘 제공자는 없어도 된다(아이콘 없이 표시).
        private void RegisterMercenaryContact(SceneUIRoot sceneUIRoot, IUIManager uiManager, IPanelRegistrar panelRegistrar, IGoldSpender gold, IMercenaryCandidateReader candidateReader, IHiredCharacterRoster roster, ICharacterCatalogReader catalog, IMercenaryClassIconReader iconReader, ITripCurrentLocationReader currentLocation)
        {
            mercenaryContactPanel?.Dispose();
            mercenaryContactPanel = null;

            if (gold == null || candidateReader == null || roster == null || catalog == null)
            {
                Debug.LogWarning($"용병단 접촉 화면에 필요한 {nameof(IGoldSpender)}/{nameof(IMercenaryCandidateReader)}/{nameof(IHiredCharacterRoster)}/{nameof(ICharacterCatalogReader)}가 연결되어 있지 않아 등록하지 못했다(Tools > Game > Build Bootstrap Scene).");
                return;
            }

            if (!RosterShopElements.TryBind(sceneUIRoot, RosterShopUIElementIds.MercenaryContactPrefix, out var elements)) return;

            var hireTooltip = RegisterRosterShopTooltip(sceneUIRoot, elements, RosterShopUIElementIds.MercenaryContactPrefix);
            mercenaryContactPanel = new MercenaryContactPanel(elements, gold, candidateReader, roster, catalog, iconReader, currentLocation, uiManager, hireTooltip);
            panelRegistrar.RegisterScreenPanel(mercenaryContactPanel);
        }

        // 용병단 접촉 화면과 같은 등록 방식(시설 화면 채널, 목록형 구매 화면 요소 공유). 종류 아이콘 제공자는 없어도 된다.
        private void RegisterStable(SceneUIRoot sceneUIRoot, IUIManager uiManager, IPanelRegistrar panelRegistrar, IGoldSpender gold, ICaravanAssetCandidateReader candidateReader, IOwnedCaravanAssetRoster roster, ICaravanAssetCatalogReader catalog, ICaravanAssetIconReader iconReader, ITownStockReader stockReader, ITownStockConsumer stockConsumer, ITripCurrentLocationReader currentLocation, ICaravanAssetDurabilityReader durabilityReader, ICaravanAssetDurabilityWriter durabilityWriter, ICaravanAssetSaleService saleService, IInventoryReader cargo)
        {
            stablePanel?.Dispose();
            stablePanel = null;

            if (gold == null || candidateReader == null || roster == null || catalog == null || stockReader == null || stockConsumer == null)
            {
                Debug.LogWarning($"마구간 화면에 필요한 {nameof(IGoldSpender)}/{nameof(ICaravanAssetCandidateReader)}/{nameof(IOwnedCaravanAssetRoster)}/{nameof(ICaravanAssetCatalogReader)}/{nameof(ITownStockReader)}/{nameof(ITownStockConsumer)}가 연결되어 있지 않아 등록하지 못했다(Tools > Game > Build Bootstrap Scene).");
                return;
            }

            if (!RosterShopElements.TryBind(sceneUIRoot, RosterShopUIElementIds.StablePrefix, out var elements, requireOwnedListLabel: false)) return;

            StableOwnedSection ownedSection = null;
            if (StableOwnedElements.TryBind(sceneUIRoot, out var ownedElements))
            {
                // 매각(설계 103번 §4.6, 2026-10-26 좌측 개체 정보로 이동) - 요소가 없으면(인스톨러 미실행) 수리만.
                StableSaleController saleController = null;
                if (sceneUIRoot.TryGetElement<TMPro.TMP_Text>(StableUIElementIds.SalePrice, out var salePrice)
                    && sceneUIRoot.TryGetElement<UnityEngine.UI.Button>(StableUIElementIds.SellButton, out var sellButton)
                    && sceneUIRoot.TryGetElement<ConfirmDialogView>(StableUIElementIds.SaleConfirmDialog, out var saleDialog))
                {
                    if (saleService == null) Debug.LogWarning($"{nameof(ICaravanAssetSaleService)}가 없어 마구간 매각 버튼을 막는다(Tools > Game > Build Bootstrap Scene).");
                    var sellTooltip = ButtonTooltip.TryRegister(GetTooltipController(), sellButton, sceneUIRoot, StableUIElementIds.SellTooltip, StableUIElementIds.SellTooltipText);
                    saleController = new StableSaleController(salePrice, sellTooltip, sellButton, saleDialog, saleService, roster, cargo);
                }
                else
                {
                    Debug.LogWarning($"Hub UI에서 마구간 매각 요소('{StableUIElementIds.SalePrice}'/'{StableUIElementIds.SellButton}'/'{StableUIElementIds.SaleConfirmDialog}')를 찾을 수 없어 매각 없이 등록한다(Tools > Game > Build Hub Scene).");
                }

                CaravanAssetRepairService repair = null;
                if (durabilityReader != null && durabilityWriter != null) repair = new CaravanAssetRepairService(gold, roster, durabilityReader, durabilityWriter);
                else Debug.LogWarning($"마구간 수리에 필요한 {nameof(ICaravanAssetDurabilityReader)}/{nameof(ICaravanAssetDurabilityWriter)}가 없어 수리를 막는다(Tools > Game > Build Bootstrap Scene).");
                var tooltips = GetTooltipController();
                var repairTooltip = ButtonTooltip.TryRegister(tooltips, ownedElements.RepairButton, sceneUIRoot, StableUIElementIds.RepairTooltip, StableUIElementIds.RepairTooltipText);
                var repairAllTooltip = ButtonTooltip.TryRegister(tooltips, ownedElements.RepairAllButton, sceneUIRoot, StableUIElementIds.RepairAllTooltip, StableUIElementIds.RepairAllTooltipText);
                ownedSection = new StableOwnedSection(ownedElements, roster, catalog, durabilityReader, repair, gold, saleController, repairTooltip, repairAllTooltip);
            }

            var purchaseTooltip = RegisterRosterShopTooltip(sceneUIRoot, elements, RosterShopUIElementIds.StablePrefix);
            stablePanel = new StablePanel(elements, gold, candidateReader, roster, ownedSection, iconReader, stockReader, stockConsumer, currentLocation, uiManager, purchaseTooltip);
            panelRegistrar.RegisterScreenPanel(stablePanel);
        }

        // 숙소 + 휴식 진행 화면(설계 103번 §4.3·§4.4) - 숙소의 "휴식"이 진행 화면을 열므로 함께 만든다. 시설 화면과 같은 등록 방식(시설 화면 채널).
        private void RegisterInn(SceneUIRoot sceneUIRoot, IUIManager uiManager, IPanelRegistrar panelRegistrar, List<InventoryPopupSource> inventorySources, IInnRestService rest, IGoldHoldingsReader gold, IDateReader date, IInputManager input)
        {
            innPanel?.Dispose();
            innPanel = null;
            innRestProgressPanel?.Dispose();
            innRestProgressPanel = null;

            if (rest == null || gold == null)
            {
                Debug.LogWarning($"숙소 화면에 필요한 {nameof(IInnRestService)}/{nameof(IGoldHoldingsReader)}가 연결되어 있지 않아 등록하지 못했다(Tools > Game > Build Bootstrap Scene).");
                return;
            }

            // 탭 = 장비·소모품·개인 물품 인벤토리(기획 94 4-3). 저장소가 없는 탭이 있으면 화면 전체를 등록하지 않는다.
            var tabSpecs = InventoryPopupSpecs.InnTabs;
            var tabSources = new List<(IInventoryReader Reader, IInventoryArrangement Arrangement)>();
            foreach (var spec in tabSpecs)
            {
                var found = inventorySources.Find(s => s.Spec.PopupId == spec.PopupId);
                if (found.Reader == null)
                {
                    Debug.LogWarning($"숙소 '{spec.Title}' 탭 저장소가 없어 숙소 화면을 등록하지 못했다(Tools > Game > Build Bootstrap Scene).");
                    return;
                }
                tabSources.Add((found.Reader, found.Arrangement));
            }

            if (!InnElements.TryBind(sceneUIRoot, tabSpecs, out var innElements) || !InnRestProgressElements.TryBind(sceneUIRoot, out var progressElements)) return;

            sceneUIRoot.TryGetElement<CanvasGroup>(HubUIElementIds.PopupExemptLayer, out var hudLayer);
            innRestProgressPanel = new InnRestProgressPanel(progressElements, rest, date, gold, uiManager, this, restPacing, hudLayer);
            panelRegistrar.RegisterScreenPanel(innRestProgressPanel);
            innPanel = new InnPanel(innElements, tabSources, rest, gold, GetTooltipController(), innRestProgressPanel, uiManager, input);
            panelRegistrar.RegisterScreenPanel(innPanel);
        }

        // 상인조합(설계 103번 §4.5) - 시설 화면과 같은 등록 방식(시설 화면 채널).
        private void RegisterMerchantGuild(SceneUIRoot sceneUIRoot, IUIManager uiManager, IPanelRegistrar panelRegistrar, IGuildQuestBoard board)
        {
            merchantGuildPanel?.Dispose();
            merchantGuildPanel = null;

            if (board == null)
            {
                Debug.LogWarning($"상인조합 화면에 필요한 {nameof(IGuildQuestBoard)}가 연결되어 있지 않아 등록하지 못했다(Tools > Game > Build Bootstrap Scene).");
                return;
            }

            if (!MerchantGuildElements.TryBind(sceneUIRoot, out var elements)) return;

            merchantGuildPanel = new MerchantGuildPanel(elements, board, uiManager);
            panelRegistrar.RegisterScreenPanel(merchantGuildPanel);
        }

        // 목록형 구매 화면(용병·마구간 공용)의 행동 버튼 사유 툴팁 - 요소 Id는 화면 접두사로 구분한다.
        private ITooltipHandle RegisterRosterShopTooltip(SceneUIRoot sceneUIRoot, RosterShopElements elements, string prefix)
            => ButtonTooltip.TryRegister(GetTooltipController(), elements.ActionButton, sceneUIRoot,
                RosterShopUIElementIds.Of(prefix, RosterShopUIElementIds.ActionTooltip), RosterShopUIElementIds.Of(prefix, RosterShopUIElementIds.ActionTooltipText));

        private ITooltipController GetTooltipController()
        {
            var tooltips = GetComponent<ITooltipController>();
            if (tooltips == null)
            {
                throw new InvalidOperationException($"{nameof(HubUIWiring)}와 같은 GameObject에 {nameof(ITooltipController)} 구현체가 없다.");
            }
            return tooltips;
        }

        // 모달 팝업 채널에 등록한다 - 시설 화면 위에 열려도 시설 화면은 닫히지 않는다(Docs/Refactor/2026-10-26_공통.md B-1). 의존성이나 요소가 없으면 등록하지 않는다 - HUD 클릭은 "등록되지 않은 패널" 경고만 낸다.
        private void RegisterGoldConversion(SceneUIRoot sceneUIRoot, IUIManager uiManager, IPanelRegistrar panelRegistrar, IGoldHoldingsReader holdings, IGoldBoxConverter converter)
        {
            goldConversionPanel?.Dispose();
            goldConversionPanel = null;

            if (holdings == null || converter == null)
            {
                Debug.LogWarning($"골드 변환 모달에 필요한 {nameof(IGoldHoldingsReader)}/{nameof(IGoldBoxConverter)}가 연결되어 있지 않아 등록하지 못했다(Tools > Game > Build Bootstrap Scene).");
                return;
            }

            if (!GoldConversionElements.TryBind(sceneUIRoot, out var elements)) return;

            goldConversionPanel = new GoldConversionPanel(elements, holdings, converter, uiManager);
            panelRegistrar.RegisterPopupPanel(goldConversionPanel);
        }

        private void OnDestroy()
        {
            tradeGoodsMarketPanel?.Dispose();
            mercenaryContactPanel?.Dispose();
            stablePanel?.Dispose();
            goldConversionPanel?.Dispose();
            innPanel?.Dispose();
            innRestProgressPanel?.Dispose();
            merchantGuildPanel?.Dispose();
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
