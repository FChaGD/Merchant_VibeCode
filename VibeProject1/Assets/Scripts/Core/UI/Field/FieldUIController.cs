using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// Field 씬의 이동 뷰(진행 게이지, 배경, 정비창 재호출)를 조율한다. 인카운터 발생 시 경고창 점멸,
    /// 전투 뷰 전환, 결과 팝업 처리는 FieldEncounterFlowCoordinator에 위임한다(SRP) - 도착 처리는
    /// 아직 구현하지 않았다(Docs/설계/04_Field씬_아키텍처.md §5.3 참고).
    /// </summary>
    public class FieldUIController : MonoBehaviour, IFieldUIController
    {
        [SerializeField] private Sprite backgroundSprite;

        private FieldProgressGaugeView gaugeView;
        private Button formationButton;
        private FieldEncounterWarningView warningView;
        private RectTransform movementViewRoot;
        private RectTransform battleViewRoot;
        private FieldResultPopupView resultPopupView;
        private FieldEncounterFlowCoordinator flowCoordinator;

        public void RegisterFieldUI(IUIManager uiManager, ISessionState sessionState, IEncounterManager encounterManager, IBattleController battleController, IBattleResultSource battleResultSource)
        {
            var fieldScene = SceneManager.GetSceneByName(SceneNames.Field);
            if (!fieldScene.IsValid())
            {
                Debug.LogWarning($"'{SceneNames.Field}' 씬을 찾을 수 없어 Field UI를 등록하지 못했다.");
                return;
            }

            SceneUIRoot sceneUIRoot = null;
            foreach (var rootObject in fieldScene.GetRootGameObjects())
            {
                sceneUIRoot = rootObject.GetComponentInChildren<SceneUIRoot>(true);
                if (sceneUIRoot != null)
                {
                    break;
                }
            }

            if (sceneUIRoot == null)
            {
                Debug.LogWarning($"'{SceneNames.Field}' 씬에서 {nameof(SceneUIRoot)}를 찾을 수 없다.");
                return;
            }

            if (!TryBind(sceneUIRoot))
            {
                return;
            }

            formationButton.onClick.RemoveAllListeners();
            formationButton.onClick.AddListener(() => uiManager.Open(UIPanelIds.Formation));

            // Field 씬은 상행마다 다시 로드되지만 sessionState(SessionStateTracker)는 Bootstrap에 상주하는
            // 영속 객체다 - 재구독 전 항상 먼저 해제해 상행을 반복할수록 구독이 누적되는 것을 막는다
            // (Docs/설계/04_Field씬_아키텍처.md §5 이벤트 구독 수명주기 참고).
            sessionState.OnProgressChanged -= HandleProgressChanged;
            sessionState.OnProgressChanged += HandleProgressChanged;

            // encounterManager/battleResultSource는 Bootstrap 상주 영속 객체다 - flowCoordinator를
            // Field 재방문 시 재생성하지 않아야 이전 상행의 구독이 쌓이지 않는다
            // (Docs/설계/04_Field씬_아키텍처.md §5.2). cameraController는 이번 Field 씬의 뷰 참조를
            // 담고 있어 매번 새로 만든다.
            flowCoordinator ??= new FieldEncounterFlowCoordinator();
            flowCoordinator.Bind(uiManager, sessionState, encounterManager, battleController, battleResultSource);
            var cameraController = new FieldCameraController(this, movementViewRoot, battleViewRoot);
            flowCoordinator.RebindViews(this, cameraController, warningView, resultPopupView);

            sessionState.Begin();
        }

        private void HandleProgressChanged(float progress)
        {
            gaugeView.SetProgress(progress);
        }

        private bool TryBind(SceneUIRoot sceneUIRoot)
        {
            if (!sceneUIRoot.TryGetElement<FieldProgressGaugeView>(FieldUIElementIds.ProgressGauge, out gaugeView))
            {
                WarnMissing(FieldUIElementIds.ProgressGauge);
                return false;
            }

            if (!sceneUIRoot.TryGetElement<Image>(FieldUIElementIds.Background, out var background))
            {
                WarnMissing(FieldUIElementIds.Background);
                return false;
            }

            if (backgroundSprite != null)
            {
                background.sprite = backgroundSprite;
            }

            if (!sceneUIRoot.TryGetElement<Button>(FieldUIElementIds.FormationButton, out formationButton))
            {
                WarnMissing(FieldUIElementIds.FormationButton);
                return false;
            }

            if (!sceneUIRoot.TryGetElement<FieldEncounterWarningView>(FieldUIElementIds.EncounterWarning, out warningView))
            {
                WarnMissing(FieldUIElementIds.EncounterWarning);
                return false;
            }

            if (!sceneUIRoot.TryGetElement<RectTransform>(FieldUIElementIds.MovementViewRoot, out movementViewRoot))
            {
                WarnMissing(FieldUIElementIds.MovementViewRoot);
                return false;
            }

            if (!sceneUIRoot.TryGetElement<RectTransform>(FieldUIElementIds.BattleViewRoot, out battleViewRoot))
            {
                WarnMissing(FieldUIElementIds.BattleViewRoot);
                return false;
            }

            if (!sceneUIRoot.TryGetElement<FieldResultPopupView>(FieldUIElementIds.ResultPopup, out resultPopupView))
            {
                WarnMissing(FieldUIElementIds.ResultPopup);
                return false;
            }

            return true;
        }

        private static void WarnMissing(string id)
        {
            Debug.LogWarning($"Field UI에서 '{id}' 요소를 찾을 수 없다. {nameof(UIElementMarker)}가 부착되어 있는지 확인하라.");
        }
    }
}
