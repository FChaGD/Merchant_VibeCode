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
        // 선택 요소(인스톨러 미실행이면 없음) - 끊어진 대열 안내와 배치 유닛 0 출발 확인(Docs/설계/79번 §9).
        private TMP_Text disconnectedNoticeLabel;
        private ConfirmDialogView departureConfirmDialog;
        private Canvas rootCanvas;
        private SceneUIRoot boundSceneUIRoot;
        private TripMapPresenter mapPresenter;

#if UNITY_EDITOR
        // 지도 디버그 편집 연동 지점 - Core/Debug/Trip 폴더를 지울 때는 이 필드와 RegisterDebugMapEditor도 함께 지운다.
        private TripMapDebugEditor mapDebugEditor;
#endif

        private IUIManager uiManager;
        private IGameManager gameManager;
        private IFormationReader formationReader;
        private ITripInfoProvider tripInfoProvider;
        private ISceneRevealSignal sceneRevealSignal;
        private ITripCurrentLocationReader currentLocationReader;
        // "상행 시작" 때 구간 계획을 확정한다(Docs/설계/76번 §5). 없으면(인스톨러 미실행) 예전처럼 여정 없이 30초 상행.
        private ITripDeparture tripDeparture;
        private ITripDestinationAssigner destinationAssigner;
        // 출발 조건(마차 대열 연결) 판정에 배치 Id → 마차·시설 해석이 필요하다(Docs/설계/79번 §9.1). 없으면 유닛을 못 찾아 연결로 본다.
        private ICaravanRosterProvider rosterProvider;

        // 화면(Hub)이 완전히 드러나기 전까지는 "상행 시작"을 막는다(사용자 확정) - 도착지 배정 게이팅과 AND로 합친다.
        private bool sceneRevealed;

        // 임시 보관 영역에 아이템이 있으면 "상행 시작"을 막는다(Docs/설계/40번 §5.5).
        private IReadOnlyList<IInventoryStagingReader> inventoryStagingReaders = System.Array.Empty<IInventoryStagingReader>();

        public void RegisterTripUI(SceneUIRoot sceneUIRoot, IUIManager uiManager, IGameManager gameManager, IFormationReader formationReader, ITripInfoProvider tripInfoProvider, ISceneRevealSignal sceneRevealSignal, ITripCurrentLocationReader currentLocationReader, ITripDestinationAssigner destinationAssigner, IReadOnlyList<IInventoryStagingReader> inventoryStagingReaders, IWorldMapReader worldMap, ITripRouteReader routeReader, ITripDeparture tripDeparture, ICaravanRosterProvider rosterProvider)
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
            this.tripDeparture = tripDeparture;
            this.destinationAssigner = destinationAssigner;

            // 저장소는 Bootstrap 상주라 Hub를 반복 방문해도 구독이 누적되지 않게 이전 구독부터 해제한다.
            foreach (var reader in this.inventoryStagingReaders) reader.OnChanged -= RefreshStartButtonInteractable;
            this.inventoryStagingReaders = inventoryStagingReaders ?? System.Array.Empty<IInventoryStagingReader>();
            foreach (var reader in this.inventoryStagingReaders) reader.OnChanged += RefreshStartButtonInteractable;

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
            startButton.onClick.AddListener(() => StartTrip(gameManager));

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
            if (startButton == null)
            {
                return;
            }

            var connected = IsFormationConnected();
            var hasDestination = destinationAssigner != null && destinationAssigner.IsAssigned;
            startButton.interactable = sceneRevealed && hasDestination && !HasStagedInventoryItems() && connected;

            // 안내 라벨은 선택 요소 - 같은 이유로 파괴됐을 수 있어 Unity null 비교로 걸러낸다.
            if (disconnectedNoticeLabel != null)
            {
                disconnectedNoticeLabel.text = DisconnectedNoticeText;
                disconnectedNoticeLabel.gameObject.SetActive(!connected);
            }
        }

        private const string DisconnectedNoticeText = "대열이 끊어져 있습니다. 정비창에서 마차를 이어 붙이세요.";
        private const string EmptyFormationWarningText = "상단에 배치된 유닛이 없습니다. 이대로 습격받으면 위험합니다. 그래도 출발하시겠습니까?";

        // 마차 덩어리가 하나 이하여야 출발할 수 있다(기획 78 §4.8). 배치가 없으면 판정할 마차가 없으므로 충족으로 본다.
        private bool IsFormationConnected()
        {
            if (formationReader == null || !formationReader.TryLoadCurrent(out var layout) || layout == null)
            {
                return true;
            }

            return FormationArea.Compute(layout, FormationAreaRules.LookupFrom(rosterProvider)).WagonsConnected;
        }

        // 마차·시설·캐릭터 구분 없이 배치 칸에 Id가 하나라도 있으면 유닛이 있는 것으로 본다(기획 77 §4-30).
        private bool HasAnyPlacedUnit()
        {
            if (formationReader == null || !formationReader.TryLoadCurrent(out var layout) || layout == null)
            {
                return false;
            }

            for (var i = 0; i < layout.SlotCount; i++)
            {
                if (!string.IsNullOrEmpty(layout.GetUnitId(i))) return true;
            }
            return false;
        }

        private bool HasStagedInventoryItems()
        {
            foreach (var reader in inventoryStagingReaders)
            {
                if (reader.StagedItems.Count > 0) return true;
            }
            return false;
        }

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

            // 아래 두 요소는 없어도(인스톨러 미실행) 패널은 동작한다 - 안내 없이 버튼만 비활성, 경고 없이 바로 출발.
            if (!sceneUIRoot.TryGetElement(TripUIElementIds.DisconnectedNotice, out disconnectedNoticeLabel))
            {
                WarnMissing(TripUIElementIds.DisconnectedNotice);
            }

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

            mapPresenter?.Hide();
            if (departureConfirmDialog != null) departureConfirmDialog.Hide();
            panelRoot.SetActive(false);
        }

        // 배치 유닛 0은 출발을 막지 않고 확인만 받는다(기획 77 §4-30) - 연결 조건(버튼 비활성)과는 별개(기획 78 §4-35).
        // 대화상자가 없으면(인스톨러 미실행) 경고 없이 예전처럼 바로 출발한다.
        private void StartTrip(IGameManager gameManager)
        {
            if (!HasAnyPlacedUnit() && departureConfirmDialog != null)
            {
                departureConfirmDialog.Show(EmptyFormationWarningText, "출발", "취소", () => Depart(gameManager), null);
                return;
            }

            Depart(gameManager);
        }

        private void Depart(IGameManager gameManager)
        {
            if (tripDeparture != null && currentLocationReader != null && destinationAssigner?.DestinationCityId is { } destinationCityId
                && !tripDeparture.TryDepart(currentLocationReader.CurrentCityId, destinationCityId))
            {
                Debug.LogWarning($"{nameof(TripPanel)}: 현재 위치({currentLocationReader.CurrentCityId}) → 도착지({destinationCityId}) 경로를 찾지 못해 여정 없이 출발한다.");
            }
            gameManager.RequestSceneTransition(ContentSceneId.Field);
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
                : new TripSummary("값 없음", "값 없음", "값 없음");

            summaryView.SetValues(summary.EstimatedDurationDistanceText, summary.DangerText, BuildFormationSummaryText(), summary.RewardText);
        }

        private string BuildFormationSummaryText()
        {
            if (formationReader == null || !formationReader.TryLoadCurrent(out var layout))
            {
                return "편성 없음";
            }

            var occupied = 0;
            for (var i = 0; i < layout.SlotCount; i++)
            {
                if (!string.IsNullOrEmpty(layout.GetUnitId(i)))
                {
                    occupied++;
                }
            }

            return $"{occupied}명 편성";
        }
    }
}
