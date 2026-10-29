using Game.Core;
using Game.Core.DebugTools;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.Editor
{
    /// <summary>
    /// Hub 씬 UI 전체(Hub↔Field 씬 전환 연출용 ContentRoot, 그 안의 배치 UI/상행 준비 UI)를 한 번에
    /// 생성/동기화한다. 원래 FormationUIInstaller/TripUIInstaller/HubUIInstaller 세 메뉴로 나뉘어
    /// 있었는데, 실행 순서(ContentRoot 생성 → 그 안에 배치/상행 준비 UI 생성)에 의존하는 데다 Hub UI를
    /// 바꿀 때마다 항상 셋 다 같이 실행해야 해서 실용적 의미가 없어 하나로 합쳤다. ContentRoot를 먼저
    /// 만들고 배치/상행 준비 UI를 처음부터 그 안에 직접 생성한다 - 다 만든 뒤 사후에 재배치하면
    /// get-or-create 조회(Transform.Find, 직계 자식만 검색)가 재실행 시 옮겨지기 전 위치를 찾다가
    /// 실패해 매번 중복 생성하는 버그로 이어진다(과거에 실제로 겪음). 씬 YAML 수작업 편집 대신 이
    /// 도구로 재현 가능하게 만든다.
    /// </summary>
    public static class HubSceneInstaller
    {

        [MenuItem("Tools/Game/Build Hub Scene")]
        public static void BuildHubScene()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.name != SceneNames.Hub)
            {
                Debug.LogError($"'{SceneNames.Hub}' 씬이 활성 씬이어야 한다. 현재 활성 씬: '{activeScene.name}'. Hub.unity를 열고 다시 실행하라.");
                return;
            }

            var sceneUIRoot = Object.FindFirstObjectByType<SceneUIRoot>(FindObjectsInactive.Include);
            if (sceneUIRoot == null)
            {
                Debug.LogError($"씬에서 {nameof(SceneUIRoot)}를 찾을 수 없다.");
                return;
            }

            // 콘텐츠 씬마다 자체 EventSystem이 있어야 클릭이 동작한다(FieldUIInstaller와 동일한 안전장치 -
            // EditorUIBuilder.EnsureSceneEventSystem 주석 참고).
            EditorUIBuilder.EnsureSceneEventSystem(activeScene);

            // ContentRoot를 먼저 만들고 배치/상행 UI를 그 "안"에 직접 생성한다(사후 재배치가 아니다) -
            // GetOrCreateUIObject의 get-or-create 조회는 직계 자식만 본다(Transform.Find). 예전에는
            // sceneUIRoot 바로 아래에 만든 뒤 나중에 ContentRoot로 옮겼는데, 그러면 재실행 시 옮겨지기
            // 전 위치(sceneUIRoot 직계)에서는 더 이상 못 찾아 매번 새로 하나씩 더 만들어졌다 - 마커 ID
            // 중복 등록 경고(SceneUIRoot.Awake)로 이어졌던 버그. 처음부터 최종 위치에 만들면 이 문제가
            // 구조적으로 발생하지 않는다.
            var contentRoot = EnsureContentRoot(sceneUIRoot);

            // 위 구조 변경 이전 버전이 sceneUIRoot 바로 아래에 남겨뒀을 수 있는 중복 오브젝트를 정리한다
            // (재실행 안전성 - 한 번만 정리되면 이후에는 항상 no-op). 문제가 있던 버전을 여러 번
            // 재실행했다면 같은 이름의 직계 자식이 여러 개 쌓여있을 수 있어(Transform.Find는 첫 번째
            // 매치만 반환) 전부 찾아서 지운다.
            EditorUIBuilder.DestroyAllDirectChildrenNamed(sceneUIRoot.transform, "FormationPanel");
            EditorUIBuilder.DestroyAllDirectChildrenNamed(sceneUIRoot.transform, "TripPanel");

            // 레이어를 먼저 만들고 기존 요소를 옮긴 "뒤"에 빌드한다 - 빌드가 먼저면 옛 위치를 못 찾고 새 레이어
            // 안에 중복 생성한다(위 ContentRoot 주석과 같은 이유).
            var layers = EnsureLayers(sceneUIRoot, contentRoot);

            BuildFormationUI(layers.ModalPopups);
            TripUIBuilder.Build(layers.ModalPopups);
            BuildTacticsUI(layers.RootDepth, layers.ModalPopups);
            StackActionButtonsAboveDeparture(sceneUIRoot);
            BuildTownCategoryColumn(layers.RootDepth);
            BuildTownCategoryDepth(layers.DepthLayer);
            HudUIBuilder.BuildCurrencyHud(layers.PopupExemptLayer);
            HudUIBuilder.BuildDateHud(layers.PopupExemptLayer);
            InventoryShortcutUIBuilder.Build(sceneUIRoot, layers.PersistentLayer);
            // 비모달 인벤토리 팝업(Docs/설계/40번 §7, 42번 §4.4, 44번 §3). 형제 순서 = 렌더 순서라 계단식 배치에서
            // 뒤에 만든 팝업이 위에 그려진다(열 때는 패널이 맨 앞으로 올린다).
            InventoryPopupUIBuilder.Build(layers.ModelessPopups, InventoryPopupSpecs.TradeGoods);
            InventoryPopupUIBuilder.Build(layers.ModelessPopups, InventoryPopupSpecs.Equipment);
            InventoryPopupUIBuilder.Build(layers.ModelessPopups, InventoryPopupSpecs.Consumable);
            InventoryPopupUIBuilder.Build(layers.ModelessPopups, InventoryPopupSpecs.PersonalItem);
            // 시설 화면은 Screens 묶음(Docs/Refactor/2026-10-26_공통.md B-1) - 재화 패널 외 UI 숨김은 레이어 숨김 규칙이, 카테고리 depth 복귀는 시설 화면 채널이 처리한다.
            TradeGoodsMarketUIBuilder.Build(layers.Screens);
            RosterShopUIBuilder.Build(layers.Screens, RosterShopUIBuilder.MercenaryContact);
            var stableRoot = RosterShopUIBuilder.Build(layers.Screens, RosterShopUIBuilder.Stable);
            StableOwnedUIBuilder.Build(stableRoot); // 마구간 보유 영역(설계 91번 §5.2)
            // 매각은 StableOwnedUIBuilder가 좌측 개체 정보에 만든다(2026-10-26) - 우측 정보 영역에 있던 옛 매각 버튼 정리.
            var candidateInfo = stableRoot.Find("CandidatePanel/CandidateInfo");
            if (candidateInfo != null) EditorUIBuilder.DestroyChildIfExists(candidateInfo, "SellButton");
            // 골드 변환 모달(설계 83번 §6.2) - HUD 짧은 클릭으로 연다.
            GoldConversionUIBuilder.Build(layers.ModalPopups);
            // 숙소·휴식 진행·상인조합(설계 103번 §5.2) - 시설 화면.
            InnUIBuilder.Build(layers.Screens);
            MerchantGuildUIBuilder.Build(layers.Screens);
            // 휴식 중 이벤트 화면(설계 109 §6.1) - Field와 같은 회수 적재·이벤트 대화·선택 조립을 시설 화면(휴식 진행) 위 모달 묶음에 둔다.
            // 형제 순서 = 렌더 순서라 모달 묶음 맨 뒤에 회수 적재 → 대화 → 선택 순으로 둔다(재실행해도 같은 순서).
            var cargoRecovery = CargoRecoveryUIBuilder.Build(layers.ModalPopups, null);
            cargoRecovery.SetAsLastSibling();
            FieldEventUIBuilder.Build(layers.ModalPopups, cargoRecovery);

            EditorUIBuilder.WarnIfOutsideTransitionRoot(sceneUIRoot.transform, contentRoot, "Hub");

            EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log("Hub Scene UI 생성/동기화 완료. 씬을 저장(Ctrl+S)해야 변경사항이 파일에 반영된다. "
                + "HubFormationPanel.dragGhostPrefab과 FieldFormationPanel.dragGhostPrefab 둘 다에 "
                + "'Assets/Prefabs/UI/Formation/FormationUnitIcon.prefab'을, "
                + "수동으로 연결하라(HubFormationPanel/FieldFormationPanel은 Bootstrap 씬에 있어 이 도구가 직접 연결할 수 없다). "
                + "TripPanel의 지도 마커·선 프리팹은 Build Bootstrap Scene이 연결한다.");
        }

        // ==================== 레이어 (Docs/설계/37번 §3, 38번 §5) ====================
        // ContentRoot(씬 전환 루트) 아래를 두 관리 축으로 나눈다. 아래→위 렌더 순서:
        //   Background
        //   DepthLayer        depth 의존 · 모달 팝업 시 숨김(CanvasGroup)
        //   PersistentLayer   depth 무관 · 모달 팝업 시 숨김(CanvasGroup) - 인벤토리 버튼, 새 상시 UI의 기본 자리
        //   PopupLayer        Screens(시설 화면) → ModalPopups(상행 준비/배치/방향성 지시/골드 변환) → ModelessPopups(인벤토리 팝업)
        //   PopupExemptLayer  depth 무관 · 모달 팝업 시 유지(재화 HUD)
        // 레이어 순서가 곧 렌더 순서라 여기서 레이어 형제 순서만 고정하면 요소별 형제 순서 강제가 필요 없다.
        private readonly struct HubLayers
        {
            public readonly Transform DepthLayer;
            public readonly Transform RootDepth;
            public readonly Transform PersistentLayer;
            public readonly Transform Screens;
            public readonly Transform ModalPopups;
            public readonly Transform ModelessPopups;
            public readonly Transform PopupExemptLayer;

            public HubLayers(Transform depthLayer, Transform rootDepth, Transform persistentLayer, Transform screens, Transform modalPopups, Transform modelessPopups, Transform popupExemptLayer)
            {
                DepthLayer = depthLayer;
                RootDepth = rootDepth;
                PersistentLayer = persistentLayer;
                Screens = screens;
                ModalPopups = modalPopups;
                ModelessPopups = modelessPopups;
                PopupExemptLayer = popupExemptLayer;
            }
        }

        private static HubLayers EnsureLayers(SceneUIRoot sceneUIRoot, Transform contentRoot)
        {
            // CanvasGroup은 LayerHideGate가 모달 팝업·시설 화면 동안 숨기는 대상이다(팝업 축).
            var depthLayer = EditorUIBuilder.EnsureStretchLayer(contentRoot, "DepthLayer", HubUIElementIds.DepthLayer);
            EditorUIBuilder.GetOrAddComponent<CanvasGroup>(depthLayer.gameObject);
            var persistentLayer = EditorUIBuilder.EnsureStretchLayer(contentRoot, "PersistentLayer", HubUIElementIds.PersistentLayer);
            EditorUIBuilder.GetOrAddComponent<CanvasGroup>(persistentLayer.gameObject);
            var popupLayer = EditorUIBuilder.EnsureStretchLayer(contentRoot, "PopupLayer", HubUIElementIds.PopupLayer);
            var popupExemptLayer = EditorUIBuilder.EnsureStretchLayer(contentRoot, "PopupExemptLayer", HubUIElementIds.PopupExemptLayer);
            // 팝업 축 대상은 아니지만 휴식 진행 중 HUD 입력을 막는 데 쓴다(설계 103번 §3) - 평소엔 기본값(상호작용·레이캐스트 켜짐).
            EditorUIBuilder.GetOrAddComponent<CanvasGroup>(popupExemptLayer.gameObject);

            // Background는 EnsureContentRoot가 첫 자식으로 둔다 - 나머지 레이어는 그 뒤에 아래 순서로.
            depthLayer.SetAsLastSibling();
            persistentLayer.SetAsLastSibling();
            popupLayer.SetAsLastSibling();
            popupExemptLayer.SetAsLastSibling();

            var screens = EditorUIBuilder.EnsureStretchLayer(popupLayer, "Screens", HubUIElementIds.Screens);
            var modalPopups = EditorUIBuilder.EnsureStretchLayer(popupLayer, "ModalPopups", HubUIElementIds.ModalPopups);
            var modelessPopups = EditorUIBuilder.EnsureStretchLayer(popupLayer, "ModelessPopups", HubUIElementIds.ModelessPopups);
            // 시설 화면 < 모달 < 인벤토리 팝업 순으로 그린다 - 골드 변환 모달이 시설 화면 위에, 인벤토리 팝업이 모달 위에 뜬다(38번 §3).
            screens.SetAsFirstSibling();
            modalPopups.SetSiblingIndex(screens.GetSiblingIndex() + 1);
            modelessPopups.SetAsLastSibling();
            // 예전엔 시설 화면 루트가 ModalPopups 아래에 있었다 - 조립 전에 Screens로 옮겨야 조립이 옛 오브젝트를 재사용한다(중복 생성 방지).
            foreach (var facilityRoot in EditorFacilityScreenBuilder.AllRoots)
            {
                var old = modalPopups.Find(facilityRoot);
                if (old != null && screens.Find(facilityRoot) == null) old.SetParent(screens, false);
            }

            // RootDepth는 카테고리 depth보다 아래에 그려져야 한다 - DepthLayer의 첫 자식으로 고정.
            // RootDepth 자신의 CanvasGroup은 씬 전환 커튼 중 루트 depth 버튼 전체를 비활성화하는 용도다
            // (HubUIController) - DepthLayer의 CanvasGroup과 중첩되면 상호작용 차단이 AND로 합쳐져 서로 간섭하지 않는다.
            var rootDepth = EditorUIBuilder.EnsureStretchLayer(depthLayer, "RootDepth", HubUIElementIds.RootDepth);
            EditorUIBuilder.GetOrAddComponent<CanvasGroup>(rootDepth.gameObject);
            rootDepth.SetAsFirstSibling();

            // 기존 요소를 새 부모로 옮긴다(마커 기준이라 이전 버전 어디에 있든 찾는다) - 상행 준비/상단 배치
            // 버튼은 코드가 아니라 씬에 원래 있던 요소라 get-or-create 대상이 아니고 이동만 한다.
            EditorUIBuilder.ReparentIfFound(sceneUIRoot, HubUIElementIds.DepartureButton, rootDepth);
            EditorUIBuilder.ReparentIfFound(sceneUIRoot, HubUIElementIds.FormationButton, rootDepth);
            EditorUIBuilder.ReparentIfFound(sceneUIRoot, HubUIElementIds.TacticsButton, rootDepth);
            EditorUIBuilder.ReparentIfFound(sceneUIRoot, FormationUIElementIds.PanelRoot, modalPopups);
            EditorUIBuilder.ReparentIfFound(sceneUIRoot, TripUIElementIds.PanelRoot, modalPopups);
            EditorUIBuilder.ReparentIfFound(sceneUIRoot, TacticsUIElementIds.PanelRoot, modalPopups);
            EditorUIBuilder.ReparentIfFound(sceneUIRoot, HubUIElementIds.CurrencyPanelRoot, popupExemptLayer);
            EditorUIBuilder.ReparentIfFound(sceneUIRoot, HubUIElementIds.InventoryShortcutRoot, persistentLayer);

            return new HubLayers(depthLayer, rootDepth, persistentLayer, screens, modalPopups, modelessPopups, popupExemptLayer);
        }

        // ==================== 방향성 지시(Tactics) ====================
        // 버튼 위치는 여기서 정하지 않는다 - StackActionButtonsAboveDeparture가 상행 준비 버튼 기준으로
        // 계산한다. 이 버튼은 예전부터 씬에 있던 게 아니라 새로 생긴 것이라, DepartureButton/
        // FormationButton과 달리 처음부터 최종 부모(RootDepth) 안에 직접 만든다(사후 재배치로 인한 중복
        // 생성 버그를 피하기 위함 - BuildHubScene 요약 주석 참고). 모달 팝업보다 아래에 그려지는 것은
        // DepthLayer가 PopupLayer보다 앞 형제라 구조적으로 보장된다(예전의 형제 순서 강제 로직 제거).
        private static void BuildTacticsUI(Transform rootDepth, Transform modalPopups)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(rootDepth, "TacticsButton");
            EditorUIBuilder.EnsureImage(go, new Color(0.85f, 0.75f, 0.95f, 1f));
            EditorUIBuilder.EnsureButton(go);
            EditorUIBuilder.EnsureLabel(go.transform, "방향성 지시", autoSize: true, minFontSize: 18f, maxFontSize: 30f);
            EditorUIBuilder.EnsureMarker(go, HubUIElementIds.TacticsButton);

            TacticsUIBuilder.Build(modalPopups);
        }

        // ==================== 마을 시설 카테고리 (Docs/설계/37번 §4~5) ====================
        // 버튼 위치는 런타임 TownButtonColumnView.Arrange가 제공 여부에 따라 매번 계산한다 - 여기서는
        // 버튼을 만들어 두기만 한다. 순서/라벨은 TownFacilityCatalog(기획 24번 확정값) 단일 출처.
        private static readonly Color TownCategoryButtonColor = new(0.95f, 0.88f, 0.7f, 1f);
        private static readonly Color TownFacilityButtonColor = new(0.8f, 0.9f, 0.75f, 1f);
        private static readonly Color TownBackButtonColor = new(0.85f, 0.85f, 0.85f, 1f);

        private static void BuildTownCategoryColumn(Transform rootDepth)
        {
            var column = EditorUIBuilder.EnsureStretchLayer(rootDepth, "TownCategoryColumn", HubUIElementIds.TownCategoryColumn);
            EditorUIBuilder.GetOrAddComponent<TownButtonColumnView>(column.gameObject);

            foreach (var categoryId in TownFacilityCatalog.CategoryIds)
            {
                BuildColumnButton(column, $"Category_{categoryId}", TownFacilityCatalog.GetCategoryLabel(categoryId),
                    HubUIElementIds.TownCategoryButton(categoryId), TownCategoryButtonColor);
            }
        }

        // 카테고리 4개가 공유하는 카테고리 depth - 시설 버튼 전체 + 뒤로 가기. 어느 카테고리가 열리느냐에 따라
        // TownCategoryPanel이 표시할 시설만 골라 배치한다. 배경은 두지 않는다(오버레이가 아니라 depth 전환).
        private static void BuildTownCategoryDepth(Transform depthLayer)
        {
            var depthRoot = EditorUIBuilder.EnsureStretchLayer(depthLayer, "TownCategoryDepth", TownUIElementIds.CategoryDepthRoot);
            EditorUIBuilder.GetOrAddComponent<TownButtonColumnView>(depthRoot.gameObject);

            foreach (var facilityId in TownFacilityCatalog.AllFacilityIds)
            {
                BuildColumnButton(depthRoot, $"Facility_{facilityId}", TownFacilityCatalog.GetFacilityLabel(facilityId),
                    TownUIElementIds.FacilityButton(facilityId), TownFacilityButtonColor);
            }

            BuildColumnButton(depthRoot, "BackButton", "뒤로 가기", TownUIElementIds.BackButton, TownBackButtonColor);

            depthRoot.gameObject.SetActive(false);
        }

        private static void BuildColumnButton(Transform column, string objectName, string label, string markerId, Color color)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(column, objectName);
            EditorUIBuilder.EnsureImage(go, color);
            EditorUIBuilder.EnsureButton(go);
            EditorUIBuilder.EnsureLabel(go.transform, label, autoSize: true, minFontSize: 18f, maxFontSize: 30f);
            EditorUIBuilder.EnsureMarker(go, markerId);
        }

        // ==================== 배치(Formation) UI ====================
        // 실제 조립 로직은 FormationUIBuilder(Hub/Field 공용)에 있다 - Field 씬에서도 "정비창 재호출"이
        // 동작하려면 같은 화면이 필요해서 공용화되어 있다(FieldUIInstaller 참고).
        private static void BuildFormationUI(Transform modalPopups)
        {
            FormationUIBuilder.EnsurePrefabFolder();
            var slotPrefab = FormationUIBuilder.GetOrCreateSlotPrefab();
            var iconPrefab = FormationUIBuilder.GetOrCreateIconPrefab();
            var rowPrefab = FormationUIBuilder.GetOrCreateRowPrefab();
            var pathLinePrefab = FormationUIBuilder.GetOrCreatePathLinePrefab();
            var travelerIconPrefab = FormationUIBuilder.GetOrCreateTravelerIconPrefab();
            var activityOverlayPrefab = FormationUIBuilder.GetOrCreateActivityOverlayPrefab();
            FormationUIBuilder.Build(modalPopups, slotPrefab, iconPrefab, rowPrefab, pathLinePrefab, travelerIconPrefab, activityOverlayPrefab, includeApplyButton: true);
        }

        // ==================== Hub↔Field 전환 연출용 ContentRoot ====================
        // 슬라이드 대상은 "Hub 배경"이 아니라 "그 순간 화면에 보이는 Hub 콘텐츠 전체"여야 한다
        // (Docs/설계/10-2026-08-26-씬전환_연출_아키텍처.md §9). 배치/상행 준비 UI는 이 메서드가 반환하는 Transform
        // 안에 처음부터 직접 생성되므로(BuildFormationUI/BuildTripUI 참고) 별도 재배치가 필요 없다.
        // Background/버튼은 이 도구가 만드는 게 아니라 Hub 씬에 이미 존재하는 요소라 여전히 찾아서
        // 옮겨야 한다.
        private static Transform EnsureContentRoot(SceneUIRoot sceneUIRoot)
        {
            var contentRootGo = EditorUIBuilder.GetOrCreateUIObject(sceneUIRoot.transform, "ContentRoot");
            EditorUIBuilder.SetStretch(contentRootGo.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureMarker(contentRootGo, HubUIElementIds.ContentRoot);

            // ContentRoot는 Hub의 배경 레이어다 - 다른 콘텐츠 씬 오브젝트보다 항상 아래(렌더링 순서상
            // 뒤)에 있어야 한다. get-or-create로 새로 만들면 sceneUIRoot의 마지막 자식으로 추가되는데,
            // 그러면 다른 오브젝트보다 위에 그려질 수 있다 - 매번 첫 번째 자식으로 고정한다.
            contentRootGo.transform.SetAsFirstSibling();

            // 상행 준비/상단 배치 버튼은 EnsureLayers가 RootDepth로 옮긴다(Docs/설계/37번 §3.4). Background는
            // 두 레이어보다 항상 아래(첫 자식)에 그려져야 한다.
            EditorUIBuilder.ReparentIfFound(sceneUIRoot, HubUIElementIds.Background, contentRootGo.transform);
            var background = EditorUIBuilder.FindMarker(sceneUIRoot, HubUIElementIds.Background);
            if (background != null)
            {
                background.transform.SetAsFirstSibling();
            }
            // Field의 같은 역할 버튼(정비창)과 표기를 통일한다(사용자 확정, 2026-09-07).
            EditorUIBuilder.SetLabelText(sceneUIRoot, HubUIElementIds.FormationButton, "상단 배치", minFontSize: 18f, maxFontSize: 30f);

            return contentRootGo.transform;
        }

        private static void StackActionButtonsAboveDeparture(SceneUIRoot sceneUIRoot)
        {
            var departure = EditorUIBuilder.FindMarker(sceneUIRoot, HubUIElementIds.DepartureButton);
            if (departure == null)
            {
                Debug.LogWarning($"Hub UI에서 '{HubUIElementIds.DepartureButton}' 요소를 찾을 수 없어 버튼 정렬을 건너뛴다.");
                return;
            }

            var departureRect = (RectTransform)departure.transform;
            var width = (departureRect.anchorMax.x - departureRect.anchorMin.x - EditorHubLayout.HubActionButtonGap) / 2f;
            var height = EditorHubLayout.HubActionButtonBaseHeight * (width / EditorHubLayout.HubActionButtonBaseWidth);
            var bottomY = departureRect.anchorMax.y + EditorHubLayout.HubActionButtonGap;
            var leftX = departureRect.anchorMin.x;

            foreach (var id in new[] { HubUIElementIds.FormationButton, HubUIElementIds.TacticsButton })
            {
                var marker = EditorUIBuilder.FindMarker(sceneUIRoot, id);
                if (marker == null)
                {
                    Debug.LogWarning($"Hub UI에서 '{id}' 요소를 찾을 수 없어 정렬을 건너뛴다.");
                }
                else
                {
                    EditorUIBuilder.SetAnchors((RectTransform)marker.transform,
                        new Vector2(leftX, bottomY),
                        new Vector2(leftX + width, bottomY + height));
                }

                // 한쪽이 없어도 다른 쪽 자리는 고정 - 오른쪽 버튼이 왼쪽으로 당겨지지 않게 한다.
                leftX += width + EditorHubLayout.HubActionButtonGap;
            }
        }

    }
}
