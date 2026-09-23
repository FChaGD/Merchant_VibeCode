using System.Linq;
using Game.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.Editor
{
    /// <summary>
    /// 매니저 하이어라키(DependencyManager, SceneLoader, 배치 UI 팔레트용 임시 로스터 제공자 포함)를
    /// 코드로 생성/동기화한다. 씬 YAML 수작업 편집 대신 이 도구로 재현 가능하게 만든다. 이미 존재하는
    /// 오브젝트는 재사용하며 DependencyManager의 managedComponents 목록만 항상 최신 상태로 재동기화한다.
    /// 조립 로직은 도메인별 Build/Wire 메서드로 나뉘어 있다 - 예전엔 전부 BuildManagerHierarchy() 하나에
    /// 인라인으로 들어있어 "책임성 분산 + 비직관적 구조"로 지적됐다(리팩토링 점검 2026-09-08 §2-2).
    /// </summary>
    public static class ManagerHierarchyInstaller
    {
        private const string RootName = "Managers";

        // 저장소 8개(BuildUIManagerDataRepositories)의 GameObject 이름 - 재실행 시 root에 남은 옛
        // 위치(과거엔 평면으로 root 밑에 있었다)의 동명 오브젝트를 정리하는 데도 재사용한다.
        private static readonly string[] DataRepositoryNames =
        {
            nameof(PlaceholderCaravanRosterProvider),
            nameof(InMemoryFormationRepository),
            nameof(InMemoryUnitConditionRepository),
            nameof(InMemoryTacticsRepository),
            nameof(PlaceholderTripInfoProvider),
            nameof(InMemoryTripCurrentLocationRepository),
            nameof(TripDestinationAssigner),
            nameof(InMemoryFieldFormationActivityRepository),
            nameof(InMemoryPlayerCurrencyWallet),
            nameof(PlaceholderTradeGoodsInventoryRepository),
            nameof(PlaceholderEquipmentInventoryRepository),
            nameof(PlaceholderConsumableInventoryRepository),
            nameof(PlaceholderPersonalItemInventoryRepository),
        };

        [MenuItem("Tools/Game/Build Bootstrap Scene")]
        public static void BuildManagerHierarchy()
        {
            var root = EditorUIBuilder.GetOrCreateSceneRoot(EditorSceneManager.GetActiveScene(), RootName);

            var (dependencyManager, gameManager, inputManager, uiManager, sceneTransitionEffectController, battleManager, aiManager, encounterManager)
                = BuildCoreManagers(root);

            var sceneLoader = WireGameManagerDependents(gameManager);
            WireBattleManagerDependents(battleManager);
            WireUIManagerPanels(uiManager);
            var dataRepositories = BuildUIManagerDataRepositories(root, uiManager);

            // 전투 디버그 기즈모(포위망/방진선/이동 목적지)는 여기서 설치하지 않는다 - "게임 빌드"와
            // "디버그 도구 켜고 끄기"는 다른 관심사라 DebugBootstrapReentryGuard와 같은 자리
            // (Tools/Game/Debug/, BattleGizmoInstaller.cs)에서 별도 Install/Remove 메뉴로 관리한다.

            var coreManagers = new MonoBehaviour[]
            {
                gameManager,
                inputManager,
                uiManager,
                // uiManager 바로 뒤에 둔다 - 둘 다 ISceneLoader.OnSceneLoaded를 구독하는데, 새 씬의
                // Wire(...)가 먼저 끝난 뒤에야 이 컨트롤러가 커튼을 페이드 아웃해야 한다. 구독 순서는
                // ResolveDependencies 호출 순서(=이 배열 순서)를 따르므로, 이 순서를 바꾸면 안 된다
                // (Docs/설계/10-2026-08-26-씬전환_연출_아키텍처.md §6/§10).
                sceneTransitionEffectController,
                battleManager,
                aiManager,
                encounterManager,
                sceneLoader,
            };

            // dataRepositories(저장소 8개)는 서로 순서 의존성이 없다(가독성 목적으로만 원래 순서
            // 유지 - RegisterSelf 전부 끝난 뒤 ResolveDependencies를 호출하는 2단계 구조라 실제
            // 순서 의존성은 없다) - coreManagers와 분리된 배열로 두어 "순서에 민감한 부분"과
            // "무관한 부분"의 경계를 코드 구조 자체로 드러낸다.
            SyncManagedComponents(dependencyManager, coreManagers.Concat(dataRepositories).ToArray());

            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("매니저 하이어라키 생성/동기화 완료. 씬을 저장(Ctrl+S)해야 변경사항이 파일에 반영된다.");
        }

        private static (DependencyManager dependencyManager, GameManager gameManager, InputManager inputManager,
            UIManager uiManager, SceneTransitionEffectController sceneTransitionEffectController,
            BattleManager battleManager, AIManager aiManager, EncounterManager encounterManager) BuildCoreManagers(GameObject root)
        {
            // 한때 Bootstrap에 EventSystem을 영속시켜봤으나 콘텐츠 씬(Hub/Field)의 그리드 드래그가
            // 깨지는 회귀가 있어 되돌렸다 - 콘텐츠 씬마다 자기 EventSystem을 갖는 원래 구조로 복귀
            // (EditorUIBuilder.EnsureSceneEventSystem/SceneLoader 참고). 이전에 여기 만들어졌을 수 있는
            // 잔재를 정리한다(재실행 안전성).
            EditorUIBuilder.DestroyChildIfExists(root.transform, "EventSystem");

            // 리팩토링 과정에서 컴포넌트 스크립트 파일 자체를 지운 적이 있다(예: BattleResultEvaluator) -
            // 씬에 이미 저장돼 있던 해당 컴포넌트 참조는 삭제된 타입이라 GetComponent<T>()로 찾아
            // 제거할 방법이 없다("Missing Script" 경고로 남는다). 재실행할 때마다 이 하이어라키 전체를
            // 훑어 없어진 스크립트 참조를 걷어낸다.
            EditorUIBuilder.RemoveMissingScriptsRecursively(root.transform);

            var dependencyManager = EditorUIBuilder.GetOrCreateManager<DependencyManager>(root.transform, nameof(DependencyManager));
            var gameManager = EditorUIBuilder.GetOrCreateManager<GameManager>(root.transform, nameof(GameManager));
            var inputManager = EditorUIBuilder.GetOrCreateManager<InputManager>(root.transform, nameof(InputManager));
            var uiManager = EditorUIBuilder.GetOrCreateManager<UIManager>(root.transform, nameof(UIManager));
            var sceneTransitionEffectController = EditorUIBuilder.GetOrCreateManager<SceneTransitionEffectController>(root.transform, nameof(SceneTransitionEffectController));
            EnsureSceneTransitionCurtain(root.transform, sceneTransitionEffectController);
            var battleManager = EditorUIBuilder.GetOrCreateManager<BattleManager>(root.transform, nameof(BattleManager));
            var aiManager = EditorUIBuilder.GetOrCreateManager<AIManager>(root.transform, nameof(AIManager));
            var encounterManager = EditorUIBuilder.GetOrCreateManager<EncounterManager>(root.transform, nameof(EncounterManager));

            return (dependencyManager, gameManager, inputManager, uiManager, sceneTransitionEffectController, battleManager, aiManager, encounterManager);
        }

        // SceneLoader는 GameManager와 같은 GameObject에 부착하되, 자체적으로 DI에 등록되는 독립된
        // 관리 대상이므로 반환해 managedComponents 동기화 목록에도 포함시킨다. SessionStateTracker는
        // GameManager 산하 컴포넌트라 전역 DI 대상이 아니다(GameManager가 RegisterSelf에서 직접
        // 조회해 ISessionState/ISessionPauseControl로 등록) - 그래서 반환하지 않는다.
        private static SceneLoader WireGameManagerDependents(GameManager gameManager)
        {
            var sceneLoader = EditorUIBuilder.GetOrAddComponent<SceneLoader>(gameManager.gameObject);
            EditorUIBuilder.GetOrAddComponent<SessionStateTracker>(gameManager.gameObject);
            return sceneLoader;
        }

        // LiveBattleSimulationRule/PlaceholderDefeatConsequenceRule도 BattleManager 산하 컴포넌트라
        // 전역 DI 대상이 아니다(BattleManager가 GetComponent<IBattleResultRule>()/
        // GetComponent<IDefeatConsequenceRule>()로 직접 조회) - 그래서 반환값이 없다.
        private static void WireBattleManagerDependents(BattleManager battleManager)
        {
            var liveBattleSimulationRule = EditorUIBuilder.GetOrAddComponent<LiveBattleSimulationRule>(battleManager.gameObject);
            WireTableAssets(liveBattleSimulationRule);
            EditorUIBuilder.GetOrAddComponent<PlaceholderDefeatConsequenceRule>(battleManager.gameObject);
        }

        // HubUIController/HubFormationPanel/FieldFormationPanel/TripPanel/TacticsPanel/FieldUIController/
        // HubUIWiring/FieldUIWiring은 전역 매니저가 아니라 UIManager 산하 컴포넌트이므로 같은
        // GameObject에 부착한다. UIManager가 GetComponents<IContentSceneUIWiring>()로 Wiring류를
        // 스스로 수집하므로 여기선 부착만 하면 된다.
        private static void WireUIManagerPanels(UIManager uiManager)
        {
            // HubFormationPanel/FieldFormationPanel(구 FormationPanel, Docs/설계/25번 §2.3 Hub/Field
            // 분리) 등으로 이름이 바뀌며 남은 옛 컴포넌트 슬롯을 정리한다 - 스크립트가 삭제됐어도
            // GameObject엔 "Missing Script" 슬롯이 남아있을 수 있다.
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(uiManager.gameObject);

            EditorUIBuilder.GetOrAddComponent<HubUIController>(uiManager.gameObject);
            EditorUIBuilder.GetOrAddComponent<PlayerCurrencyHudController>(uiManager.gameObject);

            // 인벤토리 팝업 4종의 동시 중첩 오픈을 담당하는 형제 컴포넌트(Docs/설계/32번 §5) -
            // PanelNavigationStack과 별개로 동작해야 해서 panelsById가 아니라 이쪽에 등록된다.
            EditorUIBuilder.GetOrAddComponent<InventoryPopupCoordinator>(uiManager.gameObject);
            EditorUIBuilder.GetOrAddComponent<HubFormationPanel>(uiManager.gameObject);
            EditorUIBuilder.GetOrAddComponent<FieldFormationPanel>(uiManager.gameObject);
            EditorUIBuilder.GetOrAddComponent<TripPanel>(uiManager.gameObject);
            var tacticsPanel = EditorUIBuilder.GetOrAddComponent<TacticsPanel>(uiManager.gameObject);
            WirePartyPolicyCatalog(tacticsPanel);
            WireRoleGroupCatalog(tacticsPanel);
            WireTacticsStringTables(tacticsPanel);

            // 전투 뷰 유닛 프리팹은 EditorUIBuilder(공용 조립 로직)가 자산으로 만들어 두고, 여기
            // (Bootstrap 담당)서는 그 자산을 FieldUIController 필드에 연결만 한다.
            var fieldUIController = EditorUIBuilder.GetOrAddComponent<FieldUIController>(uiManager.gameObject);
            WireFieldBattleViewPrefabs(fieldUIController);

            EditorUIBuilder.GetOrAddComponent<HubUIWiring>(uiManager.gameObject);
            EditorUIBuilder.GetOrAddComponent<FieldUIWiring>(uiManager.gameObject);
        }

        // 상행 관리/전투 데이터 시스템이 아직 없어(Placeholder 패턴), 그 자리를 메우는 임시/인메모리
        // 저장소 8개를 UIManager 산하 자식으로 생성한다. 실제 주 소비자가 전부 UIManager 산하 UI
        // (팔레트/패널)라는 게 각 클래스 최초 도입 주석에 명시돼 있어(BattleManager는 전투 판정 시점
        // 읽기만 하는 2차 소비자) 여기로 옮겼다 - 예전엔 root(Managers) 바로 밑에 매니저들과 동급으로
        // 평면 나열돼 있어 "누구 소관인지 알 수 없는 구조"로 지적됐다(리팩토링 점검 2026-09-08).
        private static MonoBehaviour[] BuildUIManagerDataRepositories(GameObject root, UIManager uiManager)
        {
            // 재실행 안전성: GetOrCreateManager는 이름 기준으로 부모의 "직계 자식"만 찾는다. 옛 씬(저장소가
            // root 평면에 있는 상태)에서 재실행하면 uiManager 밑에서는 못 찾아 새로 만들어버려 root에
            // 중복이 남는다 - 새로 만들기 전에 root에 남은 동명 오브젝트를 먼저 지운다.
            foreach (var name in DataRepositoryNames)
            {
                EditorUIBuilder.DestroyChildIfExists(root.transform, name);
            }

            // 배치 UI의 "적용" 버튼이 반영할 대상 - 현재 플레이 세션 동안만 유지되는 인메모리 저장소.
            var placeholderRosterProvider = EditorUIBuilder.GetOrCreateManager<PlaceholderCaravanRosterProvider>(uiManager.transform, nameof(PlaceholderCaravanRosterProvider));
            WirePlaceholderRosterIcons(placeholderRosterProvider);

            var formationRepository = EditorUIBuilder.GetOrCreateManager<InMemoryFormationRepository>(uiManager.transform, nameof(InMemoryFormationRepository));

            // 상행 동안 보유 유닛 HP/사망 상태를 저장하는 대상(설계 15번) - formationRepository와 같은
            // 성격의 인메모리 저장소.
            var unitConditionRepository = EditorUIBuilder.GetOrCreateManager<InMemoryUnitConditionRepository>(uiManager.transform, nameof(InMemoryUnitConditionRepository));
            WireTableAssets(unitConditionRepository);

            // 방향성 지시 UI(TacticsPanel)가 반영할 대상 - 배치와 같은 성격의 인메모리 저장소.
            var tacticsRepository = EditorUIBuilder.GetOrCreateManager<InMemoryTacticsRepository>(uiManager.transform, nameof(InMemoryTacticsRepository));
            WirePartyPolicyCatalog(tacticsRepository);
            WireRoleGroupCatalog(tacticsRepository);

            // 지역 시스템이 아직 없어, 상행 준비 UI 테스트용 임시 상행 요약 제공자를 등록한다.
            // 실제 데이터 시스템이 생기면 이 저장소를 함께 제거한다.
            var placeholderTripInfoProvider = EditorUIBuilder.GetOrCreateManager<PlaceholderTripInfoProvider>(uiManager.transform, nameof(PlaceholderTripInfoProvider));

            // "현재 위치"/도착지 지정(기획 16번, 설계 21번) - formationRepository와 같은 성격의 인메모리
            // 저장소.
            var tripCurrentLocationRepository = EditorUIBuilder.GetOrCreateManager<InMemoryTripCurrentLocationRepository>(uiManager.transform, nameof(InMemoryTripCurrentLocationRepository));
            var tripDestinationAssigner = EditorUIBuilder.GetOrCreateManager<TripDestinationAssigner>(uiManager.transform, nameof(TripDestinationAssigner));

            // 상행 중(Field) 정비창 배치/이동 소요시간 진행 상태(기획 20번, 설계 25번) - formationRepository와
            // 같은 성격의 인메모리 저장소.
            var fieldFormationActivityRepository = EditorUIBuilder.GetOrCreateManager<InMemoryFieldFormationActivityRepository>(uiManager.transform, nameof(InMemoryFieldFormationActivityRepository));

            // 재화 지갑(기획 23/26번, 설계 29번) - 기본 소지 재화만 관리, formationRepository와 같은
            // 성격의 인메모리 저장소.
            var playerCurrencyWallet = EditorUIBuilder.GetOrCreateManager<InMemoryPlayerCurrencyWallet>(uiManager.transform, nameof(InMemoryPlayerCurrencyWallet));

            // 인벤토리 카테고리 4종(기획 25/31번, 설계 32번) - 장비/소모품/개인물품은 고정 크기,
            // 교역품/전리품은 ResolveDependencies 시점에 마차 재고 수로 그리드 크기를 계산한다
            // (placeholderRosterProvider보다 ResolveDependencies 호출이 늦어도 되므로 순서 무관 -
            // 실제 순서 의존성은 없다는 이 메서드 상단 주석과 동일).
            var tradeGoodsInventoryRepository = EditorUIBuilder.GetOrCreateManager<PlaceholderTradeGoodsInventoryRepository>(uiManager.transform, nameof(PlaceholderTradeGoodsInventoryRepository));
            var equipmentInventoryRepository = EditorUIBuilder.GetOrCreateManager<PlaceholderEquipmentInventoryRepository>(uiManager.transform, nameof(PlaceholderEquipmentInventoryRepository));
            var consumableInventoryRepository = EditorUIBuilder.GetOrCreateManager<PlaceholderConsumableInventoryRepository>(uiManager.transform, nameof(PlaceholderConsumableInventoryRepository));
            var personalItemInventoryRepository = EditorUIBuilder.GetOrCreateManager<PlaceholderPersonalItemInventoryRepository>(uiManager.transform, nameof(PlaceholderPersonalItemInventoryRepository));

            return new MonoBehaviour[]
            {
                placeholderRosterProvider,
                formationRepository,
                unitConditionRepository,
                tacticsRepository,
                placeholderTripInfoProvider,
                tripCurrentLocationRepository,
                tripDestinationAssigner,
                fieldFormationActivityRepository,
                playerCurrencyWallet,
                tradeGoodsInventoryRepository,
                equipmentInventoryRepository,
                consumableInventoryRepository,
                personalItemInventoryRepository,
            };
        }

        // Hub↔Field 씬 전환 연출용 커튼은 Bootstrap(영속) 스코프여야 한다 - 콘텐츠 씬 스코프 오브젝트는
        // 그 씬이 언로드되는 순간 함께 파괴되기 때문이다(Docs/설계/10-2026-08-26-씬전환_연출_아키텍처.md §5).
        // 스케일 모드는 Hub.unity/Field.unity 실측(uiScaleMode=0=ConstantPixelSize, scaleFactor=1)과
        // 반드시 일치해야 한다(2026-09-08 실전 확인 - Docs/Refactor/2026-09-08_Hub.md §2-3/§3 수정 K 정정).
        // ScaleWithScreenSize + referenceResolution 값을 콘텐츠 씬과 맞추는 걸로는 부족하다 -
        // SceneTransitionEffectController.PlayTransition이 contentRoot.rect.width(콘텐츠 캔버스의 1:1
        // 픽셀 단위 값)를 커튼의 anchoredPosition에 그대로 대입하는데, 커튼이 ScaleWithScreenSize라면
        // 그 값이 커튼 자신의 scaleFactor로 다시 배율되어(예: 1920x1080 화면에서 referenceResolution
        // 800x600이면 2.4배) 슬라이드 시작/도착 타이밍이 어긋나 커튼과 콘텐츠 사이에 빈 화면이 보이는
        // 회귀가 있었다(실전 확인). ConstantPixelSize로 맞추면 두 캔버스가 항상 "1 unit = 1 픽셀"로
        // 일치해 이 문제가 해상도와 무관하게 근본적으로 사라진다.
        private static void EnsureSceneTransitionCurtain(Transform managersRoot, SceneTransitionEffectController controller)
        {
            var canvas = EditorUIBuilder.GetOrCreateManager<Canvas>(managersRoot, "SceneTransitionCanvas");
            canvas.gameObject.layer = LayerMask.NameToLayer("UI");
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // Hub/Field 콘텐츠 씬 캔버스보다 항상 위에 그려지도록 충분히 높은 값

            var scaler = EditorUIBuilder.GetOrAddComponent<CanvasScaler>(canvas.gameObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            EditorUIBuilder.GetOrAddComponent<GraphicRaycaster>(canvas.gameObject);

            var curtainGo = EditorUIBuilder.GetOrCreateUIObject(canvas.transform, "Curtain");
            EditorUIBuilder.SetStretch(curtainGo.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureImage(curtainGo, Color.black);
            var canvasGroup = EditorUIBuilder.GetOrAddComponent<CanvasGroup>(curtainGo);
            var curtainView = EditorUIBuilder.GetOrAddComponent<SceneTransitionCurtainView>(curtainGo);
            curtainGo.SetActive(false); // 평상시엔 숨김 - SceneTransitionEffectController.PlayTransition이 Show()로 켠다

            var curtainSerialized = new SerializedObject(curtainView);
            curtainSerialized.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            curtainSerialized.ApplyModifiedProperties();

            var controllerSerialized = new SerializedObject(controller);
            controllerSerialized.FindProperty("curtain").objectReferenceValue = curtainView;
            controllerSerialized.ApplyModifiedProperties();
        }

        private static void WireFieldBattleViewPrefabs(FieldUIController fieldUIController)
        {
            var so = new SerializedObject(fieldUIController);
            so.FindProperty("battleCharacterViewPrefab").objectReferenceValue = EditorUIBuilder.GetOrCreateBattleCharacterViewPrefab();
            so.FindProperty("battleProtectedViewPrefab").objectReferenceValue = EditorUIBuilder.GetOrCreateBattleProtectedViewPrefab();
            so.FindProperty("battlePendingReinforcementViewPrefab").objectReferenceValue = EditorUIBuilder.GetOrCreateBattlePendingReinforcementViewPrefab();
            so.ApplyModifiedProperties();
        }

        private static void WirePlaceholderRosterIcons(PlaceholderCaravanRosterProvider provider)
        {
            var serializedProvider = new SerializedObject(provider);
            serializedProvider.FindProperty("warriorIcon").objectReferenceValue = FormationPlaceholderIcons.GetOrCreateSquare();
            serializedProvider.FindProperty("archerIcon").objectReferenceValue = FormationPlaceholderIcons.GetOrCreatePentagon();
            serializedProvider.FindProperty("shieldBearerIcon").objectReferenceValue = FormationPlaceholderIcons.GetOrCreateHexagon();
            serializedProvider.FindProperty("wagonIcon").objectReferenceValue = FormationPlaceholderIcons.GetOrCreateTriangle();
            serializedProvider.FindProperty("facilityIcon").objectReferenceValue = FormationPlaceholderIcons.GetOrCreateCircle();
            serializedProvider.ApplyModifiedProperties();
        }

        // roleGroupMap과 같은 배선 전례(Docs/설계/17번 §1/§6) - 엑셀 임포트 결과 테이블은 DI로
        // 등록하지 않고 소비자마다 SerializeField로 개별 배선한다. 에셋이 아직 임포트 전(Tools/Game/
        // Table/Import Character Stats 미실행)이라 없을 수 있으므로 null 배선을 경고 없이 허용한다 -
        // 그 상태로 실행하면 TableBattleUnitStatProvider가 조회 시점에 즉시 예외로 드러낸다.
        private static void WireTableAssets(LiveBattleSimulationRule rule)
        {
            var so = new SerializedObject(rule);
            so.FindProperty("characterStatsTable").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CharacterStatsTableAsset>(TableAssetPaths.CharacterStatsTable);
            so.FindProperty("enemyStatsTable").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EnemyStatsTableAsset>(TableAssetPaths.EnemyStatsTable);
            so.FindProperty("enemyEncounterCompositionTable").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EnemyEncounterCompositionTableAsset>(TableAssetPaths.EnemyEncounterCompositionTable);
            so.ApplyModifiedProperties();
        }

        private static void WireTableAssets(InMemoryUnitConditionRepository repository)
        {
            var so = new SerializedObject(repository);
            so.FindProperty("characterStatsTable").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CharacterStatsTableAsset>(TableAssetPaths.CharacterStatsTable);
            so.ApplyModifiedProperties();
        }

        // roleGroupMap/catalog와 달리(수동 인스펙터 연결 전례) 이 에셋은 처음 생기는 것이라 두
        // 소비자(TacticsPanel/InMemoryTacticsRepository) 모두 자동 배선한다(Docs/설계/17번 §10.5).
        private static void WirePartyPolicyCatalog(TacticsPanel panel)
        {
            var so = new SerializedObject(panel);
            so.FindProperty("partyPolicyCatalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PartyTacticsPolicyCatalogAsset>(TableAssetPaths.PartyPolicyCatalog);
            so.ApplyModifiedProperties();
        }

        private static void WirePartyPolicyCatalog(InMemoryTacticsRepository repository)
        {
            var so = new SerializedObject(repository);
            so.FindProperty("partyPolicyCatalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PartyTacticsPolicyCatalogAsset>(TableAssetPaths.PartyPolicyCatalog);
            so.ApplyModifiedProperties();
        }

        // 저장소 8개를 UIManager 산하로 재배치한 뒤(2026-09-08 Bootstrap 리팩토링, 커밋 02c5dee)
        // 새로 발견된 배선 누락: InMemoryTacticsRepository/TacticsPanel의 catalog(RoleGroupTacticsCatalogAsset)
        // 필드는 예전엔 재사용되던 같은 오브젝트에 수동으로 인스펙터 연결돼 있었는데, GetOrCreateManager가
        // 이름+부모 기준으로만 찾다 보니(부모가 root→uiManager로 바뀜) 매칭에 실패해 매번 새 GameObject를
        // 만들면서 그 수동 연결이 사라졌다 - roleGroupOverride.TargetPriority가 enum 0(default)으로
        // 새어나가 TargetSelectorFactory.Create에서 ArgumentOutOfRangeException을 던지는 실전 크래시로
        // 이어졌다(전투 시작 시점). partyPolicyCatalog와 마찬가지로 코드로 배선해 재실행에도 안전하게 한다.
        private static void WireRoleGroupCatalog(InMemoryTacticsRepository repository)
        {
            var so = new SerializedObject(repository);
            so.FindProperty("catalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<RoleGroupTacticsCatalogAsset>(TableAssetPaths.RoleGroupTacticsCatalog);
            so.ApplyModifiedProperties();
        }

        // TacticsPanel도 같은 이유로 catalog 배선이 누락돼 있었다(위 InMemoryTacticsRepository 사례와
        // 동일 원인) - 이쪽은 화면 드롭다운 채우기용이라 크래시 자체의 원인은 아니지만 함께 정정한다.
        private static void WireRoleGroupCatalog(TacticsPanel panel)
        {
            var so = new SerializedObject(panel);
            so.FindProperty("catalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<RoleGroupTacticsCatalogAsset>(TableAssetPaths.RoleGroupTacticsCatalog);
            so.ApplyModifiedProperties();
        }

        // 라벨 조회용 String 에셋 2종 - catalog(값)와 마찬가지로 처음 생기는 에셋이라 자동 배선한다
        // (Docs/설계/18번 §8.2). InMemoryTacticsRepository는 라벨을 다루지 않아(값/정책만 저장) 배선
        // 대상이 아니다 - TacticsPanel(UI 계층)만 필요로 한다.
        private static void WireTacticsStringTables(TacticsPanel panel)
        {
            var so = new SerializedObject(panel);
            so.FindProperty("partyPolicyStrings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PartyTacticsPolicyStringsTableAsset>(TableAssetPaths.PartyPolicyStringsTable);
            so.FindProperty("roleGroupTacticsStrings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<RoleGroupTacticsStringsTableAsset>(TableAssetPaths.RoleGroupTacticsStringsTable);
            so.ApplyModifiedProperties();
        }

        private static void SyncManagedComponents(DependencyManager dependencyManager, MonoBehaviour[] managedComponents)
        {
            var serializedDependencyManager = new SerializedObject(dependencyManager);
            var managedComponentsProperty = serializedDependencyManager.FindProperty("managedComponents");

            managedComponentsProperty.arraySize = managedComponents.Length;
            for (var i = 0; i < managedComponents.Length; i++)
            {
                managedComponentsProperty.GetArrayElementAtIndex(i).objectReferenceValue = managedComponents[i];
            }

            serializedDependencyManager.ApplyModifiedProperties();
        }
    }
}
