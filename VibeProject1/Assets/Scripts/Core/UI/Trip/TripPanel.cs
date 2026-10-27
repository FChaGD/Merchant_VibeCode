using System.Collections.Generic;
#if UNITY_EDITOR
using Game.Core.DebugTools;
#endif
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// Hub 씬의 상행 준비 UI를 조율한다. 지도 표시·도착지 지정·지역 전환은 TripMapPresenter(정식, 빌드 포함)에, 지도 편집은
    /// TripMapDebugEditor(에디터 전용)에 위임한다(Docs/설계/69번 §2 - 예전엔 둘이 디버그 조율자 하나에 섞여 빌드에 지도가 없었다).
    /// 상행정보 패널의 편성 요약은 열릴 때마다 IFormationReader에서 다시 읽어온다(별도 캐시 없음 - 배치 UI를 거쳐 돌아왔을 때도
    /// 최신 상태를 보장하기 위함).
    /// </summary>
    public class TripPanel : MonoBehaviour, ITripPanel
    {
        // 마커·선 프리팹 - 예전 디버그 이름에서 옮겨 온 직렬화 값을 유지한다. ManagerHierarchyInstaller가 코드로 배선한다.
        [FormerlySerializedAs("debugCityMarkerPrefab")]
        [SerializeField] private TripMapMarkerView cityMarkerPrefab;
        [SerializeField] private TripMapMarkerView gateMarkerPrefab;
        [FormerlySerializedAs("debugRoadLinePrefab")]
        [SerializeField] private TripRoadLineView roadLinePrefab;

        public string PanelId => UIPanelIds.Trip;

        private GameObject panelRoot;
        private TripMapView mapView;
        private TMP_Dropdown regionDropdown;
        private TripLocationInfoView originInfoView;
        private TripLocationInfoView destinationInfoView;
        private TripSummaryView summaryView;
        private Button closeButton;
        private Button openFormationButton;
        private Button startButton;
        // 선택 요소(인스톨러 미실행이면 없음) - 배치 유닛 0 출발 확인(Docs/설계/79번 §9.2).
        private ConfirmDialogView departureConfirmDialog;
        // 비활성 사유 툴팁(Docs/설계/100번 §4.3) - Hub 바인딩마다 다시 등록한다. 요소가 없으면 null(버튼 판정은 그대로).
        private ITooltipHandle startBlockTooltip;
        private Canvas rootCanvas;
        private SceneUIRoot boundSceneUIRoot;
        private TripMapPresenter mapPresenter;

#if UNITY_EDITOR
        // 지도 디버그 편집 연동 지점 - Core/Debug/Trip 폴더를 지울 때는 이 필드와 RegisterDebugMapEditor도 함께 지운다.
        private TripMapDebugEditor mapDebugEditor;
#endif

        private IPanelNavigator uiManager;
        private IGameManager gameManager;
        private IFormationReader formationReader;
        private ITripInfoProvider tripInfoProvider;
        private ISceneRevealSignal sceneRevealSignal;
        private ITripCurrentLocationReader currentLocationReader;
        private ITripDestinationAssigner destinationAssigner;
        // 출발 조건(마차 대열 연결) 판정에 배치 Id → 마차·시설 해석이 필요하다(Docs/설계/79번 §9.1). 없으면 유닛을 못 찾아 연결로 본다.
        private ICaravanRosterProvider rosterProvider;
        // 출발 절차(경고 → 버림 → 구간 계획 확정 → 전환, Docs/Refactor/2026-10-26_씬전용.md D-1) - Hub 바인딩마다 이번 씬의 확인 창으로 새로 만든다.
        private TripDepartureFlow departureFlow;
        // 시작 버튼 판정은 패널이 열려 있을 때만 한다(HO-3) - 상단 배치 화면 편집마다 대열 연결을 계산하지 않게. 열 때 한 번 판정한다.
        private bool isOpen;

        // 화면(Hub)이 완전히 드러나기 전까지는 "상행 시작"을 막는다(사용자 확정) - 도착지 배정 게이팅과 AND로 합친다.
        private bool sceneRevealed;

        public void RegisterTripUI(SceneUIRoot sceneUIRoot, IPanelNavigator uiManager, IGameManager gameManager, IFormationReader formationReader, ITripInfoProvider tripInfoProvider, ISceneRevealSignal sceneRevealSignal, ITripCurrentLocationReader currentLocationReader, ITripDestinationAssigner destinationAssigner, IReadOnlyList<IInventoryStagingDiscarder> inventoryStaging, IWorldMapReader worldMap, ITripRouteReader routeReader, ITripDeparture tripDeparture, ICaravanRosterProvider rosterProvider, IGoldHoldingsReader goldHoldings, IGoldDepartureSettlement goldDeparture, IQuestCargoWarnings questWarnings = null)
        {
            this.uiManager = uiManager;
            this.gameManager = gameManager;
            // 저장소는 Bootstrap 상주라 Hub를 반복 방문해도 구독이 누적되지 않게 이전 구독부터 해제한다.
            if (this.formationReader != null) this.formationReader.Changed -= RefreshStartButtonInteractable;
            this.formationReader = formationReader;
            if (formationReader != null) formationReader.Changed += RefreshStartButtonInteractable;
            this.rosterProvider = rosterProvider;
            this.tripInfoProvider = tripInfoProvider;
            this.sceneRevealSignal = sceneRevealSignal;
            this.currentLocationReader = currentLocationReader;
            this.destinationAssigner = destinationAssigner;
            // "상행 시작"은 패널이 열린 채 Field로 넘어가 Close가 불리지 않는다 - 새 Hub 로드는 닫힌 상태에서 시작한다.
            isOpen = false;

            // 이전 Hub 방문의 지도 표시는 파괴된 씬 오브젝트를 가리키므로 버린다.
            mapPresenter?.Dispose();
            mapPresenter = null;
#if UNITY_EDITOR
            mapDebugEditor?.Dispose();
            mapDebugEditor = null;
#endif

            if (!TryBind(sceneUIRoot))
            {
                return;
            }

            boundSceneUIRoot = sceneUIRoot;
            // 임시 보관은 버튼 활성 조건이 아니라 출발 경고로 다룬다(2026-10-15) - 출발 시점에만 읽으므로 변경 구독이 필요 없다.
            departureFlow = new TripDepartureFlow(departureConfirmDialog, inventoryStaging, goldHoldings, goldDeparture, tripDeparture, currentLocationReader, destinationAssigner,
                () => gameManager.RequestSceneTransition(ContentSceneId.Field), questWarnings);
            rootCanvas = panelRoot.GetComponentInParent<Canvas>()?.rootCanvas;
            SetupMapPresenter(worldMap, routeReader);

            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() =>
            {
                // 배치 UI 왕복 중에는 배정을 유지하고, 상행 준비 UI를 완전히 종료할 때만 도착지 배정을 초기화한다.
                destinationAssigner?.Reset();
                uiManager.Close(PanelId);
            });

            openFormationButton.onClick.RemoveAllListeners();
            openFormationButton.onClick.AddListener(() => uiManager.Open(UIPanelIds.Formation));

            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(() => departureFlow.Start(CountPlacedUnits() > 0));

            if (destinationAssigner != null)
            {
                destinationAssigner.Changed -= RefreshStartButtonInteractable;
                destinationAssigner.Changed += RefreshStartButtonInteractable;
                // 도착지를 바꾸면 정보 패널(구간별 소요시간·등급)도 바로 바뀐다 - 예전엔 패널을 열 때만 갱신했다(Docs/설계/76번 §7.1).
                destinationAssigner.Changed -= RefreshSummary;
                destinationAssigner.Changed += RefreshSummary;
            }

            sceneRevealed = false;
            sceneRevealSignal.SceneRevealed -= HandleSceneRevealed;
            sceneRevealSignal.SceneRevealed += HandleSceneRevealed;
            RefreshStartButtonInteractable();

            panelRoot.SetActive(false);
        }

        private void SetupMapPresenter(IWorldMapReader worldMap, ITripRouteReader routeReader)
        {
            if (worldMap == null || routeReader == null || currentLocationReader == null || destinationAssigner == null
                || cityMarkerPrefab == null || gateMarkerPrefab == null || roadLinePrefab == null)
            {
                Debug.LogWarning($"{nameof(TripPanel)}: 지도 표시에 필요한 요소(월드 지도·도착지·마커/선 프리팹) 중 일부가 없어 지도를 그리지 않는다(Tools > Game > Build Bootstrap Scene).");
                return;
            }

            mapPresenter = new TripMapPresenter(mapView, cityMarkerPrefab, gateMarkerPrefab, roadLinePrefab, regionDropdown,
                originInfoView, destinationInfoView, worldMap, routeReader, currentLocationReader, destinationAssigner);
        }

