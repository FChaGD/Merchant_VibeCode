using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// Field 씬의 이동 뷰(진행 게이지, 배경, 정비창 재호출)를 조율한다. 인카운터 발생 시 경고창 점멸,
    /// 전투 뷰 전환, 결과 팝업 처리는 FieldEncounterFlowCoordinator에 위임한다(SRP) - 도착 처리
    /// (게이지 100%↔OnArrived)만은 인카운터/전투와 무관한 단순 이벤트→뷰 반영이라 이 클래스가 직접
    /// 처리한다(Docs/설계/04_Field씬_아키텍처.md §5.3).
    /// </summary>
    public class FieldUIController : MonoBehaviour, IFieldUIController
    {
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private BattleCharacterUnitView battleCharacterViewPrefab;
        [SerializeField] private BattleProtectedUnitView battleProtectedViewPrefab;

        /// <summary>
        /// Hub↔Field 씬 전환 연출(SceneTransitionEffectController)이 슬라이드시킬 대상. Field는 이번
        /// 연출 전용 요소를 새로 만들지 않고 기존 이동 뷰 루트를 그대로 재사용한다 - Hub↔Field 왕복
        /// 시점엔 항상 이동 뷰가 화면에 보이는 상태이기 때문이다(Docs/설계/10_씬전환_연출_아키텍처.md §8).
        /// </summary>
        public RectTransform MovementViewRoot => movementViewRoot;

        private FieldProgressGaugeView gaugeView;
        private Button formationButton;
        private Button tacticsButton;
        private FieldEncounterWarningView warningView;
        private RectTransform movementViewRoot;
        private RectTransform battleViewRoot;
        private RectTransform battleAllyLayer;
        private RectTransform battleEnemyLayer;
        private BattleFieldCameraView battleCameraView;
        private FieldResultPopupView resultPopupView;
        private FieldTransitionCurtainView transitionCurtain;
        private FieldEncounterFlowCoordinator flowCoordinator;
        private BattleViewPresenter viewPresenter;
        private IGameManager gameManager;
        private ISessionState sessionState;
        private IUIManager uiManager;

        public void RegisterFieldUI(IUIManager uiManager, ISessionState sessionState, IEncounterManager encounterManager, IBattleController battleController, IBattleResultSource battleResultSource, IDefeatConsequenceSource defeatConsequenceSource, IBattleSimulationEvents battleSimulationEvents, IGameManager gameManager, ISceneRevealSignal sceneRevealSignal)
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

            this.gameManager = gameManager;
            this.sessionState = sessionState;
            this.uiManager = uiManager;

            formationButton.onClick.RemoveAllListeners();
            formationButton.onClick.AddListener(() => uiManager.Open(UIPanelIds.Formation));

            tacticsButton.onClick.RemoveAllListeners();
            tacticsButton.onClick.AddListener(() => uiManager.Open(UIPanelIds.Tactics));

            // 화면이 완전히 드러나기 전까지는 정비창/방향성 지시 재호출을 막는다(사용자 확정) -
            // HandleSceneRevealed에서 다시 켠다. 전환 없이 로드된 경우(최초 진입 등)엔 사실상 바로 다시 켜진다.
            formationButton.interactable = false;
            tacticsButton.interactable = false;
            sceneRevealSignal.SceneRevealed -= HandleSceneRevealed;
            sceneRevealSignal.SceneRevealed += HandleSceneRevealed;

            // Field 씬은 상행마다 다시 로드되지만 sessionState(SessionStateTracker)는 Bootstrap에 상주하는
            // 영속 객체다 - 재구독 전 항상 먼저 해제해 상행을 반복할수록 구독이 누적되는 것을 막는다
            // (Docs/설계/04_Field씬_아키텍처.md §5 이벤트 구독 수명주기 참고).
            sessionState.OnProgressChanged -= HandleProgressChanged;
            sessionState.OnProgressChanged += HandleProgressChanged;
            sessionState.OnArrived -= HandleArrived;
            sessionState.OnArrived += HandleArrived;

            // encounterManager/battleResultSource는 Bootstrap 상주 영속 객체다 - flowCoordinator를
            // Field 재방문 시 재생성하지 않아야 이전 상행의 구독이 쌓이지 않는다
            // (Docs/설계/04_Field씬_아키텍처.md §5.2). cameraController는 이번 Field 씬의 뷰 참조를
            // 담고 있어 매번 새로 만든다.
            flowCoordinator ??= new FieldEncounterFlowCoordinator();
            flowCoordinator.Bind(uiManager, sessionState, encounterManager, battleController, battleResultSource, defeatConsequenceSource, gameManager);
            var cameraController = new FieldCameraController(this, movementViewRoot, battleViewRoot, transitionCurtain);
            flowCoordinator.RebindViews(this, cameraController, warningView, resultPopupView, transitionCurtain);

            // battleSimulationEvents도 Bootstrap 상주 영속 객체(BattleManager)라 같은 이유로
            // viewPresenter를 재생성하지 않는다 - Bind(이벤트 구독)는 최초 1회, RebindViews(이번 씬의
            // 유닛 레이어/프리팹 참조)는 Field 씬을 로드할 때마다 실행한다.
            viewPresenter ??= new BattleViewPresenter();
            viewPresenter.Bind(battleSimulationEvents);
            viewPresenter.RebindViews(battleAllyLayer, battleEnemyLayer, battleCharacterViewPrefab, battleProtectedViewPrefab, battleCameraView);

            // sessionState.Begin()은 여기서 바로 부르지 않는다 - 화면이 완전히 드러난 뒤(HandleSceneRevealed)에
            // 시작해야 "전투 시작" 준비(=상행 진행 시작)가 페이드 아웃 완료 이후로 미뤄진다(사용자 확정).
        }

        private void HandleSceneRevealed(ContentSceneId sceneId)
        {
            if (sceneId != ContentSceneId.Field)
            {
                return;
            }

            formationButton.interactable = true;
            tacticsButton.interactable = true;
            sessionState.Begin();
        }

        private void HandleProgressChanged(float progress)
        {
            gaugeView.SetProgress(progress);
        }

        // 도착 성공 처리(Docs/설계/04_Field씬_아키텍처.md §5.3) - 전투 승/패와 같은 resultPopupView를
        // 재사용한다(문구·버튼 라벨·콜백만 다름).
        private void HandleArrived()
        {
            // 인카운터 발생 시 FieldEncounterFlowCoordinator.HandleEncounterTriggered가 배치/방향성
            // 지시를 닫는 것과 같은 이유 - 열려있는 채로 도착 팝업이 뜨면 그 위로 안 닫힌 패널이 남는다.
            // 둘 다 Close가 멱등이라 열려있지 않아도 안전하다.
            uiManager.Close(UIPanelIds.Formation);
            uiManager.Close(UIPanelIds.Tactics);
            resultPopupView.Show("도착 성공", "도시 입장", onConfirm: () => gameManager.RequestSceneTransition(ContentSceneId.Hub));
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

            if (!sceneUIRoot.TryGetElement<Button>(FieldUIElementIds.TacticsButton, out tacticsButton))
            {
                WarnMissing(FieldUIElementIds.TacticsButton);
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

            // BattleFieldCameraView는 BattleViewRoot와 같은 GameObject에 부착된다(UIElementMarker는
            // GameObject당 하나의 id만 가질 수 있어 별도 마커를 만들 수 없다) - 방금 구한 RectTransform과
            // 같은 오브젝트에서 바로 GetComponent로 조회한다.
            battleCameraView = battleViewRoot.GetComponent<BattleFieldCameraView>();
            if (battleCameraView == null)
            {
                WarnMissing(FieldUIElementIds.BattleViewRoot + " (BattleFieldCameraView)");
                return false;
            }

            if (!sceneUIRoot.TryGetElement<RectTransform>(FieldUIElementIds.BattleAllyLayer, out battleAllyLayer))
            {
                WarnMissing(FieldUIElementIds.BattleAllyLayer);
                return false;
            }

            if (!sceneUIRoot.TryGetElement<RectTransform>(FieldUIElementIds.BattleEnemyLayer, out battleEnemyLayer))
            {
                WarnMissing(FieldUIElementIds.BattleEnemyLayer);
                return false;
            }

            if (!sceneUIRoot.TryGetElement<FieldResultPopupView>(FieldUIElementIds.ResultPopup, out resultPopupView))
            {
                WarnMissing(FieldUIElementIds.ResultPopup);
                return false;
            }

            if (!sceneUIRoot.TryGetElement<FieldTransitionCurtainView>(FieldUIElementIds.TransitionCurtain, out transitionCurtain))
            {
                WarnMissing(FieldUIElementIds.TransitionCurtain);
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
