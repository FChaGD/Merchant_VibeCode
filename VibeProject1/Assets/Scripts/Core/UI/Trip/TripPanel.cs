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
        public string PanelId => UIPanelIds.Trip;

        private GameObject panelRoot;
        private TripMapView mapView;
        private TripLocationInfoView originInfoView;
        private TripLocationInfoView destinationInfoView;
        private TripSummaryView summaryView;
        private Button closeButton;
        private Button openFormationButton;
        private Button startButton;

        private IUIManager uiManager;
        private IGameManager gameManager;
        private IFormationReader formationReader;
        private ITripInfoProvider tripInfoProvider;

        public void RegisterTripUI(IUIManager uiManager, IGameManager gameManager, IFormationReader formationReader, ITripInfoProvider tripInfoProvider)
        {
            this.uiManager = uiManager;
            this.gameManager = gameManager;
            this.formationReader = formationReader;
            this.tripInfoProvider = tripInfoProvider;

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

            mapView.Initialize(HandleOriginPinClicked, HandleDestinationPinClicked);

            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() => uiManager.Close(PanelId));

            openFormationButton.onClick.RemoveAllListeners();
            openFormationButton.onClick.AddListener(() => uiManager.Open(UIPanelIds.Formation));

            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(() => gameManager.RequestSceneTransition(SceneNames.Field));

            panelRoot.SetActive(false);
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

            originInfoView.Clear();
            destinationInfoView.Clear();
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

        private void HandleOriginPinClicked() => originInfoView.Show(tripInfoProvider?.GetOrigin());

        private void HandleDestinationPinClicked() => destinationInfoView.Show(tripInfoProvider?.GetDestination());

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