#if UNITY_EDITOR
        public void RegisterDebugMapEditor(IWorldMapEditor editor)
        {
            if (mapPresenter == null || boundSceneUIRoot == null || editor == null) return;

            mapDebugEditor = new TripMapDebugEditor();
            if (!mapDebugEditor.TryBind(boundSceneUIRoot, mapPresenter, editor, currentLocationReader, destinationAssigner, rootCanvas != null ? rootCanvas.transform : null))
            {
                mapDebugEditor = null;
            }
        }
#endif

        private void HandleSceneRevealed(ContentSceneId sceneId)
        {
            if (sceneId != ContentSceneId.Hub)
            {
                return;
            }

            sceneRevealed = true;
            RefreshStartButtonInteractable();
        }

        // 지도가 빌드에도 있으므로 빌드에서도 도착지가 있어야 "상행 시작"이 켜진다(Docs/설계/69번 §9-3).
        private void RefreshStartButtonInteractable()
        {
            // destinationAssigner·formationReader(Bootstrap 상주)의 Changed는 Hub가 언로드된 상태에서도 발화할 수 있다 - 버튼이 파괴됐으면 무시한다.
            if (startButton == null || !isOpen)
            {
                return;
            }

            var hasDestination = destinationAssigner != null && destinationAssigner.IsAssigned;
            var reasons = TripStartBlockReasons.Collect(sceneRevealed, hasDestination, IsFormationConnected());
            startButton.interactable = reasons.Count == 0;
            startBlockTooltip?.SetText(string.Join("\n", reasons));
        }

        // 마차 덩어리가 하나 이하여야 출발할 수 있다(기획 78 §4.8). 배치가 없으면 판정할 마차가 없으므로 충족으로 본다.
        private bool IsFormationConnected()
        {
            if (formationReader == null || !formationReader.TryLoadCurrent(out var layout) || layout == null)
            {
                return true;
            }

            return FormationArea.Compute(layout, FormationAreaRules.LookupFrom(rosterProvider)).WagonsConnected;
        }

        // 마차·시설·캐릭터 구분 없이 배치 칸 수(기획 77 §4-30) - 출발 경고(배치 유닛 0)와 편성 요약이 같은 조회를 쓴다. 배치가 없으면 0.
        private int CountPlacedUnits()
            => formationReader != null && formationReader.TryLoadCurrent(out var layout) && layout != null ? layout.CountPlacedUnits() : 0;

        private bool TryBind(SceneUIRoot sceneUIRoot)
        {
            if (!sceneUIRoot.TryGetElement<Transform>(TripUIElementIds.PanelRoot, out var rootTransform))
            {
                WarnMissing(TripUIElementIds.PanelRoot);
                return false;
            }
            panelRoot = rootTransform.gameObject;

            if (!sceneUIRoot.TryGetElement<TripMapView>(TripUIElementIds.MapRoot, out mapView))
            {
                WarnMissing(TripUIElementIds.MapRoot);
                return false;
            }

            if (!sceneUIRoot.TryGetElement<TripLocationInfoView>(TripUIElementIds.OriginInfoRoot, out originInfoView))
            {
                WarnMissing(TripUIElementIds.OriginInfoRoot);
                return false;
            }

            if (!sceneUIRoot.TryGetElement<TripLocationInfoView>(TripUIElementIds.DestinationInfoRoot, out destinationInfoView))
            {
                WarnMissing(TripUIElementIds.DestinationInfoRoot);
                return false;
            }

            if (!sceneUIRoot.TryGetElement<TripSummaryView>(TripUIElementIds.SummaryRoot, out summaryView))
            {
                WarnMissing(TripUIElementIds.SummaryRoot);
                return false;
            }

            if (!sceneUIRoot.TryGetElement<Button>(TripUIElementIds.CloseButton, out closeButton))
            {
                WarnMissing(TripUIElementIds.CloseButton);
                return false;
            }

            if (!sceneUIRoot.TryGetElement<Button>(TripUIElementIds.OpenFormationButton, out openFormationButton))
            {
                WarnMissing(TripUIElementIds.OpenFormationButton);
                return false;
            }

            if (!sceneUIRoot.TryGetElement<Button>(TripUIElementIds.StartButton, out startButton))
            {
                WarnMissing(TripUIElementIds.StartButton);
                return false;
            }

            // 아래 요소들은 없어도(인스톨러 미실행) 패널은 동작한다 - 툴팁 없이 버튼만 비활성, 경고 없이 바로 출발.
            BindStartBlockTooltip(sceneUIRoot);

            if (!sceneUIRoot.TryGetElement(TripUIElementIds.DepartureConfirmDialog, out departureConfirmDialog))
            {
                WarnMissing(TripUIElementIds.DepartureConfirmDialog);
            }
            else
            {
                departureConfirmDialog.Hide();
            }

            // 지역 드롭다운이 없어도(인스톨러 미실행) 지도는 그린다 - 관문 클릭으로만 전환된다.
            if (!sceneUIRoot.TryGetElement(TripUIElementIds.RegionDropdown, out regionDropdown))
            {
                WarnMissing(TripUIElementIds.RegionDropdown);
            }

            return true;
        }

        // 이전 Hub 방문의 등록은 파괴된 요소를 가리키므로 먼저 해제한다 - 남기면 중계 구독이 방문마다 쌓인다.
        private void BindStartBlockTooltip(SceneUIRoot sceneUIRoot)
        {
            startBlockTooltip?.Dispose();
            startBlockTooltip = null;

            var tooltips = GetComponent<ITooltipController>();
            if (tooltips == null)
            {
                throw new System.InvalidOperationException($"{nameof(TripPanel)}와 같은 GameObject에 {nameof(ITooltipController)} 구현체가 없다.");
            }

            if (!startButton.TryGetComponent<PointerHoverRelay>(out var hover) || !startButton.TryGetComponent<PointerPressRelay>(out var press))
            {
                WarnMissing($"{TripUIElementIds.StartButton}의 {nameof(PointerHoverRelay)}/{nameof(PointerPressRelay)}");
                return;
            }

            if (!sceneUIRoot.TryGetElement<RectTransform>(TripUIElementIds.StartBlockTooltip, out var tooltipRoot))
            {
                WarnMissing(TripUIElementIds.StartBlockTooltip);
                return;
            }

            if (!sceneUIRoot.TryGetElement<TMP_Text>(TripUIElementIds.StartBlockTooltipText, out var tooltipText))
            {
                WarnMissing(TripUIElementIds.StartBlockTooltipText);
                return;
            }

            startBlockTooltip = tooltips.Register(hover, press, tooltipRoot.gameObject, tooltipText);
        }

        private static void WarnMissing(string id)
        {
            Debug.LogWarning($"상행 준비 UI에서 '{id}' 요소를 찾을 수 없다. {nameof(UIElementMarker)}가 부착되어 있는지 확인하라.");
        }

        public void Open()
        {
            if (panelRoot == null)
            {
                return;
            }

            // 출발/도착 정보 패널은 여기서 강제로 비우지 않는다 - 배치 UI를 갔다 와도 배정 상태가 유지된다.
            isOpen = true;
            RefreshSummary();
            RefreshStartButtonInteractable();

            panelRoot.SetActive(true);

            // TripMapView.Content는 패널이 처음 켜질 때 Awake로 생기므로 켠 뒤에 그린다.
            mapPresenter?.Show();
        }

        // 순수 "숨기기"만 한다. Hub로 돌아갈지 이전 패널로 돌아갈지는 UIManager.Close(PanelId)가 결정하므로
        // 버튼 등 외부에서 패널을 닫을 때는 이 메서드를 직접 호출하지 말고 반드시 uiManager.Close(PanelId)를 거칠 것.
        public void Close()
        {
            if (panelRoot == null)
            {
                return;
            }

            isOpen = false;
            mapPresenter?.Hide();
            if (departureConfirmDialog != null) departureConfirmDialog.Hide();
            panelRoot.SetActive(false);
        }

        private void RefreshSummary()
        {
            // destinationAssigner(Bootstrap 상주)의 Changed는 Hub가 언로드된 상태에서도 발화할 수 있다 - 뷰가 파괴됐으면 무시한다.
            if (summaryView == null)
            {
                return;
            }

            var summary = tripInfoProvider != null
                ? tripInfoProvider.GetTripSummary()
                : new TripSummary(PlaceholderText.Missing, PlaceholderText.Missing, PlaceholderText.Missing);

            summaryView.SetValues(summary.EstimatedDurationDistanceText, summary.DangerText, BuildFormationSummaryText(), summary.RewardText);
        }

        private string BuildFormationSummaryText()
        {
            if (formationReader == null || !formationReader.TryLoadCurrent(out var layout))
            {
                return "편성 없음";
            }

            return $"{layout.CountPlacedUnits()}명 편성";
        }
    }
}
