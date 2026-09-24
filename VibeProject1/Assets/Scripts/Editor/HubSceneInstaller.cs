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
        private const string TripPrefabFolder = "Assets/Prefabs/UI/Trip";
        private const string CityMarkerPrefabPath = TripPrefabFolder + "/TripDebugCityMarker.prefab";
        private const string RoadLinePrefabPath = TripPrefabFolder + "/TripDebugRoadLine.prefab";

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
            DestroyAllDirectChildrenNamed(sceneUIRoot.transform, "FormationPanel");
            DestroyAllDirectChildrenNamed(sceneUIRoot.transform, "TripPanel");

            // 레이어를 먼저 만들고 기존 요소를 옮긴 "뒤"에 빌드한다 - 빌드가 먼저면 옛 위치(ContentRoot 직계)를
            // 못 찾고 새 레이어 안에 중복 생성한다(위 ContentRoot 주석과 같은 이유).
            var (depthLayer, rootDepth, persistentLayer) = EnsureLayers(sceneUIRoot, contentRoot);

            BuildFormationUI(depthLayer);
            BuildTripUI(depthLayer);
            BuildTacticsUI(rootDepth, depthLayer);
            StackActionButtonsAboveDeparture(sceneUIRoot);
            BuildTownCategoryColumn(rootDepth);
            BuildTownCategoryDepth(depthLayer);
            BuildPlayerCurrencyHud(persistentLayer);

            EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log("Hub Scene UI 생성/동기화 완료. 씬을 저장(Ctrl+S)해야 변경사항이 파일에 반영된다. "
                + "HubFormationPanel.dragGhostPrefab과 FieldFormationPanel.dragGhostPrefab 둘 다에 "
                + "'Assets/Prefabs/UI/Formation/FormationUnitIcon.prefab'을, "
                + $"TripPanel.debugCityMarkerPrefab에는 '{CityMarkerPrefabPath}', debugRoadLinePrefab에는 '{RoadLinePrefabPath}'를 "
                + "수동으로 연결하라(HubFormationPanel/FieldFormationPanel/TripPanel은 Bootstrap 씬에 있어 이 도구가 직접 연결할 수 없다).");
        }

        // ==================== 레이어 (Docs/설계/37번 §3) ====================
        // ContentRoot = Background + DepthLayer(화면 depth에 따라 바뀌는 UI) + PersistentLayer(항상 활성인 UI).
        // 레이어 순서가 곧 렌더 순서라, 여기서 레이어 2개와 RootDepth의 형제 순서만 고정하면 요소별 형제
        // 순서 강제(예전 TacticsButton 이동 로직, 재화 HUD SetAsLastSibling)가 필요 없다.
        private static (Transform depthLayer, Transform rootDepth, Transform persistentLayer) EnsureLayers(SceneUIRoot sceneUIRoot, Transform contentRoot)
        {
            var depthLayer = EnsureStretchLayer(contentRoot, "DepthLayer", HubUIElementIds.DepthLayer);
            var persistentLayer = EnsureStretchLayer(contentRoot, "PersistentLayer", HubUIElementIds.PersistentLayer);
            // Background는 EnsureContentRoot가 첫 자식으로 둔다 - 두 레이어는 그 뒤, PersistentLayer가 맨 뒤(최상단).
            depthLayer.SetAsLastSibling();
            persistentLayer.SetAsLastSibling();

            // RootDepth는 모달 패널/카테고리 depth보다 아래에 그려져야 한다 - DepthLayer의 첫 자식으로 고정.
            // CanvasGroup은 씬 전환 커튼 중 루트 depth 버튼 전체를 한 번에 비활성화하는 데 쓴다(HubUIController).
            var rootDepth = EnsureStretchLayer(depthLayer, "RootDepth", HubUIElementIds.RootDepth);
            EditorUIBuilder.GetOrAddComponent<CanvasGroup>(rootDepth.gameObject);
            rootDepth.SetAsFirstSibling();

            // 기존 요소를 새 부모로 옮긴다(마커 기준이라 어디에 있든 찾는다) - 상행 준비/상단 배치 버튼은
            // 코드가 아니라 씬에 원래 있던 요소라 get-or-create 대상이 아니고 이동만 한다.
            ReparentIfFound(sceneUIRoot, HubUIElementIds.DepartureButton, rootDepth);
            ReparentIfFound(sceneUIRoot, HubUIElementIds.FormationButton, rootDepth);
            ReparentIfFound(sceneUIRoot, HubUIElementIds.TacticsButton, rootDepth);
            ReparentIfFound(sceneUIRoot, FormationUIElementIds.PanelRoot, depthLayer);
            ReparentIfFound(sceneUIRoot, TripUIElementIds.PanelRoot, depthLayer);
            ReparentIfFound(sceneUIRoot, TacticsUIElementIds.PanelRoot, depthLayer);
            ReparentIfFound(sceneUIRoot, HubUIElementIds.CurrencyPanelRoot, persistentLayer);

            return (depthLayer, rootDepth, persistentLayer);
        }

        private static Transform EnsureStretchLayer(Transform parent, string name, string markerId)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, name);
            EditorUIBuilder.SetStretch(go.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureMarker(go, markerId);
            return go.transform;
        }

        // ==================== 방향성 지시(Tactics) ====================
        // 버튼 위치는 여기서 정하지 않는다 - StackActionButtonsAboveDeparture가 상행 준비 버튼 기준으로
        // 계산한다. 이 버튼은 예전부터 씬에 있던 게 아니라 새로 생긴 것이라, DepartureButton/
        // FormationButton과 달리 처음부터 최종 부모(RootDepth) 안에 직접 만든다(사후 재배치로 인한 중복
        // 생성 버그를 피하기 위함 - BuildHubScene 요약 주석 참고). 모달 패널보다 아래에 그려지는 것은
        // RootDepth가 DepthLayer의 첫 자식이라 구조적으로 보장된다(예전의 형제 순서 강제 로직 제거).
        private static void BuildTacticsUI(Transform rootDepth, Transform depthLayer)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(rootDepth, "TacticsButton");
            EditorUIBuilder.EnsureImage(go, new Color(0.85f, 0.75f, 0.95f, 1f));
            EditorUIBuilder.EnsureButton(go);
            EditorUIBuilder.EnsureLabel(go.transform, "방향성 지시", autoSize: true, minFontSize: 18f, maxFontSize: 30f);
            EditorUIBuilder.EnsureMarker(go, HubUIElementIds.TacticsButton);

            TacticsUIBuilder.Build(depthLayer);
        }

        // ==================== 마을 시설 카테고리 (Docs/설계/37번 §4~5) ====================
        // 버튼 위치는 런타임 TownButtonColumnView.Arrange가 제공 여부에 따라 매번 계산한다 - 여기서는
        // 버튼을 만들어 두기만 한다. 순서/라벨은 TownFacilityCatalog(기획 24번 확정값) 단일 출처.
        private static readonly Color TownCategoryButtonColor = new(0.95f, 0.88f, 0.7f, 1f);
        private static readonly Color TownFacilityButtonColor = new(0.8f, 0.9f, 0.75f, 1f);
        private static readonly Color TownBackButtonColor = new(0.85f, 0.85f, 0.85f, 1f);

        private static void BuildTownCategoryColumn(Transform rootDepth)
        {
            var column = EnsureStretchLayer(rootDepth, "TownCategoryColumn", HubUIElementIds.TownCategoryColumn);
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
            var depthRoot = EnsureStretchLayer(depthLayer, "TownCategoryDepth", TownUIElementIds.CategoryDepthRoot);
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

        // ==================== 재화 HUD ====================
        // 스타크래프트류 RTS 자원 바 형태(기획 27번 §3.1) - 항상 노출, 다른 패널이 열려도 가려지지
        // 않는다(27번 §3.5). 예전엔 SetAsLastSibling()으로 최상단을 강제했지만, 이제 PersistentLayer(항상
        // ContentRoot의 마지막 자식) 안에 두는 것으로 구조적으로 보장된다(Docs/설계/37번 §3.2).
        private static void BuildPlayerCurrencyHud(Transform persistentLayer)
        {
            // 가로폭은 화면의 30%로 고정 요구사항(사용자 확정, 2026-09-23)이 생겨 X축만 스트레치
            // 앵커(anchorMin.x=0.7~anchorMax.x=1)로 바꿨다 - width가 화면 비율로 결정되므로
            // 2026-09-21에 겪은 "고정 픽셀 박스가 콘텐츠보다 작아서 아이콘이 밖으로 튀어나오는"
            // 문제는 재발하지 않는다(30%가 아이콘+텍스트+패딩보다 항상 크다). 세로축은 기존과
            // 동일하게 한 점 고정 + ContentSizeFitter로 콘텐츠 높이에 맞춘다.
            var root = EditorUIBuilder.GetOrCreateUIObject(persistentLayer, "PlayerCurrencyHud");
            var rootRect = root.GetComponent<RectTransform>();
            // 우상단 기준(사용자 확정, 2026-09-21). pivot.x=1/anchorMax.x=1이 겹쳐 anchoredPosition.x는
            // 화면 우측 모서리로부터의 오프셋으로 동작한다(스트레치 축이어도 점 앵커와 동일하게 계산됨).
            rootRect.anchorMin = new Vector2(0.7f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(1f, 1f);
            // 화면 우상단 모서리로부터의 마진 2배 확대(사용자 확정, 2026-09-23: -16 -> -32).
            rootRect.anchoredPosition = new Vector2(-32f, -32f);
            rootRect.sizeDelta = new Vector2(0f, rootRect.sizeDelta.y);
            EditorUIBuilder.EnsureMarker(root, HubUIElementIds.CurrencyPanelRoot);
            // 패널 배경 - 이게 없으면 아이콘/텍스트만 화면에 떠 있는 것처럼 보여 "패널 형태"로 보이지
            // 않는다(2026-09-21 실전 확인).
            EditorUIBuilder.EnsureImage(root, new Color(0f, 0f, 0f, 0.6f));

            var layout = EditorUIBuilder.GetOrAddComponent<HorizontalLayoutGroup>(root);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 8f;
            layout.padding = new RectOffset(8, 8, 4, 4);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var sizeFitter = EditorUIBuilder.GetOrAddComponent<ContentSizeFitter>(root);
            // 가로는 위에서 anchorMin/Max.x(0.7~1)로 화면의 30%를 직접 지정했으므로 ContentSizeFitter가
            // 다시 콘텐츠 크기로 덮어쓰면 안 된다(Unconstrained) - 세로만 콘텐츠 높이에 맞춘다.
            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var iconGo = EditorUIBuilder.GetOrCreateUIObject(root.transform, "Icon");
            EditorUIBuilder.EnsureImage(iconGo, Color.white); // currencyIcon 미할당 시 자리표시자
            EditorUIBuilder.EnsureMarker(iconGo, HubUIElementIds.CurrencyIcon);
            var iconLayoutElement = EditorUIBuilder.GetOrAddComponent<LayoutElement>(iconGo);
            iconLayoutElement.preferredWidth = 32f;
            iconLayoutElement.preferredHeight = 32f;

            var amountLabel = EditorUIBuilder.EnsureLabel(root.transform, "0");
            EditorUIBuilder.EnsureMarker(amountLabel.gameObject, HubUIElementIds.CurrencyAmountText);
            EditorUIBuilder.GetOrAddComponent<PointerHoverRelay>(amountLabel.gameObject);
            // EnsureLabel은 기본적으로 raycastTarget=false로 만든다(장식용 텍스트 기준) - 이 텍스트는
            // 호버 대상이라 포인터 이벤트를 받아야 하므로 켜야 한다.
            amountLabel.raycastTarget = true;
            // EnsureLabel 기본 색상은 검정이다(밝은 배경 버튼 기준) - 이 패널은 어두운 배경이라
            // 흰색으로 바꿔야 보인다(2026-09-21 실전 확인).
            amountLabel.color = Color.white;
            var amountLayoutElement = EditorUIBuilder.GetOrAddComponent<LayoutElement>(amountLabel.gameObject);
            amountLayoutElement.minWidth = 60f;

            // 프레임(Image)과 텍스트(TextMeshProUGUI)를 부모/자식으로 분리한다 - 기존 버튼류와 같은
            // 구조(EnsureImage로 배경, EnsureLabel로 자식 텍스트). 같은 오브젝트에 Image+TMP를 함께
            // 붙이면 AddComponent가 실패해 NullReferenceException으로 이어진 적이 있어(2026-09-21
            // 실전 확인) 이 구조로 되돌렸다. 컨트롤러는 프레임(tooltipGo)을 SetActive로 토글하고,
            // 텍스트(tooltipLabel)는 내용만 갱신한다.
            // 실제 원인 발견(2026-09-21): anchorMin=(0,0)/anchorMax=(1,0)은 위아래 앵커가 같은 y=0
            // 지점이라 sizeDelta 없이는 높이가 0이다 - 알파를 1로 올려도 덮을 영역 자체가 없어 수치가
            // 그대로 다 보였다. 부모(amountLabel)와 완전히 같은 영역을 덮도록 SetStretch로 바꾼다.
            var tooltipGo = EditorUIBuilder.GetOrCreateUIObject(amountLabel.transform, "CapacityTooltip");
            EditorUIBuilder.SetStretch(tooltipGo.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureImage(tooltipGo, new Color(0f, 0f, 0f, 1f));
            EditorUIBuilder.EnsureMarker(tooltipGo, HubUIElementIds.CurrencyCapacityTooltip);

            var tooltipLabel = EditorUIBuilder.EnsureLabel(tooltipGo.transform, "상한 -");
            EditorUIBuilder.EnsureMarker(tooltipLabel.gameObject, HubUIElementIds.CurrencyCapacityTooltipText);
            tooltipLabel.color = Color.white; // 어두운 배경(§ 위) 기준 - EnsureLabel 기본 검정이면 안 보인다.

            tooltipGo.SetActive(false);
        }

        // ==================== 배치(Formation) UI ====================
        // 실제 조립 로직은 FormationUIBuilder(Hub/Field 공용)에 있다 - Field 씬에서도 "정비창 재호출"이
        // 동작하려면 같은 화면이 필요해서 공용화되어 있다(FieldUIInstaller 참고).
        private static void BuildFormationUI(Transform depthLayer)
        {
            FormationUIBuilder.EnsurePrefabFolder();
            var slotPrefab = FormationUIBuilder.GetOrCreateSlotPrefab();
            var iconPrefab = FormationUIBuilder.GetOrCreateIconPrefab();
            var rowPrefab = FormationUIBuilder.GetOrCreateRowPrefab();
            var pathLinePrefab = FormationUIBuilder.GetOrCreatePathLinePrefab();
            var travelerIconPrefab = FormationUIBuilder.GetOrCreateTravelerIconPrefab();
            var activityOverlayPrefab = FormationUIBuilder.GetOrCreateActivityOverlayPrefab();
            FormationUIBuilder.Build(depthLayer, slotPrefab, iconPrefab, rowPrefab, pathLinePrefab, travelerIconPrefab, activityOverlayPrefab, includeApplyButton: true);
        }

        // ==================== 상행 준비(Trip) UI ====================
        private static void BuildTripUI(Transform depthLayer)
        {
            EnsureTripPrefabFolder();
            GetOrCreateCityMarkerPrefab(); // TripPanel.debugCityMarkerPrefab에 수동 연결 대상(에셋만 미리 생성)
            GetOrCreateRoadLinePrefab();   // TripPanel.debugRoadLinePrefab에 수동 연결 대상(에셋만 미리 생성)

            var panelRoot = EditorUIBuilder.GetOrCreateUIObject(depthLayer, "TripPanel");
            EditorUIBuilder.SetStretch(panelRoot.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureMarker(panelRoot, TripUIElementIds.PanelRoot);

            BuildTripTopButtons(panelRoot.transform);
            BuildTripMap(panelRoot.transform);
            BuildTripLocationInfo(panelRoot.transform, "OriginInfo", TripUIElementIds.OriginInfoRoot, new Vector2(0.64f, 0.58f), new Vector2(0.94f, TripContentTopY));
            BuildTripLocationInfo(panelRoot.transform, "DestinationInfo", TripUIElementIds.DestinationInfoRoot, new Vector2(0.64f, 0.37f), new Vector2(0.94f, 0.57f));
            BuildTripSummary(panelRoot.transform);
            BuildTripStartButton(panelRoot.transform);
            BuildTripDebugMapControls(panelRoot.transform);

            panelRoot.SetActive(false);
        }

        // 지도 저장(디버그)과 닫기 버튼은 상단 줄의 같은 구간(0.64~0.79)을 좌/우로 양분한다 - 예전엔
        // 두 버튼이 0.70~0.78에서 겹쳐 저장 버튼이 닫기 버튼을 가렸다. 간격은 지도(~0.62)와 정보
        // 패널(0.64~) 사이 간격(0.02)을 그대로 따른다.
        private const float TripTopSharedMinX = 0.64f;
        private const float TripTopSharedMaxX = 0.79f;
        private const float TripTopSharedGap = 0.02f;
        private const float TripTopSharedButtonWidth = (TripTopSharedMaxX - TripTopSharedMinX - TripTopSharedGap) / 2f;

        // 상단 버튼 줄(디버그 지도 컨트롤/닫기/배치)의 세로 범위. 우상단 재화 HUD(상단 마진 32px +
        // 높이 약 40px = 화면 위 72px까지, ConstantPixelSize)가 예전 0.90~0.97 줄을 가려서 아래로
        // 내렸다 - 1080p 기준 줄 윗변은 화면 위에서 약 130px로 HUD 하단과 여유가 있다. 본문(지도/
        // 정보 패널) 윗변은 이 줄에서 기존 간격 0.02를 둔 TripContentTopY로 함께 압축했다.
        private const float TripTopRowMinY = 0.81f;
        private const float TripTopRowMaxY = 0.88f;
        private const float TripContentTopY = 0.79f;

        private static void BuildTripTopButtons(Transform parent)
        {
            var closeGo = EditorUIBuilder.GetOrCreateUIObject(parent, "CloseButton");
            EditorUIBuilder.SetAnchors(closeGo.GetComponent<RectTransform>(), new Vector2(TripTopSharedMaxX - TripTopSharedButtonWidth, TripTopRowMinY), new Vector2(TripTopSharedMaxX, TripTopRowMaxY));
            EditorUIBuilder.EnsureImage(closeGo, new Color(0.85f, 0.85f, 0.85f, 1f));
            EditorUIBuilder.EnsureButton(closeGo);
            EditorUIBuilder.EnsureLabel(closeGo.transform, "닫기");
            EditorUIBuilder.EnsureMarker(closeGo, TripUIElementIds.CloseButton);

            var formationGo = EditorUIBuilder.GetOrCreateUIObject(parent, "OpenFormationButton");
            EditorUIBuilder.SetAnchors(formationGo.GetComponent<RectTransform>(), new Vector2(0.80f, TripTopRowMinY), new Vector2(0.89f, TripTopRowMaxY));
            EditorUIBuilder.EnsureImage(formationGo, new Color(0.75f, 0.87f, 1f, 1f));
            EditorUIBuilder.EnsureButton(formationGo);
            EditorUIBuilder.EnsureLabel(formationGo.transform, "배치");
            EditorUIBuilder.EnsureMarker(formationGo, TripUIElementIds.OpenFormationButton);
        }

        private static void BuildTripMap(Transform parent)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(parent, "Map");
            EditorUIBuilder.SetAnchors(root.GetComponent<RectTransform>(), new Vector2(0.06f, 0.16f), new Vector2(0.62f, TripContentTopY));
            EditorUIBuilder.EnsureImage(root, new Color(0.85f, 0.9f, 0.85f, 1f));
            EditorUIBuilder.EnsureMarker(root, TripUIElementIds.MapRoot);

            var (viewport, contentGo) = EditorUIBuilder.CreateViewportAndContent(root.transform);
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta = new Vector2(TripMapView.ContentSize, TripMapView.ContentSize);
            contentRect.anchoredPosition = Vector2.zero;
            EditorUIBuilder.EnsureImage(contentGo, new Color(0.55f, 0.75f, 0.55f, 1f));

            // 예전에는 고정 출발/도착 핀(사각형)이었으나, 지도 위에 자유 배치되는 디버그 도시 아이콘이
            // 그 역할을 대신하게 되면서 더 이상 쓰이지 않는다 - 남아있던 옛 오브젝트를 정리한다.
            EditorUIBuilder.DestroyChildIfExists(contentRect, "OriginPin");
            EditorUIBuilder.DestroyChildIfExists(contentRect, "DestinationPin");

            EditorUIBuilder.ConfigureScrollRect(root, viewport, contentRect, horizontal: true, vertical: true);

            // ScrollRect는 기본적으로 마우스 휠도 자체적으로 패닝(스크롤)에 쓴다. 같은 오브젝트의
            // TripMapView.OnScroll(확대/축소)도 동시에 반응해 휠을 돌리면 줌과 스크롤이 함께 발동됐다.
            // scrollSensitivity를 0으로 두면 ScrollRect 자신의 휠 반응만 꺼지고(드래그 패닝은 별개 경로라
            // 그대로 유지), 휠은 온전히 TripMapView의 줌 전용이 된다.
            var mapScrollRect = root.GetComponent<ScrollRect>();
            mapScrollRect.scrollSensitivity = 0f;
            // TripMapView.Awake도 런타임에 같은 값을 강제하지만(09번 설계 §6), 인스톨러가 씬 상태를
            // 명시적으로 보장하는 기존 관례를 따라 여기서도 설정해 둔다.
            mapScrollRect.inertia = false;

            EditorUIBuilder.GetOrAddComponent<TripMapView>(root);
        }

        private static void BuildTripLocationInfo(Transform parent, string objectName, string markerId, Vector2 anchorMin, Vector2 anchorMax)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(parent, objectName);
            EditorUIBuilder.SetAnchors(root.GetComponent<RectTransform>(), anchorMin, anchorMax);
            EditorUIBuilder.EnsureImage(root, new Color(1f, 0.9f, 0.78f, 1f));
            EditorUIBuilder.EnsureMarker(root, markerId);

            var iconGo = EditorUIBuilder.GetOrCreateUIObject(root.transform, "Icon");
            EditorUIBuilder.SetAnchors(iconGo.GetComponent<RectTransform>(), new Vector2(0.06f, 0.55f), new Vector2(0.32f, 0.92f));
            var iconImage = EditorUIBuilder.EnsureImage(iconGo, Color.white);
            iconImage.preserveAspect = true;

            var nameGo = EditorUIBuilder.GetOrCreateUIObject(root.transform, "NameLabel");
            EditorUIBuilder.SetAnchors(nameGo.GetComponent<RectTransform>(), new Vector2(0.36f, 0.55f), new Vector2(0.96f, 0.92f));
            var nameLabel = EditorUIBuilder.GetOrAddComponent<TextMeshProUGUI>(nameGo);
            nameLabel.fontSize = 20;
            nameLabel.color = Color.black;
            nameLabel.alignment = TextAlignmentOptions.MidlineLeft;
            nameLabel.raycastTarget = false;

            var descriptionGo = EditorUIBuilder.GetOrCreateUIObject(root.transform, "DescriptionLabel");
            EditorUIBuilder.SetAnchors(descriptionGo.GetComponent<RectTransform>(), new Vector2(0.06f, 0.06f), new Vector2(0.96f, 0.48f));
            var descriptionLabel = EditorUIBuilder.GetOrAddComponent<TextMeshProUGUI>(descriptionGo);
            descriptionLabel.fontSize = 16;
            descriptionLabel.color = Color.black;
            descriptionLabel.raycastTarget = false;

            var infoView = EditorUIBuilder.GetOrAddComponent<TripLocationInfoView>(root);
            var so = new SerializedObject(infoView);
            so.FindProperty("iconImage").objectReferenceValue = iconImage;
            so.FindProperty("nameText").objectReferenceValue = nameLabel;
            so.FindProperty("descriptionText").objectReferenceValue = descriptionLabel;
            so.ApplyModifiedProperties();
        }

        private static void BuildTripSummary(Transform parent)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(parent, "Summary");
            EditorUIBuilder.SetAnchors(root.GetComponent<RectTransform>(), new Vector2(0.64f, 0.16f), new Vector2(0.94f, 0.36f));
            EditorUIBuilder.EnsureImage(root, new Color(0.9f, 0.9f, 0.96f, 1f));
            EditorUIBuilder.EnsureMarker(root, TripUIElementIds.SummaryRoot);

            var durationLabel = BuildTripSummaryRow(root.transform, "DurationDistanceLabel", 0);
            var dangerLabel = BuildTripSummaryRow(root.transform, "DangerLabel", 1);
            var formationLabel = BuildTripSummaryRow(root.transform, "FormationSummaryLabel", 2);
            var rewardLabel = BuildTripSummaryRow(root.transform, "RewardLabel", 3);

            var summaryView = EditorUIBuilder.GetOrAddComponent<TripSummaryView>(root);
            var so = new SerializedObject(summaryView);
            so.FindProperty("durationDistanceText").objectReferenceValue = durationLabel;
            so.FindProperty("dangerText").objectReferenceValue = dangerLabel;
            so.FindProperty("formationSummaryText").objectReferenceValue = formationLabel;
            so.FindProperty("rewardText").objectReferenceValue = rewardLabel;
            so.ApplyModifiedProperties();
        }

        private static TextMeshProUGUI BuildTripSummaryRow(Transform parent, string name, int rowIndex)
        {
            const int rowCount = 4;
            const float rowHeight = 1f / rowCount;
            var top = 1f - rowIndex * rowHeight;
            var bottom = top - rowHeight;

            var go = EditorUIBuilder.GetOrCreateUIObject(parent, name);
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), new Vector2(0.06f, bottom + 0.02f), new Vector2(0.94f, top - 0.02f));
            var label = EditorUIBuilder.GetOrAddComponent<TextMeshProUGUI>(go);
            label.fontSize = 16;
            label.color = Color.black;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            return label;
        }

        private static void BuildTripStartButton(Transform parent)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, "StartButton");
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), new Vector2(0.30f, 0.03f), new Vector2(0.70f, 0.14f));
            EditorUIBuilder.EnsureImage(go, new Color(0.71f, 0.32f, 0.03f, 1f));
            EditorUIBuilder.EnsureButton(go);
            var label = EditorUIBuilder.EnsureLabel(go.transform, "상행 시작");
            label.fontSize = 30;
            label.color = Color.white;
            label.fontStyle = FontStyles.Bold;
            EditorUIBuilder.EnsureMarker(go, TripUIElementIds.StartButton);
        }

        /// <summary>
        /// 지도 위 디버그 도시 배치/경로 연결(03/04번 기획 문서) 전용 컨트롤. 상단 여백(닫기/배치
        /// 버튼과 같은 줄, 그 왼쪽)에 작게 배치한다 - 원래 지도 아래에 뒀더니 상행 시작 버튼과
        /// 겹쳤다. 정식 콘텐츠가 아니므로 실제 지역 시스템이 생기면 이 메서드와 관련 프리팹 생성
        /// 로직을 통째로 제거한다(DEBUG_FEATURES.md §2 참고).
        /// </summary>
        private static void BuildTripDebugMapControls(Transform parent)
        {
            const float top = TripTopRowMinY;
            const float bottom = TripTopRowMaxY;

            var paletteGo = EditorUIBuilder.GetOrCreateUIObject(parent, "DebugCityPalette");
            EditorUIBuilder.SetAnchors(paletteGo.GetComponent<RectTransform>(), new Vector2(0.06f, top), new Vector2(0.12f, bottom));
            EditorUIBuilder.EnsureMarker(paletteGo, TripUIElementIds.DebugCityPaletteRoot);
            var paletteIcon = EditorUIBuilder.EnsureImage(paletteGo, Color.white);
            paletteIcon.sprite = FormationPlaceholderIcons.GetOrCreateCircle();
            paletteIcon.preserveAspect = true;

            var paletteView = EditorUIBuilder.GetOrAddComponent<TripDebugCityPaletteView>(paletteGo);
            var paletteSo = new SerializedObject(paletteView);
            paletteSo.FindProperty("iconImage").objectReferenceValue = paletteIcon;
            paletteSo.ApplyModifiedProperties();

            var toggleGo = EditorUIBuilder.GetOrCreateUIObject(parent, "DebugRoadToggleButton");
            EditorUIBuilder.SetAnchors(toggleGo.GetComponent<RectTransform>(), new Vector2(0.14f, top), new Vector2(0.30f, bottom));
            EditorUIBuilder.EnsureImage(toggleGo, new Color(0.95f, 0.85f, 0.6f, 1f));
            EditorUIBuilder.EnsureButton(toggleGo);
            var toggleLabel = EditorUIBuilder.EnsureLabel(toggleGo.transform, "경로 연결: OFF");
            toggleLabel.fontSize = 11;
            EditorUIBuilder.EnsureMarker(toggleGo, TripUIElementIds.DebugRoadToggleButton);

            var toggleView = EditorUIBuilder.GetOrAddComponent<TripDebugRoadToggleView>(toggleGo);
            var toggleSo = new SerializedObject(toggleView);
            toggleSo.FindProperty("toggleButton").objectReferenceValue = toggleGo.GetComponent<Button>();
            toggleSo.FindProperty("label").objectReferenceValue = toggleLabel;
            toggleSo.ApplyModifiedProperties();

            var cityDeleteGo = EditorUIBuilder.GetOrCreateUIObject(parent, "DebugCityBulkDeleteButton");
            EditorUIBuilder.SetAnchors(cityDeleteGo.GetComponent<RectTransform>(), new Vector2(0.32f, top), new Vector2(0.46f, bottom));
            EditorUIBuilder.EnsureImage(cityDeleteGo, new Color(0.9f, 0.6f, 0.6f, 1f));
            EditorUIBuilder.EnsureButton(cityDeleteGo);
            var cityDeleteLabel = EditorUIBuilder.EnsureLabel(cityDeleteGo.transform, "도시 전체삭제");
            cityDeleteLabel.fontSize = 10;
            EditorUIBuilder.EnsureMarker(cityDeleteGo, TripUIElementIds.DebugCityBulkDeleteButton);

            var roadDeleteGo = EditorUIBuilder.GetOrCreateUIObject(parent, "DebugRoadBulkDeleteButton");
            EditorUIBuilder.SetAnchors(roadDeleteGo.GetComponent<RectTransform>(), new Vector2(0.48f, top), new Vector2(0.62f, bottom));
            EditorUIBuilder.EnsureImage(roadDeleteGo, new Color(0.9f, 0.6f, 0.6f, 1f));
            EditorUIBuilder.EnsureButton(roadDeleteGo);
            var roadDeleteLabel = EditorUIBuilder.EnsureLabel(roadDeleteGo.transform, "경로 전체삭제");
            roadDeleteLabel.fontSize = 10;
            EditorUIBuilder.EnsureMarker(roadDeleteGo, TripUIElementIds.DebugRoadBulkDeleteButton);

            // 닫기 버튼과 구간을 양분해 왼쪽 절반에 둔다(TripTopShared* 상수 참고). 저장=긍정 동작이라
            // 기존 빨강(삭제)/황토(토글)와 구분되는 녹색 계열(Docs/기획/15번 §3.1, 설계 19번 §8).
            var saveGo = EditorUIBuilder.GetOrCreateUIObject(parent, "DebugMapSaveButton");
            EditorUIBuilder.SetAnchors(saveGo.GetComponent<RectTransform>(), new Vector2(TripTopSharedMinX, top), new Vector2(TripTopSharedMinX + TripTopSharedButtonWidth, bottom));
            EditorUIBuilder.EnsureImage(saveGo, new Color(0.6f, 0.85f, 0.6f, 1f));
            EditorUIBuilder.EnsureButton(saveGo);
            var saveLabel = EditorUIBuilder.EnsureLabel(saveGo.transform, "지도 저장");
            saveLabel.fontSize = 10;
            EditorUIBuilder.EnsureMarker(saveGo, TripUIElementIds.DebugMapSaveButton);
        }

        private static void EnsureTripPrefabFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
            }
            if (!AssetDatabase.IsValidFolder(TripPrefabFolder))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs/UI", "Trip");
            }
        }

        private static TripDebugCityMarkerView GetOrCreateCityMarkerPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CityMarkerPrefabPath);
            if (existing != null)
            {
                return existing.GetComponent<TripDebugCityMarkerView>();
            }

            var go = new GameObject("TripDebugCityMarker", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(48f, 48f);

            var image = go.AddComponent<Image>();
            image.sprite = FormationPlaceholderIcons.GetOrCreateCircle();
            image.color = Color.white;
            image.raycastTarget = true;

            var markerView = go.AddComponent<TripDebugCityMarkerView>();
            var so = new SerializedObject(markerView);
            so.FindProperty("iconImage").objectReferenceValue = image;
            so.ApplyModifiedProperties();

            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(go, CityMarkerPrefabPath);
            Object.DestroyImmediate(go);

            return savedPrefab.GetComponent<TripDebugCityMarkerView>();
        }

        private static TripDebugRoadLineView GetOrCreateRoadLinePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(RoadLinePrefabPath);
            if (existing != null)
            {
                // 기존 프리팹이라도 최신 필드 연결 상태로 동기화한다(재실행 안전성) - lineImage 연결이
                // 나중에 추가됐으므로, 예전에 생성된 프리팹에는 누락돼 있을 수 있다.
                var existingView = existing.GetComponent<TripDebugRoadLineView>();
                var existingSo = new SerializedObject(existingView);
                existingSo.FindProperty("lineImage").objectReferenceValue = existing.GetComponent<Image>();
                existingSo.ApplyModifiedProperties();
                return existingView;
            }

            var go = new GameObject("TripDebugRoadLine", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(100f, 6f);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.3f, 0.3f, 0.3f, 0.9f);
            image.raycastTarget = true;

            var lineView = go.AddComponent<TripDebugRoadLineView>();
            var so = new SerializedObject(lineView);
            so.FindProperty("lineImage").objectReferenceValue = image;
            so.ApplyModifiedProperties();

            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(go, RoadLinePrefabPath);
            Object.DestroyImmediate(go);

            return savedPrefab.GetComponent<TripDebugRoadLineView>();
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
            ReparentIfFound(sceneUIRoot, HubUIElementIds.Background, contentRootGo.transform);
            var background = FindMarker(sceneUIRoot, HubUIElementIds.Background);
            if (background != null)
            {
                background.transform.SetAsFirstSibling();
            }
            // Field의 같은 역할 버튼(정비창)과 표기를 통일한다(사용자 확정, 2026-09-07).
            SetLabelText(sceneUIRoot, HubUIElementIds.FormationButton, "상단 배치", minFontSize: 18f, maxFontSize: 30f);

            return contentRootGo.transform;
        }

        // 상행 준비 버튼 위로 상단 배치 → 방향성 지시 순서로 쌓고, 세 버튼의 오른쪽 변을 상행 준비
        // 버튼에 맞춘다(사용자 확정, 2026-09-24). 상행 준비/상단 배치 버튼은 코드가 아니라 씬에 원래
        // 있던 요소라 좌표를 상수로 박지 않고, 매 실행마다 상행 준비 버튼의 현재 앵커에서 계산한다 -
        // 인스펙터에서 상행 준비 버튼을 옮겨도 재실행하면 나머지 둘이 따라온다. 크기는 두 버튼의 기존
        // 크기(Hub.unity 실측), 간격은 Hub 버튼 간 기존 관례(0.02)를 그대로 쓴다.
        private const float HubActionButtonWidth = 0.1354f;
        private const float HubActionButtonHeight = 0.1154f;
        private const float HubActionButtonGap = 0.02f;

        private static void StackActionButtonsAboveDeparture(SceneUIRoot sceneUIRoot)
        {
            var departure = FindMarker(sceneUIRoot, HubUIElementIds.DepartureButton);
            if (departure == null)
            {
                Debug.LogWarning($"Hub UI에서 '{HubUIElementIds.DepartureButton}' 요소를 찾을 수 없어 버튼 정렬을 건너뛴다.");
                return;
            }

            var departureRect = (RectTransform)departure.transform;
            var rightX = departureRect.anchorMax.x;
            var bottomY = departureRect.anchorMax.y + HubActionButtonGap;

            foreach (var id in new[] { HubUIElementIds.FormationButton, HubUIElementIds.TacticsButton })
            {
                var marker = FindMarker(sceneUIRoot, id);
                if (marker == null)
                {
                    Debug.LogWarning($"Hub UI에서 '{id}' 요소를 찾을 수 없어 정렬을 건너뛴다.");
                    continue;
                }

                EditorUIBuilder.SetAnchors((RectTransform)marker.transform,
                    new Vector2(rightX - HubActionButtonWidth, bottomY),
                    new Vector2(rightX, bottomY + HubActionButtonHeight));
                bottomY += HubActionButtonHeight + HubActionButtonGap;
            }
        }

        private static void DestroyAllDirectChildrenNamed(Transform parent, string name)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child.name == name)
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }
        }

        private static void ReparentIfFound(SceneUIRoot sceneUIRoot, string id, Transform newParent)
        {
            var found = FindMarker(sceneUIRoot, id);
            if (found == null)
            {
                Debug.LogWarning($"Hub UI에서 '{id}' 요소를 찾을 수 없어 재배치를 건너뛴다. UIElementMarker가 부착되어 있는지 확인하라.");
                return;
            }

            if (found.transform.parent != newParent)
            {
                Undo.SetTransformParent(found.transform, newParent, $"Reparent {id}");
            }
        }

        // 씬에 원래부터 있던(코드로 만들어지지 않은) 버튼의 라벨 텍스트를 갱신할 때 쓴다 - "배치"
        // 버튼처럼 EditorUIBuilder.EnsureLabel로 만들어진 게 아니라 자식 이름이 "Label"이 아니므로,
        // EnsureLabel을 그대로 호출하면 새 라벨이 중복 생성된다. 대신 마커로 찾은 뒤 자식 TMP
        // 컴포넌트를 직접 갱신한다. 폰트 크기도 자동 축소(minFontSize~maxFontSize)로 맞춘다 - 인스펙터에서
        // 먼저 확정한 값(사용자 확정, 2026-09-07: 18~30)을 여기로 옮겨 재실행해도 유지되게 한다
        // (EditorUIBuilder.EnsureLabel의 autoSize 오버로드와 같은 값 의미).
        private static void SetLabelText(SceneUIRoot sceneUIRoot, string id, string text, float minFontSize, float maxFontSize)
        {
            var found = FindMarker(sceneUIRoot, id);
            if (found == null)
            {
                Debug.LogWarning($"Hub UI에서 '{id}' 요소를 찾을 수 없어 라벨 텍스트를 갱신하지 못했다.");
                return;
            }

            var label = found.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label == null)
            {
                Debug.LogWarning($"'{id}' 요소 밑에서 TextMeshProUGUI 라벨을 찾을 수 없어 텍스트를 갱신하지 못했다.");
                return;
            }

            label.text = text;
            label.enableAutoSizing = true;
            label.fontSizeMin = minFontSize;
            label.fontSizeMax = maxFontSize;
        }

        private static UIElementMarker FindMarker(SceneUIRoot sceneUIRoot, string id)
        {
            foreach (var marker in sceneUIRoot.GetComponentsInChildren<UIElementMarker>(true))
            {
                if (marker.Id == id)
                {
                    return marker;
                }
            }
            return null;
        }
    }
}
