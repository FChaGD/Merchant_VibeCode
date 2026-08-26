#if UNITY_EDITOR
using Game.Core.DebugTools;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// Hub 씬의 상행 준비 UI를 조율한다. 지도의 출발/도착 핀 클릭에 따라 정보 패널을 채우고,
    /// 상행정보 패널의 편성 요약은 열릴 때마다 IFormationReader에서 다시 읽어온다(별도 캐시 없음 -
    /// 배치 UI를 거쳐 돌아왔을 때도 최신 상태를 보장하기 위함).
    /// </summary>
    public class TripPanel : MonoBehaviour, ITripPanel
    {
#if UNITY_EDITOR
        [SerializeField] private TripDebugCityMarkerView debugCityMarkerPrefab;
        [SerializeField] private TripDebugRoadLineView debugRoadLinePrefab;
#endif

        public string PanelId => UIPanelIds.Trip;

        private GameObject panelRoot;
        private TripMapView mapView;
        private TripLocationInfoView originInfoView;
        private TripLocationInfoView destinationInfoView;
        private TripSummaryView summaryView;
        private Button closeButton;
        private Button openFormationButton;
        private Button startButton;
        private Canvas rootCanvas;

#if UNITY_EDITOR
        // 지도 위 디버그 도시 배치/경로 연결 + 출발·도착 지정 배선 전체의 연동 지점 - Core/Debug/Trip
        // 폴더를 지울 때는 이 필드들과 SetupDebugMapInteraction, TryBind/RefreshStartButtonInteractable
        // 안의 #if UNITY_EDITOR 블록도 함께 지운다(DEBUG_FEATURES.md 참고).
        private TripDebugCityPaletteView debugCityPaletteView;
        private TripDebugRoadToggleView debugRoadToggleView;
        private Button debugCityBulkDeleteButton;
        private Button debugRoadBulkDeleteButton;
        private TripMapInteractionCoordinator mapInteractionCoordinator;
#endif

        private IUIManager uiManager;
        private IGameManager gameManager;
        private IFormationReader formationReader;
        private ITripInfoProvider tripInfoProvider;
        private ISceneRevealSignal sceneRevealSignal;

        // 화면(Hub)이 완전히 드러나기 전까지는 "상행 시작"을 막는다(사용자 확정) - 출발/도착 배정
        // 게이팅(RefreshStartButtonInteractable)과 별개 조건이라 AND로 합친다.
        private bool sceneRevealed;

        public void RegisterTripUI(IUIManager uiManager, IGameManager gameManager, IFormationReader formationReader, ITripInfoProvider tripInfoProvider, ISceneRevealSignal sceneRevealSignal)
        {
            this.uiManager = uiManager;
            this.gameManager = gameManager;
            this.formationReader = formationReader;
            this.tripInfoProvider = tripInfoProvider;
            this.sceneRevealSignal = sceneRevealSignal;

            var hubScene = SceneManager.GetSceneByName(SceneNames.Hub);
            if (!hubScene.IsValid())
            {
                Debug.LogWarning($"'{SceneNames.Hub}' 씬을 찾을 수 없어 상행 준비 UI를 등록하지 못했다.");
                return;
            }

            SceneUIRoot sceneUIRoot = null;
            foreach (var rootObject in hubScene.GetRootGameObjects())
            {
                sceneUIRoot = rootObject.GetComponentInChildren<SceneUIRoot>(true);
                if (sceneUIRoot != null)
                {
                    break;
                }
            }

            if (sceneUIRoot == null)
            {
                Debug.LogWarning($"'{SceneNames.Hub}' 씬에서 {nameof(SceneUIRoot)}를 찾을 수 없다.");
                return;
            }

            if (!TryBind(sceneUIRoot))
            {
                return;
            }

            rootCanvas = panelRoot.GetComponentInParent<Canvas>()?.rootCanvas;

#if UNITY_EDITOR
            SetupDebugMapInteraction();
#endif

            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() =>
            {
                // 배치 UI 왕복(openFormationButton) 중에는 배정을 유지하고, 상행 준비 UI를 완전히
                // 종료할 때만 출발/도착 배정과 지도 강조를 초기화한다.
#if UNITY_EDITOR
                mapInteractionCoordinator?.ResetOriginDestination();
#endif
                uiManager.Close(PanelId);
            });

            openFormationButton.onClick.RemoveAllListeners();
            openFormationButton.onClick.AddListener(() => uiManager.Open(UIPanelIds.Formation));

            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(() => gameManager.RequestSceneTransition(ContentSceneId.Field));

            // Hub 화면이 완전히 드러날 때까지 "상행 시작"을 막는다 - 매번 재구독 전 해제해 Hub를
            // 반복 방문해도 구독이 누적되지 않게 한다(sceneRevealSignal은 Bootstrap 상주 영속 객체).
            sceneRevealed = false;
            sceneRevealSignal.SceneRevealed -= HandleSceneRevealed;
            sceneRevealSignal.SceneRevealed += HandleSceneRevealed;
            RefreshStartButtonInteractable();

            panelRoot.SetActive(false);
        }

