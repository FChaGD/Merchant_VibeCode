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
    /// </summary>
    public static class ManagerHierarchyInstaller
    {
        private const string RootName = "Managers";

        [MenuItem("Tools/Game/Build Bootstrap Scene")]
        public static void BuildManagerHierarchy()
        {
            var root = EditorUIBuilder.GetOrCreateSceneRoot(EditorSceneManager.GetActiveScene(), RootName);

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

            var dependencyManager = EditorUIBuilder.GetOrCreateManager<DependencyManager>(root.transform, "DependencyManager");
            var gameManager = EditorUIBuilder.GetOrCreateManager<GameManager>(root.transform, "GameManager");
            var inputManager = EditorUIBuilder.GetOrCreateManager<InputManager>(root.transform, "InputManager");
            var uiManager = EditorUIBuilder.GetOrCreateManager<UIManager>(root.transform, "UIManager");
            // uiManager보다 뒤에서 만들어야 하는 건 아니다(생성 순서는 무관) - 다만 아래
            // SyncManagedComponents 배열에서는 반드시 uiManager 뒤에 둬야 한다(주석 참고).
            var sceneTransitionEffectController = EditorUIBuilder.GetOrCreateManager<SceneTransitionEffectController>(root.transform, "SceneTransitionEffectController");
            EnsureSceneTransitionCurtain(root.transform, sceneTransitionEffectController);
            var battleManager = EditorUIBuilder.GetOrCreateManager<BattleManager>(root.transform, "BattleManager");
            var aiManager = EditorUIBuilder.GetOrCreateManager<AIManager>(root.transform, "AIManager");
            var encounterManager = EditorUIBuilder.GetOrCreateManager<EncounterManager>(root.transform, "EncounterManager");

            // SceneLoader는 GameManager와 같은 GameObject에 부착하되, 자체적으로 DI에 등록되는
            // 독립된 관리 대상이므로 managedComponents 동기화 목록에도 포함한다.
            var sceneLoader = EnsureSiblingComponent<SceneLoader>(gameManager.gameObject);

            // SessionStateTracker는 GameManager 산하 컴포넌트라 전역 DI 대상이 아니다(GameManager가
            // RegisterSelf에서 직접 조회해 ISessionState/ISessionPauseControl로 등록) - 그래서
            // managedComponents 동기화 목록에는 포함하지 않는다.
            EnsureSiblingComponent<SessionStateTracker>(gameManager.gameObject);

            // LiveBattleSimulationRule도 BattleManager 산하 컴포넌트라 전역 DI 대상이 아니다
            // (BattleManager가 GetComponent<IBattleResultRule>()로 직접 조회).
            EnsureSiblingComponent<LiveBattleSimulationRule>(battleManager.gameObject);

            // PlaceholderDefeatConsequenceRule도 같은 이유로 BattleManager 산하 컴포넌트다
            // (BattleManager가 GetComponent<IDefeatConsequenceRule>()로 직접 조회).
            EnsureSiblingComponent<PlaceholderDefeatConsequenceRule>(battleManager.gameObject);

            // 전투 디버그 기즈모(포위망/방진선/이동 목적지)는 여기서 설치하지 않는다 - "게임 빌드"와
            // "디버그 도구 켜고 끄기"는 다른 관심사라 DebugBootstrapReentryGuard와 같은 자리
            // (Tools/Game/Debug/, BattleGizmoInstaller.cs)에서 별도 Install/Remove 메뉴로 관리한다.

            // HubUIController/FormationPanel/TripPanel/TacticsPanel/FieldUIController는 전역 매니저가 아니라 UIManager 산하 컴포넌트이므로 같은 GameObject에 부착한다.
            EnsureSiblingComponent<HubUIController>(uiManager.gameObject);
            EnsureSiblingComponent<FormationPanel>(uiManager.gameObject);
            EnsureSiblingComponent<TripPanel>(uiManager.gameObject);
            EnsureSiblingComponent<TacticsPanel>(uiManager.gameObject);
            var fieldUIController = EnsureSiblingComponent<FieldUIController>(uiManager.gameObject);

            // 씬별 UI 배선(IContentSceneUIWiring)도 전역 DI 대상이 아니라 UIManager 산하 컴포넌트다 -
            // UIManager가 GetComponents<IContentSceneUIWiring>()로 이들을 스스로 수집한다.
            EnsureSiblingComponent<HubUIWiring>(uiManager.gameObject);
            EnsureSiblingComponent<FieldUIWiring>(uiManager.gameObject);

            // 전투 뷰 유닛 프리팹은 EditorUIBuilder(공용 조립 로직)가 자산으로 만들어 두고, 여기(Bootstrap
            // 담당)서는 그 자산을 FieldUIController 필드에 연결만 한다.
            WireFieldBattleViewPrefabs(fieldUIController);

            // 상행 관리 데이터 시스템이 아직 없어, 배치 UI 팔레트 테스트용 임시 로스터 제공자를 등록한다.
            // 실제 데이터 시스템이 생기면 이 매니저와 아이콘 생성 로직을 함께 제거한다.
            var placeholderRosterProvider = EditorUIBuilder.GetOrCreateManager<PlaceholderCaravanRosterProvider>(root.transform, "PlaceholderCaravanRosterProvider");
            WirePlaceholderRosterIcons(placeholderRosterProvider);

            // 배치 UI의 "적용" 버튼이 반영할 대상 - 현재 플레이 세션 동안만 유지되는 인메모리 저장소.
            var formationRepository = EditorUIBuilder.GetOrCreateManager<InMemoryFormationRepository>(root.transform, "InMemoryFormationRepository");

            // 상행 동안 보유 유닛 HP/사망 상태를 저장하는 대상(설계 15번) - formationRepository와 같은
            // 성격의 인메모리 저장소. ResolveDependencies에서 ICaravanRosterProvider를 TryResolve하므로
            // placeholderRosterProvider보다 뒤에 둔다(가독성 목적 - DependencyManager가 RegisterSelf를
            // 전부 끝낸 뒤 ResolveDependencies를 호출하는 2단계 구조라 실제 순서 의존성은 없다).
            var unitConditionRepository = EditorUIBuilder.GetOrCreateManager<InMemoryUnitConditionRepository>(root.transform, "InMemoryUnitConditionRepository");

            // 방향성 지시 UI(TacticsPanel)가 반영할 대상 - 배치와 같은 성격의 인메모리 저장소.
            var tacticsRepository = EditorUIBuilder.GetOrCreateManager<InMemoryTacticsRepository>(root.transform, "InMemoryTacticsRepository");

            // 지역 시스템이 아직 없어, 상행 준비 UI 테스트용 임시 출발지/도착지/상행 요약 제공자를 등록한다.
            // 실제 데이터 시스템이 생기면 이 매니저를 함께 제거한다.
            var placeholderTripInfoProvider = EditorUIBuilder.GetOrCreateManager<PlaceholderTripInfoProvider>(root.transform, "PlaceholderTripInfoProvider");

            SyncManagedComponents(dependencyManager, new MonoBehaviour[]
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
                placeholderRosterProvider,
                formationRepository,
                unitConditionRepository,
                tacticsRepository,
                placeholderTripInfoProvider,
            });

            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("매니저 하이어라키 생성/동기화 완료. 씬을 저장(Ctrl+S)해야 변경사항이 파일에 반영된다.");
        }

        private static T EnsureSiblingComponent<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(go);
        }

        // Hub↔Field 씬 전환 연출용 커튼은 Bootstrap(영속) 스코프여야 한다 - 콘텐츠 씬 스코프 오브젝트는
        // 그 씬이 언로드되는 순간 함께 파괴되기 때문이다(Docs/설계/10-2026-08-26-씬전환_연출_아키텍처.md §5).
        // CanvasScaler 설정은 Hub/Field 콘텐츠 씬 캔버스와 반드시 대조해 맞춰야 한다 - 다르면 슬라이드
        // 거리/커튼 커버리지가 화면상 어긋난다(§12 남은 이슈). 아래 값은 이 프로젝트의 일반적인 설정을
        // 가정한 자리표시자다.
        private static void EnsureSceneTransitionCurtain(Transform managersRoot, SceneTransitionEffectController controller)
        {
            var canvas = EditorUIBuilder.GetOrCreateManager<Canvas>(managersRoot, "SceneTransitionCanvas");
            canvas.gameObject.layer = LayerMask.NameToLayer("UI");
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // Hub/Field 콘텐츠 씬 캔버스보다 항상 위에 그려지도록 충분히 높은 값

            var scaler = EditorUIBuilder.GetOrAddComponent<CanvasScaler>(canvas.gameObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

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
