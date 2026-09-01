using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 배틀 테스트 씬(독립 단일 씬) 전체를 조율한다. Field 씬의 FieldUIController/
    /// FieldEncounterFlowCoordinator에 해당하지만, 이동 뷰·씬 전환 연출·인카운터 판정이 전혀 없는
    /// 단순한 상황이라 하나의 클래스로 충분하다. 배치(정비창)는 이 씬에서 쓰지 않는다 - 아군도 적과
    /// 같은 자유 드래그 팔레트(BattleTestUnitPaletteView)로 배치하므로 등록할 IFormationPanel이
    /// 없다. 방향성 지시(전술)만은 실제 TacticsPanel을 그대로 재사용한다.
    /// 방향성 지시 패널 등록(RegisterTacticsUI)은 SceneUIRoot.Awake()가 채우는 elementsById에
    /// 의존하는데, DependencyManager.Awake()가 다른 GameObject의 Awake보다 먼저 실행될 수도 있어
    /// (Unity는 서로 다른 루트 오브젝트 간 Awake 순서를 보장하지 않는다) 그 시점(ResolveDependencies)에는
    /// 아직 비어있을 위험이 있다 - 실제 등록/버튼 배선은 모든 Awake가 끝난 뒤 보장되는 Start()로 미룬다.
    /// </summary>
    public class BattleTestController : MonoBehaviour, IManagedComponent
    {
        [SerializeField] private Button startBattleButton;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button tacticsButton;
        [SerializeField] private Transform allyContainer;
        [SerializeField] private Transform enemyContainer;
        [SerializeField] private BattleCharacterUnitView characterViewPrefab;
        [SerializeField] private BattleProtectedUnitView protectedViewPrefab;
        [SerializeField] private BattleFieldWorldCameraView cameraView;
        [SerializeField] private BattleBackgroundGridView backgroundView;
        [SerializeField] private BattleTestUnitPaletteView paletteView;
        [SerializeField] private BattleTestResultPopupView resultPopupView;
        [SerializeField] private BattleTestUnitInfoPanelView unitInfoPanelView;
        [SerializeField] private BattleTestUnitPickerView unitPickerView;
        [SerializeField] private BattleTestSpawnPointPanelView spawnPointPanelView;
        // 다른 GameObject(BattleManager)에 있는 컴포넌트라 GetComponent로 못 찾는다 - 인스톨러가
        // 직접 연결한다(ManagerHierarchyInstaller의 WireFieldBattleViewPrefabs와 같은 패턴).
        [SerializeField] private BattleTestSimulationRule battleTestSimulation;

        // 요구사항: 기준 줌의 3배까지 넓게, 1/2배(orthographicSize 절반)까지 확대. Field 씬은
        // BattleFieldWorldCameraView.ConfigureZoomRange를 호출하지 않아 기존 동작 그대로다.
        private const float CameraZoomInRatio = 2f;
        private const float CameraZoomOutRatio = 3f;

        private readonly BattleViewPresenter viewPresenter = new();

        private IDependencyRegistrar registrar;
        private IBattleController battleController;
        private IUIManager uiManager;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            // 이 씬 안에서만 쓰이는 조율자라 등록할 인터페이스가 없다.
        }

        public void ResolveDependencies(IDependencyRegistrar registrar)
        {
            this.registrar = registrar;

            battleController = registrar.Resolve<IBattleController>();
            var simulationEvents = registrar.Resolve<IBattleSimulationEvents>();
            viewPresenter.Bind(simulationEvents);
            viewPresenter.RebindViews(allyContainer, enemyContainer, characterViewPrefab, protectedViewPrefab, cameraView, backgroundView);

            registrar.Resolve<IBattleResultSource>().OnBattleEnded += HandleBattleEnded;
        }

        private void Start()
        {
            uiManager = registrar.Resolve<IUIManager>();
            var tacticsRepository = registrar.Resolve<ITacticsRepository>();

            var tacticsPanel = GetComponent<ITacticsPanel>();
            var panelRegistrar = GetComponent<IPanelRegistrar>();
            tacticsPanel.RegisterTacticsUI(tacticsRepository, uiManager, gameObject.scene.name);
            panelRegistrar.RegisterPanel(tacticsPanel);

            tacticsButton.onClick.AddListener(() => uiManager.Open(UIPanelIds.Tactics));
            startBattleButton.onClick.AddListener(HandleStartClicked);
            pauseButton.onClick.AddListener(() => battleTestSimulation.Pause());
            resetButton.onClick.AddListener(() => battleTestSimulation.ResetToSetup());

            paletteView.Bind(battleTestSimulation);
            unitPickerView.Bind(HandleUnitClicked);
            unitPickerView.BindSpawnPointHandler(spawnPointPanelView.Show);
            battleTestSimulation.OnReset += HandleSimulationReset;
            battleTestSimulation.OnUnitAdded += HandleUnitAdded;
            battleTestSimulation.OnEnemyRosterCleared += HandleEnemyRosterCleared;

            // 세팅 단계(전투 시작 전)에도 카메라 팬/줌이 동작해야 하므로, 첫 전투가 시작되기 전에
            // 미리 한 번 경계를 잡아둔다(안 하면 RecomputeBounds가 한 번도 안 불려 팬/줌이 원점에
            // 고정된다) - 실제 전투가 시작되면 BattleViewPresenter.Present()가 그 시점의 fieldRadius로
            // 다시 정확하게 잡아준다. clampToField:false - 요구사항: 이 씬은 전장 정사각형 밖으로도
            // 자유롭게 드래그팬할 수 있어야 한다(Field 씬은 이 메서드를 안 불러 기존 제약 그대로).
            cameraView.ConfigureZoomRange(CameraZoomInRatio, CameraZoomOutRatio, clampToField: false);
            var columnCount = battleTestSimulation.FieldLayout.ColumnCount;
            cameraView.ConfigureFieldBounds(battleTestSimulation.FieldLayout.ComputeFieldRadius(columnCount));

            // 요구사항: 바닥 배경 타일이 전투 시작 전(세팅 단계)부터 보여야 한다 - 원래
            // BattleBackgroundGridView.ConfigureField는 BattleViewPresenter.Present()(전투 시작 시)에만
            // 호출돼, 그전까지는 타일 풀 자체가 비어 있었다(비활성화가 아니라 아예 생성 전).
            backgroundView.ConfigureField(battleTestSimulation.FieldLayout.ComputeSpawnRadius(columnCount));
        }

        // BattleViewPresenter는 OnSimulationBuilt(Evaluate() 1회)에만 반응해 유닛 뷰를 스폰하므로,
        // 팔레트로 추가된 유닛(세팅 단계 미리보기 포함)은 이 컨트롤러가 직접 뷰를 만들어줘야 눈에
        // 보인다 - BattleViewPresenter.SpawnCharacterView와 같은 절차(Instantiate+Bind)를 그대로 쓴다.
        // 세팅 단계(!IsRunning)인 유닛에만 클릭 대상(BattleTestUnitClickTarget)을 붙인다 - 전투 중
        // 실시간 추가 유닛은 이미 싸우고 있어 "배치 취소"/수치 조절 대상이 아니다.
        private void HandleUnitAdded(IBattleCombatant unit, bool isAlly, int entryId)
        {
            var parent = isAlly ? allyContainer : enemyContainer;
            if (parent == null || characterViewPrefab == null)
            {
                Debug.LogWarning($"{nameof(BattleTestController)}: allyContainer/enemyContainer 또는 characterViewPrefab이 비어 있어 유닛 뷰를 만들지 못했다(인스톨러 배선 확인).");
                return;
            }

            var view = Instantiate(characterViewPrefab, parent);
            view.Bind(unit);

            if (!battleTestSimulation.IsRunning)
            {
                var clickTarget = view.gameObject.AddComponent<BattleTestUnitClickTarget>();
                clickTarget.Initialize(isAlly, entryId);
            }
        }

        private void HandleUnitClicked(BattleTestUnitClickTarget target) => unitInfoPanelView.Show(target);

        // 이미 진행 중(일시정지 포함)이면 재개만 하고 다시 짓지 않는다 - 그렇지 않으면 "중지" 뒤
        // "시작"을 누를 때마다 매번 새 전투로 재시작돼 버린다. 요구사항은 시작/중지/리셋 3버튼만
        // 요구하므로 "재개"는 시작 버튼이 겸한다. 다만 승패가 이미 갈린 뒤(IsFinished)라면 재개할
        // 대상이 없으므로 리셋 없이도 새로 시작한다.
        // BattleViewPresenter.Present()가 내부 Clear()로 정리하는 대상은 "자신이 직접 스폰한 뷰
        // 목록"뿐이다 - 세팅 단계 미리보기 뷰는 HandleUnitAdded가 별도로 Instantiate했으므로 그
        // 목록에 없어 지워지지 않는다(실전투 확인된 버그). 그래서 새 전투를 실제로 짓기 직전에
        // ClearSpawnedViews()로 컨테이너를 통째로 비운다 - Present()가 그 뒤에 실제 시뮬레이션
        // 유닛 뷰를 새로 스폰한다.
        private void HandleStartClicked()
        {
            resultPopupView.Hide();
            unitInfoPanelView.Hide();
            spawnPointPanelView.Hide();
            uiManager.Close(UIPanelIds.Tactics);

            if (!battleTestSimulation.IsRunning || battleTestSimulation.IsFinished)
            {
                ClearSpawnedViews();
                battleController.StartBattle();
            }
            battleController.ResumeSimulation();
        }

        private void HandleBattleEnded(BattleResult result) => resultPopupView.Show(result);

        private void HandleSimulationReset()
        {
            resultPopupView.Hide();
            unitInfoPanelView.Hide();
            spawnPointPanelView.Hide();
            ClearSpawnedViews();
        }

        // 적 구성 편집 패널의 "적용"이 로스터를 통째로 다시 채우기 직전에 발행된다 - 아군 미리보기는
        // 그대로 두고 적 쪽만 비운다(OnReset과 달리 전체를 지우면 안 된다).
        private void HandleEnemyRosterCleared()
        {
            unitInfoPanelView.Hide(); // 지워질 적 유닛을 패널이 참조 중이었을 수 있다.
            for (var i = enemyContainer.childCount - 1; i >= 0; i--) Destroy(enemyContainer.GetChild(i).gameObject);
        }

        // BattleViewPresenter.Clear()는 private이라(FieldUIController 전용 설계, 프로덕션 파일 변경
        // 범위 밖) 스폰된 유닛 뷰는 여기서 직접 정리한다 - 리셋은 다음 전투 시작을 기다리지 않고
        // 즉시 지워야 한다.
        private void ClearSpawnedViews()
        {
            for (var i = allyContainer.childCount - 1; i >= 0; i--) Destroy(allyContainer.GetChild(i).gameObject);
            for (var i = enemyContainer.childCount - 1; i >= 0; i--) Destroy(enemyContainer.GetChild(i).gameObject);
        }
    }
}