#if UNITY_EDITOR
        // 지도 위 도시 배치/경로 연결(디버그, 03/04번 기획)과 출발/도착 지정(정식, 02번 기획) 배선을
        // 모두 TripMapInteractionCoordinator에 위임한다(SRP) - 둘 다 같은 지도/도시 데이터를 다루고,
        // TripPanel이 직접 들고 있으면 책임이 비대해진다. 필요한 요소 중 하나라도 씬에 없으면(예: 아직
        // 인스톨러를 재실행하지 않음) 이 기능 전체를 건너뛰고 나머지 상행 준비 UI(요약/버튼)는 정상
        // 동작해야 한다.
        private void SetupDebugMapInteraction()
        {
            if (debugCityPaletteView == null || debugRoadToggleView == null
                || debugCityBulkDeleteButton == null || debugRoadBulkDeleteButton == null
                || debugCityMarkerPrefab == null || debugRoadLinePrefab == null)
            {
                Debug.LogWarning($"{nameof(TripPanel)}: 지도 디버그 배치/경로 연결 요소 중 일부가 연결되지 않아 해당 기능을 건너뛴다.");
                return;
            }

            mapInteractionCoordinator = new TripMapInteractionCoordinator();
            mapInteractionCoordinator.Bind(
                mapView,
                debugCityMarkerPrefab,
                debugCityPaletteView,
                debugRoadLinePrefab,
                debugRoadToggleView,
                debugCityBulkDeleteButton,
                debugRoadBulkDeleteButton,
                rootCanvas != null ? rootCanvas.transform : null,
                originInfoView,
                destinationInfoView);

            // "상행 시작"은 출발/도착이 모두 배정돼야 활성화된다(02번 5절) - 배정이 바뀔 때마다 갱신.
            mapInteractionCoordinator.OriginDestinationReader.Changed += RefreshStartButtonInteractable;
            RefreshStartButtonInteractable();
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

        private void RefreshStartButtonInteractable()
        {
#if UNITY_EDITOR
            var debugReady = mapInteractionCoordinator?.OriginDestinationReader.IsBothAssigned ?? true;
#else
            var debugReady = true;
#endif
            startButton.interactable = sceneRevealed && debugReady;
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

#if UNITY_EDITOR
            // 지도 디버그 배치/경로 연결 요소는 보조 기능이라 없어도 나머지 상행 준비 UI는 정상 동작해야
            // 한다 - 없으면 SetupDebugMapInteraction에서 조용히 건너뛴다.
            sceneUIRoot.TryGetElement<TripDebugCityPaletteView>(TripUIElementIds.DebugCityPaletteRoot, out debugCityPaletteView);
            sceneUIRoot.TryGetElement<TripDebugRoadToggleView>(TripUIElementIds.DebugRoadToggleButton, out debugRoadToggleView);
            sceneUIRoot.TryGetElement<Button>(TripUIElementIds.DebugCityBulkDeleteButton, out debugCityBulkDeleteButton);
            sceneUIRoot.TryGetElement<Button>(TripUIElementIds.DebugRoadBulkDeleteButton, out debugRoadBulkDeleteButton);
#endif

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

            // 출발/도착 정보 패널은 여기서 강제로 비우지 않는다 - 배치 UI를 갔다 와도 배정 상태(및
            // 지도 강조)가 유지되므로, 정보 패널도 그 상태를 그대로 반영해야 한다(assigner.Changed가
            // 배정 시점에 이미 채워/비워 둔 값을 그대로 둔다). 완전 초기화는 종료 버튼에서만 일어난다.
            RefreshSummary();

            panelRoot.SetActive(true);
        }

        // 순수 "숨기기"만 한다. Hub로 돌아갈지 이전 패널로 돌아갈지는 UIManager.Close(PanelId)가 결정하므로
        // 버튼 등 외부에서 패널을 닫을 때는 이 메서드를 직접 호출하지 말고 반드시 uiManager.Close(PanelId)를 거칠 것.
        public void Close()
        {
            if (panelRoot == null)
            {
                return;
            }

            panelRoot.SetActive(false);
        }

        private void RefreshSummary()
        {
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
