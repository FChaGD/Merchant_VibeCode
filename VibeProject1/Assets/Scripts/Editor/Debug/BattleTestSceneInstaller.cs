using Game.Core;
using Game.Core.DebugTools;
using Game.Core.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.Editor.DebugTools
{
    /// <summary>
    /// 사기(Morale)/전투 시뮬레이션 반복 검증용 독립 씬을 코드로 생성/동기화한다. 실제 게임은 전투
    /// 진입까지 Hub→상행 시작→랜덤 인카운터(5초마다 30%)를 거쳐야 해 반복 테스트가 느리다 - 이 씬은
    /// Bootstrap/Hub/Field 없이 그 자체로 완결된 매니저 하이어라키를 갖고 있어, BattleTest.unity를
    /// 열고 Play만 누르면 세팅→전투 시작까지 곧바로 반복할 수 있다(사용자 확정, Docs/설계 계획
    /// "배틀 테스트 씬 — 개발자 디버그 도구 확장" 참고). UIManager는 ISceneLoader(Hub/Field 전환
    /// 시스템)에 강하게 결합돼 있어 그대로 못 쓰고, 대신 BattleTestPanelHost(최소 IUIManager 구현)를
    /// 쓴다. 아군 배치도 정비창(그리드) 대신 자유 드래그 유닛 팔레트로 교체됐다 - 전투 로직 자체는
    /// LiveBattleSimulationRule과 동일 클래스(BattleCharacterUnit/PartyMorale/Frontline·
    /// RangedSurround 조율자 등)를 그대로 쓰지만, 오케스트레이션만 BattleTestSimulationRule로
    /// 파생했다(실제 게임 코드는 건드리지 않는다는 원칙). Tools/Game/Debug/에 두는 이유는
    /// BattleGizmoInstaller와 같다: "게임 빌드"와는 다른 관심사인 디버그 전용 도구.
    /// </summary>
    public static class BattleTestSceneInstaller
    {
        private const string SceneName = "BattleTest";
        private const string ScenePath = "Assets/Scenes/" + SceneName + ".unity";
        private const string RoleGroupMapAssetPath = "Assets/Prefabs/ScriptableObejct/MercenaryRoleGroupMap.asset";
        private const string TacticsCatalogAssetPath = "Assets/Prefabs/ScriptableObejct/RoleGroupTacticsCatalog.asset";

        [MenuItem("Tools/Game/Debug/Build Battle Test Scene")]
        public static void BuildBattleTestScene()
        {
            var scene = OpenOrCreateScene();

            EditorUIBuilder.EnsureSceneEventSystem(scene);
            EnsureMainCamera();
            EditorUIBuilder.ConfigureBattleCamera();
            var battleWorldRoot = EditorUIBuilder.EnsureBattleWorldRoot();
            // Field와 달리 전환할 "이동 뷰"가 없어 처음부터 항상 활성 상태다.
            battleWorldRoot.gameObject.SetActive(true);
            var cameraView = Camera.main.GetComponent<BattleFieldWorldCameraView>();

            var sceneUIRoot = EnsureCanvasAndSceneUIRoot();
            // 전장 드래그팬/휠줌 입력 캡처(요구사항: Field 씬과 동일) - 다른 UI보다 먼저 만들어 항상
            // 맨 뒤(첫 번째 형제)에 깔리게 한다. 그래야 팔레트/버튼 등 다른 UI가 그 위에서 정상적으로
            // 자기 클릭/드래그를 먼저 가로챈다(겹치는 부분만).
            var unitPickerView = BuildCameraDragCatcher(sceneUIRoot.transform, cameraView);

            // ==================== 매니저(전투 로직) 먼저 조립 - 아래 UI들이 참조해야 한다 ====================
            var managersRoot = EditorUIBuilder.GetOrCreateSceneRoot(scene, "Managers");
            EditorUIBuilder.RemoveMissingScriptsRecursively(managersRoot.transform);

            var dependencyManager = EditorUIBuilder.GetOrCreateManager<DependencyManager>(managersRoot.transform, "DependencyManager");

            var battleManager = EditorUIBuilder.GetOrCreateManager<BattleManager>(managersRoot.transform, "BattleManager");
            var battleTestSimulation = EditorUIBuilder.GetOrAddComponent<BattleTestSimulationRule>(battleManager.gameObject);
            EditorUIBuilder.GetOrAddComponent<PlaceholderDefeatConsequenceRule>(battleManager.gameObject);
            WireRoleGroupMap(battleTestSimulation);

            var tacticsRepository = EditorUIBuilder.GetOrCreateManager<InMemoryTacticsRepository>(managersRoot.transform, "InMemoryTacticsRepository");
            WireTacticsCatalog(tacticsRepository);

            // 전투 디버그 기즈모 4종을 이 씬에서는 항상 켜둔다(요구사항 #5) - Bootstrap과 달리
            // Tools/Game/Debug/Install Battle Gizmos 토글을 거치지 않고 인스톨러가 직접 부착한다.
            EditorUIBuilder.GetOrAddComponent<BattleFrontlineGizmoView>(battleManager.gameObject);
            EditorUIBuilder.GetOrAddComponent<BattleSurroundGizmoView>(battleManager.gameObject);
            EditorUIBuilder.GetOrAddComponent<BattleMoveTargetGizmoView>(battleManager.gameObject);
            EditorUIBuilder.GetOrAddComponent<BattleTestMoraleWaveGizmoView>(battleManager.gameObject);

            // ==================== UI ====================
            TacticsUIBuilder.Build(sceneUIRoot.transform);
            var tacticsButton = BuildActionButton(sceneUIRoot.transform, "TacticsButton", "방향성 지시", new Vector2(0.02f, 0.02f), new Vector2(0.14f, 0.10f), new Color(0.85f, 0.75f, 0.95f, 1f));

            var startButton = BuildActionButton(sceneUIRoot.transform, "StartBattleButton", "전투 시작", new Vector2(0.88f, 0.20f), new Vector2(0.99f, 0.28f), new Color(0.4f, 0.8f, 0.4f, 1f));
            var pauseButton = BuildActionButton(sceneUIRoot.transform, "PauseBattleButton", "전투 중지", new Vector2(0.88f, 0.11f), new Vector2(0.99f, 0.19f), new Color(0.9f, 0.8f, 0.3f, 1f));
            var resetButton = BuildActionButton(sceneUIRoot.transform, "ResetBattleButton", "전투 리셋", new Vector2(0.88f, 0.02f), new Vector2(0.99f, 0.10f), new Color(0.9f, 0.4f, 0.3f, 1f));

            var (allyPaletteRoot, dragGhost, allyIcons, enemyIcons) = BuildUnitPalette(sceneUIRoot.transform);
            var paletteView = EditorUIBuilder.GetOrAddComponent<BattleTestUnitPaletteView>(allyPaletteRoot);
            WireUnitPalette(paletteView, dragGhost, cameraView, allyIcons, enemyIcons);

            BuildExtentGizmo(sceneUIRoot.transform, battleWorldRoot.transform, cameraView, battleTestSimulation);
            BuildEnemySetupPanel(sceneUIRoot.transform, battleTestSimulation);
            BuildSpawnPointGizmo(battleWorldRoot.transform, battleTestSimulation);
            BuildMoraleGauge(sceneUIRoot.transform, battleTestSimulation);
            var resultPopupView = BuildResultPopup(sceneUIRoot.transform);
            var unitInfoPanelView = BuildUnitInfoPanel(sceneUIRoot.transform, battleTestSimulation);
            var spawnPointPanelView = BuildSpawnPointPanel(sceneUIRoot.transform, battleTestSimulation);

            // ==================== 배틀 테스트 씬 조율자(BattleTestPanelHost/TacticsPanel/BattleTestController) ====================
            var panelHost = EditorUIBuilder.GetOrCreateManager<BattleTestPanelHost>(managersRoot.transform, "BattleTestUI");
            var tacticsPanel = EditorUIBuilder.GetOrAddComponent<TacticsPanel>(panelHost.gameObject);
            WireTacticsPanelCatalog(tacticsPanel);
            var battleTestController = EditorUIBuilder.GetOrAddComponent<BattleTestController>(panelHost.gameObject);
            WireBattleTestController(battleTestController, startButton, pauseButton, resetButton, tacticsButton, battleWorldRoot, cameraView, paletteView, resultPopupView, unitInfoPanelView, unitPickerView, spawnPointPanelView, battleTestSimulation);

            SyncManagedComponents(dependencyManager, new MonoBehaviour[]
            {
                battleManager,
                tacticsRepository,
                panelHost,
                battleTestController,
            });

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"'{SceneName}' 씬 생성/동기화 완료. 씬을 저장(Ctrl+S)한 뒤 Play를 누르면 바로 유닛 팔레트로 세팅→전투 시작을 반복할 수 있다.");
        }

        private static UnityEngine.SceneManagement.Scene OpenOrCreateScene()
        {
            if (System.IO.File.Exists(ScenePath))
            {
                var opened = EditorSceneManager.GetActiveScene().path == ScenePath
                    ? EditorSceneManager.GetActiveScene()
                    : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                return opened;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }
            EditorSceneManager.SaveScene(scene, ScenePath);
            return scene;
        }

        private static void EnsureMainCamera()
        {
            GameObject cameraGo;
            if (Camera.main != null)
            {
                cameraGo = Camera.main.gameObject;
            }
            else
            {
                cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cameraGo.tag = "MainCamera";
                Undo.RegisterCreatedObjectUndo(cameraGo, "Create Main Camera");
            }

            // z=0에 그대로 두면 유닛 스프라이트(z=0)와 같은 위치가 돼 near clip plane에 가려 아무것도
            // 안 보인다 - BattleFieldWorldCameraView.ScreenToWorld가 카메라 z를 음수로 전제하는 것과
            // 같은 이유(Field 씬의 기존 Main Camera는 이미 z<0으로 배치돼 있어 이 문제가 없었다). 이미
            // 존재하는 카메라라도 재실행할 때마다 강제해 잘못된 위치로 남아있지 않게 한다.
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            cameraGo.GetComponent<Camera>().orthographic = true;
        }

        private const string UICanvasName = "Canvas";

        private static SceneUIRoot EnsureCanvasAndSceneUIRoot()
        {
            // 씬에 매니저 하이어라키(SceneTransitionCanvas 등)가 함께 있을 때 FindFirstObjectByType<Canvas>가
            // Unity 내부 열거 순서에 따라 SceneTransitionCanvas를 대신 집어 실행마다 다른 Canvas 밑에
            // 이 씬 UI 전체가 중복 생성되는 버그가 실제로 발생했다("기존 팔레트가 사라지지 않았다" 리포트 -
            // 확인해보니 UnitPalette가 두 벌 존재했고, 하나는 SceneTransitionCanvas 밑에 잘못 지어져
            // 있었다). 타입이 아니라 이름으로 우리 캔버스를 특정해 이 모호성을 근본적으로 없앤다.
            CleanupMisplacedUIRoot();

            var canvasGo = FindCanvasByName(UICanvasName);
            if (canvasGo == null)
            {
                canvasGo = new GameObject(UICanvasName, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
                canvasGo.layer = LayerMask.NameToLayer("UI");
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            return EditorUIBuilder.GetOrAddComponent<SceneUIRoot>(canvasGo);
        }

        private static GameObject FindCanvasByName(string name)
        {
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas.gameObject.name == name) return canvas.gameObject;
            }
            return null;
        }

        // 위 모호성 버그로 예전 실행 중 하나가 SceneTransitionCanvas(ManagerHierarchyInstaller 소유,
        // 정상 자식은 "Curtain" 하나뿐 - EnsureSceneTransitionCurtain 참고) 밑에 이 씬의 UI 전체를
        // 잘못 지어놓았을 수 있다. 우리 캔버스가 아닌데 SceneUIRoot가 붙어있는 Canvas를 발견하면
        // "Curtain"을 제외한 모든 자식 + SceneUIRoot 자체를 지워 오염을 되돌린다 - 재실행할 때마다
        // 자동으로 정리되므로 별도 수동 정리가 필요 없다.
        private static void CleanupMisplacedUIRoot()
        {
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas.gameObject.name == UICanvasName) continue;

                var strayRoot = canvas.GetComponent<SceneUIRoot>();
                if (strayRoot == null) continue;

                Undo.DestroyObjectImmediate(strayRoot);

                var staleChildren = new System.Collections.Generic.List<GameObject>();
                foreach (Transform child in canvas.transform)
                {
                    if (child.name != "Curtain") staleChildren.Add(child.gameObject);
                }
                foreach (var stale in staleChildren) Undo.DestroyObjectImmediate(stale);
            }
        }

        // Field 씬의 BattleView 배경(FieldUIInstaller.BuildBattleView)과 같은 패턴 - 전체 화면을
        // 덮는 투명 이미지로 드래그팬/휠줌 입력만 캡처한다(BattleFieldInputForwarder 그대로 재사용,
        // 프로덕션 코드 변경 없음). 같은 오브젝트에 BattleTestUnitPickerView도 얹는다 - 이 배경이
        // 화면 전체를 덮고 있어 별도 Physics2DRaycaster를 쓰면 유닛 클릭이 항상 이 배경에 막히므로,
        // 이 배경 자신이 받은 클릭을 월드 좌표로 바꿔 유닛을 찾게 한다(BattleTestUnitPickerView 참고).
        private static BattleTestUnitPickerView BuildCameraDragCatcher(Transform parent, BattleFieldWorldCameraView cameraView)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, "CameraDragCatcher");
            EditorUIBuilder.SetStretch(go.GetComponent<RectTransform>());
            var background = EditorUIBuilder.EnsureImage(go, new Color(1f, 1f, 1f, 0.001f));
            background.raycastTarget = true;
            go.transform.SetAsFirstSibling();
            EditorUIBuilder.GetOrAddComponent<BattleFieldInputForwarder>(go);

            var pickerView = EditorUIBuilder.GetOrAddComponent<BattleTestUnitPickerView>(go);
            var so = new SerializedObject(pickerView);
            so.FindProperty("cameraView").objectReferenceValue = cameraView;
            so.ApplyModifiedProperties();

            return pickerView;
        }

        private static Button BuildActionButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, name);
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), anchorMin, anchorMax);
            EditorUIBuilder.EnsureImage(go, color);
            var button = EditorUIBuilder.EnsureButton(go);
            EditorUIBuilder.EnsureLabel(go.transform, label);
            return button;
        }

        // ==================== 유닛 팔레트(요구사항 #1/#6) ====================
        // 아군/적 팔레트를 각각 화면 좌상단/우상단 구석에 3칸씩 나눠 배치한다(기존엔 상단 중앙에 6칸을
        // 한 줄로 몰아넣어 가로가 넓고 세로가 짧아 아이콘이 눌려 보였다) - 칸 수를 6→3으로 줄이고 세로
        // 여유(ExtentInputPanel/EnemySetupPanel 윗쪽 빈 공간)를 그만큼 더 써서 아이콘이 정사각형에
        // 가깝게 보이도록 한다.
        private static (GameObject allyRoot, Image dragGhost, BattleTestPaletteIconView[] allyIcons, BattleTestPaletteIconView[] enemyIcons) BuildUnitPalette(Transform parent)
        {
            // 상단 중앙 6칸 한 줄 배치(구 이름 "UnitPalette")에서 좌상단/우상단 3칸씩 분리 배치로
            // 구조가 바뀌어, 재실행 시 옛 오브젝트가 안 지워지고 남는다.
            EditorUIBuilder.DestroyChildIfExists(parent, "UnitPalette");

            var allyRoot = EditorUIBuilder.GetOrCreateUIObject(parent, "AllyUnitPalette");
            EditorUIBuilder.SetAnchors(allyRoot.GetComponent<RectTransform>(), new Vector2(0.02f, 0.88f), new Vector2(0.21f, 0.99f));
            EditorUIBuilder.EnsureImage(allyRoot, new Color(0f, 0f, 0f, 0.15f));

            var enemyRoot = EditorUIBuilder.GetOrCreateUIObject(parent, "EnemyUnitPalette");
            EditorUIBuilder.SetAnchors(enemyRoot.GetComponent<RectTransform>(), new Vector2(0.79f, 0.88f), new Vector2(0.98f, 0.99f));
            EditorUIBuilder.EnsureImage(enemyRoot, new Color(0f, 0f, 0f, 0.15f));

            // 아군 3종(전사=사각형/궁수=오각형/방패병=육각형) - PlaceholderCaravanRosterProvider와
            // 같은 도형 매핑을 재사용한다(FormationPlaceholderIcons, 에디터 어셈블리에서 접근 가능).
            var allyIcons = new[]
            {
                BuildPaletteIcon(allyRoot.transform, "AllyWarrior", 0, new Color(0.75f, 0.87f, 1f, 1f)),
                BuildPaletteIcon(allyRoot.transform, "AllyArcher", 1, new Color(0.75f, 0.87f, 1f, 1f)),
                BuildPaletteIcon(allyRoot.transform, "AllyShieldBearer", 2, new Color(0.75f, 0.87f, 1f, 1f)),
            };
            allyIcons[0].BindAlly(MercenaryClass.Warrior, FormationPlaceholderIcons.GetOrCreateSquare());
            allyIcons[1].BindAlly(MercenaryClass.Archer, FormationPlaceholderIcons.GetOrCreatePentagon());
            allyIcons[2].BindAlly(MercenaryClass.ShieldBearer, FormationPlaceholderIcons.GetOrCreateHexagon());

            // 적 3종(약탈자=사각형/괴수=삼각형/적대자=원) - BattlePlaceholderSprite.ForEnemyType과 같은
            // 도형 매핑이지만, 그 클래스는 런타임 전용(internal, Core.asmdef)이라 에디터 어셈블리에서
            // 접근할 수 없다(BattlePlaceholderSprite 요약 주석 - FormationPlaceholderIcons와 로직이
            // 겹쳐도 의도적으로 분리돼 있음) - 대신 FormationPlaceholderIcons의 같은 도형을 재사용한다.
            var enemyIcons = new[]
            {
                BuildPaletteIcon(enemyRoot.transform, "EnemyMarauder", 0, new Color(1f, 0.8f, 0.8f, 1f)),
                BuildPaletteIcon(enemyRoot.transform, "EnemyMonster", 1, new Color(1f, 0.8f, 0.8f, 1f)),
                BuildPaletteIcon(enemyRoot.transform, "EnemyAdversary", 2, new Color(1f, 0.8f, 0.8f, 1f)),
            };
            enemyIcons[0].BindEnemy(EnemyType.Marauder, FormationPlaceholderIcons.GetOrCreateSquare());
            enemyIcons[1].BindEnemy(EnemyType.Monster, FormationPlaceholderIcons.GetOrCreateTriangle());
            enemyIcons[2].BindEnemy(EnemyType.Adversary, FormationPlaceholderIcons.GetOrCreateCircle());

            // 드래그 고스트 - 매번 Instantiate/Destroy하지 않고 하나만 만들어 켜고 끈다.
            var ghostGo = EditorUIBuilder.GetOrCreateUIObject(parent, "UnitPaletteDragGhost");
            var ghostRect = ghostGo.GetComponent<RectTransform>();
            ghostRect.sizeDelta = new Vector2(64f, 64f);
            var ghostImage = EditorUIBuilder.EnsureImage(ghostGo, Color.white);
            ghostImage.raycastTarget = false;
            ghostGo.SetActive(false);
            ghostGo.transform.SetAsLastSibling(); // 항상 다른 요소들보다 위에 그려지도록.

            return (allyRoot, ghostImage, allyIcons, enemyIcons);
        }

        private static BattleTestPaletteIconView BuildPaletteIcon(Transform parent, string name, int slotIndex, Color backgroundColor)
        {
            const int slotCount = 3;
            var slotWidth = 1f / slotCount;
            var left = slotIndex * slotWidth + 0.02f;
            var right = (slotIndex + 1) * slotWidth - 0.02f;

            var go = EditorUIBuilder.GetOrCreateUIObject(parent, name);
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), new Vector2(left, 0.05f), new Vector2(right, 0.95f));
            EditorUIBuilder.EnsureImage(go, backgroundColor);

            var iconGo = EditorUIBuilder.GetOrCreateUIObject(go.transform, "Icon");
            EditorUIBuilder.SetAnchors(iconGo.GetComponent<RectTransform>(), new Vector2(0.15f, 0.15f), new Vector2(0.85f, 0.85f));
            EditorUIBuilder.EnsureImage(iconGo, Color.white);

            var iconView = EditorUIBuilder.GetOrAddComponent<BattleTestPaletteIconView>(go);
            var so = new SerializedObject(iconView);
            so.FindProperty("iconImage").objectReferenceValue = iconGo.GetComponent<Image>();
            so.ApplyModifiedProperties();

            return iconView;
        }

        private static void WireUnitPalette(BattleTestUnitPaletteView paletteView, Image dragGhost, BattleFieldWorldCameraView cameraView, BattleTestPaletteIconView[] allyIcons, BattleTestPaletteIconView[] enemyIcons)
        {
            var so = new SerializedObject(paletteView);
            so.FindProperty("dragGhost").objectReferenceValue = dragGhost;
            so.FindProperty("cameraView").objectReferenceValue = cameraView;

            var allyProperty = so.FindProperty("allyIcons");
            allyProperty.arraySize = allyIcons.Length;
            for (var i = 0; i < allyIcons.Length; i++) allyProperty.GetArrayElementAtIndex(i).objectReferenceValue = allyIcons[i];

            var enemyProperty = so.FindProperty("enemyIcons");
            enemyProperty.arraySize = enemyIcons.Length;
            for (var i = 0; i < enemyIcons.Length; i++) enemyProperty.GetArrayElementAtIndex(i).objectReferenceValue = enemyIcons[i];

            so.ApplyModifiedProperties();
        }

        // ==================== 대열 범위 기즈모(요구사항 #2/#3) ====================
        private static BattleTestExtentGizmoView BuildExtentGizmo(Transform canvasParent, Transform worldParent, BattleFieldWorldCameraView cameraView, BattleTestSimulationRule simulationRule)
        {
            // ConfigureBattleCamera가 cullingMask를 Battle 레이어 하나로 좁혀두므로, 이 레이어가
            // 아니면 LineRenderer가 카메라에 아예 안 보인다(AllyLayer/EnemyLayer와 같은 이유).
            var battleLayer = LayerMask.NameToLayer(BattleFieldGeometry.BattleLayerName);
            var boxGo = EditorUIBuilder.GetOrCreateWorldChild(worldParent, "ExtentBox", battleLayer >= 0 ? battleLayer : 0);
            var lineRenderer = EditorUIBuilder.GetOrAddComponent<LineRenderer>(boxGo);
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = Color.yellow;
            lineRenderer.endColor = Color.yellow;
            lineRenderer.startWidth = 0.08f;
            lineRenderer.endWidth = 0.08f;
            lineRenderer.useWorldSpace = true;

            var handles = new BattleTestDragHandle[4];
            for (var i = 0; i < 4; i++)
            {
                var handleGo = EditorUIBuilder.GetOrCreateUIObject(canvasParent, $"ExtentCornerHandle{i}");
                var handleRect = handleGo.GetComponent<RectTransform>();
                handleRect.sizeDelta = new Vector2(24f, 24f);
                EditorUIBuilder.EnsureImage(handleGo, Color.yellow);
                handles[i] = EditorUIBuilder.GetOrAddComponent<BattleTestDragHandle>(handleGo);
            }

            var panelGo = EditorUIBuilder.GetOrCreateUIObject(canvasParent, "ExtentInputPanel");
            EditorUIBuilder.SetAnchors(panelGo.GetComponent<RectTransform>(), new Vector2(0.02f, 0.76f), new Vector2(0.20f, 0.88f));
            EditorUIBuilder.EnsureImage(panelGo, new Color(0f, 0f, 0f, 0.15f));

            var columnsInput = BuildLabeledInput(panelGo.transform, "ColumnsInput", "열(X)", new Vector2(0.02f, 0.55f), new Vector2(0.48f, 0.95f));
            var rowsInput = BuildLabeledInput(panelGo.transform, "RowsInput", "행(Y)", new Vector2(0.52f, 0.55f), new Vector2(0.98f, 0.95f));
            var applyGo = EditorUIBuilder.GetOrCreateUIObject(panelGo.transform, "ApplyButton");
            EditorUIBuilder.SetAnchors(applyGo.GetComponent<RectTransform>(), new Vector2(0.30f, 0.05f), new Vector2(0.70f, 0.48f));
            EditorUIBuilder.EnsureImage(applyGo, new Color(0.7f, 1f, 0.7f, 1f));
            var applyButton = EditorUIBuilder.EnsureButton(applyGo);
            EditorUIBuilder.EnsureLabel(applyGo.transform, "적용");

            var gizmoView = EditorUIBuilder.GetOrAddComponent<BattleTestExtentGizmoView>(boxGo);
            var so = new SerializedObject(gizmoView);
            so.FindProperty("boxRenderer").objectReferenceValue = lineRenderer;
            var handlesProperty = so.FindProperty("cornerHandles");
            handlesProperty.arraySize = handles.Length;
            for (var i = 0; i < handles.Length; i++) handlesProperty.GetArrayElementAtIndex(i).objectReferenceValue = handles[i];
            so.FindProperty("columnCountInput").objectReferenceValue = columnsInput;
            so.FindProperty("rowCountInput").objectReferenceValue = rowsInput;
            so.FindProperty("applyButton").objectReferenceValue = applyButton;
            so.FindProperty("cameraView").objectReferenceValue = cameraView;
            so.FindProperty("simulationRule").objectReferenceValue = simulationRule;
            so.ApplyModifiedProperties();

            return gizmoView;
        }

        // ==================== 스폰 포인트 원 + 마커(신규 요구사항) ====================
        // 적이 실제로 생성되는 고정 12지점(BattleFieldGeometry.SpawnPointCount)과 그 반지름 원을
        // Scene뷰 전용 Gizmos가 아니라 실제 월드 오브젝트로 그린다 - Game뷰(Play)에서도 항상 보이고
        // 클릭도 가능해야 하기 때문(요구사항: "테스트 씬에서는 실제 보이게").
        private static BattleTestSpawnPointGizmoView BuildSpawnPointGizmo(Transform worldParent, BattleTestSimulationRule simulationRule)
        {
            var battleLayer = LayerMask.NameToLayer(BattleFieldGeometry.BattleLayerName);
            var layer = battleLayer >= 0 ? battleLayer : 0;

            var circleGo = EditorUIBuilder.GetOrCreateWorldChild(worldParent, "SpawnRadiusCircle", layer);
            var circleRenderer = EditorUIBuilder.GetOrAddComponent<LineRenderer>(circleGo);
            circleRenderer.material = new Material(Shader.Find("Sprites/Default"));
            circleRenderer.startColor = new Color(0.3f, 0.7f, 1f, 0.6f);
            circleRenderer.endColor = new Color(0.3f, 0.7f, 1f, 0.6f);
            circleRenderer.startWidth = 0.06f;
            circleRenderer.endWidth = 0.06f;
            circleRenderer.useWorldSpace = true;

            var markersRoot = EditorUIBuilder.GetOrCreateWorldChild(worldParent, "SpawnPointMarkers", layer);
            var markers = new BattleTestSpawnPointMarkerView[BattleFieldGeometry.SpawnPointCount];
            for (var i = 0; i < markers.Length; i++)
            {
                var markerGo = EditorUIBuilder.GetOrCreateWorldChild(markersRoot.transform, $"SpawnPointMarker{i}", layer);
                var renderer = EditorUIBuilder.GetOrAddComponent<SpriteRenderer>(markerGo);
                EditorUIBuilder.GetOrAddComponent<CircleCollider2D>(markerGo);
                var marker = EditorUIBuilder.GetOrAddComponent<BattleTestSpawnPointMarkerView>(markerGo);

                var markerSo = new SerializedObject(marker);
                markerSo.FindProperty("bodyRenderer").objectReferenceValue = renderer;
                markerSo.ApplyModifiedProperties();

                markers[i] = marker;
            }

            var gizmoView = EditorUIBuilder.GetOrAddComponent<BattleTestSpawnPointGizmoView>(circleGo);
            var so = new SerializedObject(gizmoView);
            so.FindProperty("circleRenderer").objectReferenceValue = circleRenderer;
            var markersProperty = so.FindProperty("markers");
            markersProperty.arraySize = markers.Length;
            for (var i = 0; i < markers.Length; i++) markersProperty.GetArrayElementAtIndex(i).objectReferenceValue = markers[i];
            so.FindProperty("simulationRule").objectReferenceValue = simulationRule;
            so.ApplyModifiedProperties();

            return gizmoView;
        }

        private static TMP_InputField BuildLabeledInput(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, TMP_InputField.ContentType contentType = TMP_InputField.ContentType.IntegerNumber)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, name);
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), anchorMin, anchorMax);
            EditorUIBuilder.EnsureImage(go, new Color(1f, 1f, 1f, 0.95f));

            var labelGo = EditorUIBuilder.GetOrCreateUIObject(go.transform, "Label");
            EditorUIBuilder.SetAnchors(labelGo.GetComponent<RectTransform>(), new Vector2(0f, 0.55f), new Vector2(1f, 1f));
            var labelText = EditorUIBuilder.GetOrAddComponent<TextMeshProUGUI>(labelGo);
            labelText.text = label;
            labelText.fontSize = 14;
            labelText.color = Color.black;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.raycastTarget = false;

            var textAreaGo = EditorUIBuilder.GetOrCreateUIObject(go.transform, "TextArea");
            var textAreaRect = textAreaGo.GetComponent<RectTransform>();
            EditorUIBuilder.SetAnchors(textAreaRect, new Vector2(0f, 0f), new Vector2(1f, 0.55f));
            EditorUIBuilder.GetOrAddComponent<RectMask2D>(textAreaGo);

            var textGo = EditorUIBuilder.GetOrCreateUIObject(textAreaRect, "Text");
            EditorUIBuilder.SetStretch(textGo.GetComponent<RectTransform>());
            var textComponent = EditorUIBuilder.GetOrAddComponent<TextMeshProUGUI>(textGo);
            textComponent.fontSize = 16;
            textComponent.color = Color.black;
            textComponent.alignment = TextAlignmentOptions.MidlineLeft;
            textComponent.raycastTarget = false;

            var inputField = EditorUIBuilder.GetOrAddComponent<TMP_InputField>(go);
            inputField.textViewport = textAreaRect;
            inputField.textComponent = textComponent;
            inputField.contentType = contentType;

            return inputField;
        }

        // ==================== 적 구성 편집(요구사항 #6) ====================
        private static BattleTestEnemySetupView BuildEnemySetupPanel(Transform parent, BattleTestSimulationRule simulationRule)
        {
            var panelGo = EditorUIBuilder.GetOrCreateUIObject(parent, "EnemySetupPanel");
            EditorUIBuilder.SetAnchors(panelGo.GetComponent<RectTransform>(), new Vector2(0.80f, 0.76f), new Vector2(0.99f, 0.88f));
            EditorUIBuilder.EnsureImage(panelGo, new Color(1f, 0.8f, 0.8f, 0.4f));

            var marauderInput = BuildLabeledInput(panelGo.transform, "MarauderCountInput", "약탈자", new Vector2(0.02f, 0.05f), new Vector2(0.30f, 0.95f));
            var monsterInput = BuildLabeledInput(panelGo.transform, "MonsterCountInput", "괴수", new Vector2(0.34f, 0.05f), new Vector2(0.62f, 0.95f));
            var adversaryInput = BuildLabeledInput(panelGo.transform, "AdversaryCountInput", "적대자", new Vector2(0.66f, 0.05f), new Vector2(0.94f, 0.95f));

            var applyGo = EditorUIBuilder.GetOrCreateUIObject(parent, "EnemySetupApplyButton");
            EditorUIBuilder.SetAnchors(applyGo.GetComponent<RectTransform>(), new Vector2(0.80f, 0.71f), new Vector2(0.99f, 0.75f));
            EditorUIBuilder.EnsureImage(applyGo, new Color(0.7f, 1f, 0.7f, 1f));
            var applyButton = EditorUIBuilder.EnsureButton(applyGo);
            EditorUIBuilder.EnsureLabel(applyGo.transform, "적 구성 적용");

            var setupView = EditorUIBuilder.GetOrAddComponent<BattleTestEnemySetupView>(panelGo);
            var so = new SerializedObject(setupView);
            so.FindProperty("marauderCountInput").objectReferenceValue = marauderInput;
            so.FindProperty("monsterCountInput").objectReferenceValue = monsterInput;
            so.FindProperty("adversaryCountInput").objectReferenceValue = adversaryInput;
            so.FindProperty("applyButton").objectReferenceValue = applyButton;
            so.FindProperty("simulationRule").objectReferenceValue = simulationRule;
            so.ApplyModifiedProperties();

            return setupView;
        }

        // ==================== 사기 게이지바(요구사항 #5) ====================
        private static BattleTestMoraleGaugeView BuildMoraleGauge(Transform parent, BattleTestSimulationRule simulationRule)
        {
            var allyGaugeGo = BuildGaugeBar(parent, "AllyMoraleGauge", new Vector2(0.30f, 0.955f), new Vector2(0.49f, 0.985f), new Color(0.2f, 0.4f, 0.9f, 1f));
            var enemyGaugeGo = BuildGaugeBar(parent, "EnemyMoraleGauge", new Vector2(0.51f, 0.955f), new Vector2(0.70f, 0.985f), new Color(0.9f, 0.2f, 0.2f, 1f));

            var hostGo = EditorUIBuilder.GetOrCreateUIObject(parent, "MoraleGaugeHost");
            EditorUIBuilder.SetStretch(hostGo.GetComponent<RectTransform>());
            var gaugeView = EditorUIBuilder.GetOrAddComponent<BattleTestMoraleGaugeView>(hostGo);
            var so = new SerializedObject(gaugeView);
            so.FindProperty("allyFillImage").objectReferenceValue = allyGaugeGo;
            so.FindProperty("enemyFillImage").objectReferenceValue = enemyGaugeGo;
            so.FindProperty("simulationRule").objectReferenceValue = simulationRule;
            so.ApplyModifiedProperties();

            return gaugeView;
        }

        private static Image BuildGaugeBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color fillColor)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, name);
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), anchorMin, anchorMax);
            var background = EditorUIBuilder.EnsureImage(go, new Color(0.2f, 0.2f, 0.2f, 0.6f));
            background.raycastTarget = false;

            var fillGo = EditorUIBuilder.GetOrCreateUIObject(go.transform, "Fill");
            EditorUIBuilder.SetStretch(fillGo.GetComponent<RectTransform>());
            var fillImage = EditorUIBuilder.EnsureImage(fillGo, fillColor);
            fillImage.sprite = EditorUIBuilder.GetOrCreateSolidSprite();
            fillImage.raycastTarget = false;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillAmount = 1f;

            return fillImage;
        }

        // ==================== 결과 배지(전투 종료 시 화면 중앙 하단) ====================
        private static BattleTestResultPopupView BuildResultPopup(Transform parent)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, "ResultPopup");
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), new Vector2(0.42f, 0.03f), new Vector2(0.58f, 0.09f));
            var background = EditorUIBuilder.EnsureImage(go, new Color(0f, 0f, 0f, 0.7f));
            background.raycastTarget = false;

            var messageLabel = EditorUIBuilder.EnsureLabel(go.transform, string.Empty);
            messageLabel.color = Color.white;
            messageLabel.fontSize = 20;

            var popupView = EditorUIBuilder.GetOrAddComponent<BattleTestResultPopupView>(go);
            var so = new SerializedObject(popupView);
            so.FindProperty("messageLabel").objectReferenceValue = messageLabel;
            so.ApplyModifiedProperties();

            go.SetActive(false); // 평소에는 숨김 - 전투 종료 시 BattleTestResultPopupView.Show()가 켠다.
            return popupView;
        }

        // ==================== 유닛 정보/배치 취소 패널(세팅 단계 유닛 클릭 시) ====================
        private static BattleTestUnitInfoPanelView BuildUnitInfoPanel(Transform parent, BattleTestSimulationRule simulationRule)
        {
            var panelGo = EditorUIBuilder.GetOrCreateUIObject(parent, "UnitInfoPanel");
            EditorUIBuilder.SetAnchors(panelGo.GetComponent<RectTransform>(), new Vector2(0.72f, 0.14f), new Vector2(0.99f, 0.66f));
            EditorUIBuilder.EnsureImage(panelGo, new Color(0.15f, 0.15f, 0.15f, 0.9f));

            var titleGo = EditorUIBuilder.GetOrCreateUIObject(panelGo.transform, "Title");
            EditorUIBuilder.SetAnchors(titleGo.GetComponent<RectTransform>(), new Vector2(0.05f, 0.90f), new Vector2(0.95f, 0.99f));
            var titleLabel = EditorUIBuilder.GetOrAddComponent<TextMeshProUGUI>(titleGo);
            titleLabel.fontSize = 20;
            titleLabel.color = Color.white;
            titleLabel.alignment = TextAlignmentOptions.Center;
            titleLabel.raycastTarget = false;

            // 7개 수치 입력칸을 세로로 스택 - 전부 소수 입력 가능해야 한다(이동속도/공격주기 등).
            const int rowCount = 7;
            var maxHpInput = BuildStatRow(panelGo.transform, "MaxHpInput", "체력", 0);
            var attackInput = BuildStatRow(panelGo.transform, "AttackInput", "공격력", 1);
            var defenseInput = BuildStatRow(panelGo.transform, "DefenseInput", "방어력", 2);
            var moveSpeedInput = BuildStatRow(panelGo.transform, "MoveSpeedInput", "이동속도", 3);
            var attackIntervalInput = BuildStatRow(panelGo.transform, "AttackIntervalInput", "공격주기", 4);
            var rangeInput = BuildStatRow(panelGo.transform, "RangeInput", "사거리", 5);
            var moraleSyncRateInput = BuildStatRow(panelGo.transform, "MoraleSyncRateInput", "사기동기화", 6);

            var applyGo = EditorUIBuilder.GetOrCreateUIObject(panelGo.transform, "ApplyButton");
            EditorUIBuilder.SetAnchors(applyGo.GetComponent<RectTransform>(), new Vector2(0.05f, 0.02f), new Vector2(0.32f, 0.09f));
            EditorUIBuilder.EnsureImage(applyGo, new Color(0.7f, 1f, 0.7f, 1f));
            var applyButton = EditorUIBuilder.EnsureButton(applyGo);
            EditorUIBuilder.EnsureLabel(applyGo.transform, "적용");

            var cancelGo = EditorUIBuilder.GetOrCreateUIObject(panelGo.transform, "CancelPlacementButton");
            EditorUIBuilder.SetAnchors(cancelGo.GetComponent<RectTransform>(), new Vector2(0.36f, 0.02f), new Vector2(0.63f, 0.09f));
            EditorUIBuilder.EnsureImage(cancelGo, new Color(0.95f, 0.5f, 0.5f, 1f));
            var cancelButton = EditorUIBuilder.EnsureButton(cancelGo);
            EditorUIBuilder.EnsureLabel(cancelGo.transform, "배치 취소");

            var closeGo = EditorUIBuilder.GetOrCreateUIObject(panelGo.transform, "CloseButton");
            EditorUIBuilder.SetAnchors(closeGo.GetComponent<RectTransform>(), new Vector2(0.67f, 0.02f), new Vector2(0.94f, 0.09f));
            EditorUIBuilder.EnsureImage(closeGo, new Color(0.85f, 0.85f, 0.85f, 1f));
            var closeButton = EditorUIBuilder.EnsureButton(closeGo);
            EditorUIBuilder.EnsureLabel(closeGo.transform, "닫기");

            var panelView = EditorUIBuilder.GetOrAddComponent<BattleTestUnitInfoPanelView>(panelGo);
            var so = new SerializedObject(panelView);
            so.FindProperty("titleLabel").objectReferenceValue = titleLabel;
            so.FindProperty("maxHpInput").objectReferenceValue = maxHpInput;
            so.FindProperty("attackInput").objectReferenceValue = attackInput;
            so.FindProperty("defenseInput").objectReferenceValue = defenseInput;
            so.FindProperty("moveSpeedInput").objectReferenceValue = moveSpeedInput;
            so.FindProperty("attackIntervalInput").objectReferenceValue = attackIntervalInput;
            so.FindProperty("rangeInput").objectReferenceValue = rangeInput;
            so.FindProperty("moraleSyncRateInput").objectReferenceValue = moraleSyncRateInput;
            so.FindProperty("applyButton").objectReferenceValue = applyButton;
            so.FindProperty("cancelPlacementButton").objectReferenceValue = cancelButton;
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            so.FindProperty("simulationRule").objectReferenceValue = simulationRule;
            so.ApplyModifiedProperties();

            panelGo.SetActive(false); // 평소에는 숨김 - 세팅 단계 유닛을 클릭하면 Show()가 켠다.
            return panelView;

            // 로컬 함수 - rowCount는 세로 칸 분할용, 위쪽(Title) 여백 0.10만큼 뺀 나머지를 균등 분배.
            TMP_InputField BuildStatRow(Transform statParent, string name, string label, int rowIndex)
            {
                const float top = 0.86f;
                const float bottom = 0.12f;
                var rowHeight = (top - bottom) / rowCount;
                var rowTop = top - rowIndex * rowHeight;
                var rowBottom = rowTop - rowHeight + 0.01f;
                return BuildLabeledInput(statParent, name, label, new Vector2(0.05f, rowBottom), new Vector2(0.95f, rowTop), TMP_InputField.ContentType.DecimalNumber);
            }
        }

        // ==================== 스폰 포인트 예약 패널(마커 클릭 시, 신규 요구사항) ====================
        // 화면 왼쪽 중단(다른 패널이 없는 빈 공간)에 배치 - 좌상단은 아군 팔레트+대열 범위 패널,
        // 우상단은 적 팔레트+적 구성 패널이 이미 차지하고 있다.
        private static BattleTestSpawnPointPanelView BuildSpawnPointPanel(Transform parent, BattleTestSimulationRule simulationRule)
        {
            var panelGo = EditorUIBuilder.GetOrCreateUIObject(parent, "SpawnPointPanel");
            EditorUIBuilder.SetAnchors(panelGo.GetComponent<RectTransform>(), new Vector2(0.02f, 0.35f), new Vector2(0.26f, 0.62f));
            EditorUIBuilder.EnsureImage(panelGo, new Color(0.15f, 0.15f, 0.15f, 0.9f));

            // 구버전(타입 버튼 4개 + 닫기)에서 "타입별 개수 입력"으로 구조가 바뀌어, 재실행 시 옛
            // 버튼 오브젝트가 안 지워지고 남는다.
            EditorUIBuilder.DestroyChildIfExists(panelGo.transform, "MarauderButton");
            EditorUIBuilder.DestroyChildIfExists(panelGo.transform, "MonsterButton");
            EditorUIBuilder.DestroyChildIfExists(panelGo.transform, "AdversaryButton");
            EditorUIBuilder.DestroyChildIfExists(panelGo.transform, "ClearButton");

            var titleGo = EditorUIBuilder.GetOrCreateUIObject(panelGo.transform, "Title");
            EditorUIBuilder.SetAnchors(titleGo.GetComponent<RectTransform>(), new Vector2(0.05f, 0.86f), new Vector2(0.95f, 0.99f));
            var titleLabel = EditorUIBuilder.GetOrAddComponent<TextMeshProUGUI>(titleGo);
            titleLabel.fontSize = 16;
            titleLabel.color = Color.white;
            titleLabel.alignment = TextAlignmentOptions.Center;
            titleLabel.raycastTarget = false;

            // 적 구성 편집 패널(BuildEnemySetupPanel)과 같은 "타입별 개수 입력" 패턴 - 한 스폰
            // 포인트에서 여러 타입/여러 마리를 동시에 예약할 수 있어야 한다(사용자 요청).
            var marauderInput = BuildLabeledInput(panelGo.transform, "MarauderCountInput", "약탈자", new Vector2(0.03f, 0.66f), new Vector2(0.31f, 0.82f));
            var monsterInput = BuildLabeledInput(panelGo.transform, "MonsterCountInput", "괴수", new Vector2(0.35f, 0.66f), new Vector2(0.63f, 0.82f));
            var adversaryInput = BuildLabeledInput(panelGo.transform, "AdversaryCountInput", "적대자", new Vector2(0.67f, 0.66f), new Vector2(0.95f, 0.82f));

            var applyButton = BuildActionButton(panelGo.transform, "ApplyButton", "적용", new Vector2(0.05f, 0.44f), new Vector2(0.95f, 0.60f), new Color(0.7f, 1f, 0.7f, 1f));
            var closeButton = BuildActionButton(panelGo.transform, "CloseButton", "닫기", new Vector2(0.05f, 0.24f), new Vector2(0.95f, 0.40f), new Color(0.7f, 0.7f, 0.7f, 1f));

            var panelView = EditorUIBuilder.GetOrAddComponent<BattleTestSpawnPointPanelView>(panelGo);
            var so = new SerializedObject(panelView);
            so.FindProperty("titleLabel").objectReferenceValue = titleLabel;
            so.FindProperty("marauderCountInput").objectReferenceValue = marauderInput;
            so.FindProperty("monsterCountInput").objectReferenceValue = monsterInput;
            so.FindProperty("adversaryCountInput").objectReferenceValue = adversaryInput;
            so.FindProperty("applyButton").objectReferenceValue = applyButton;
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            so.FindProperty("simulationRule").objectReferenceValue = simulationRule;
            so.ApplyModifiedProperties();

            panelGo.SetActive(false); // 평소에는 숨김 - 스폰 포인트 마커를 클릭하면 Show()가 켠다.
            return panelView;
        }

        // Bootstrap.unity의 LiveBattleSimulationRule.roleGroupMap과 같은 에셋 - 인스톨러가 자동
        // 연결한다(에셋 하나뿐이라 경로가 고정적, 안 하면 방향성 지시 없이 폴백 동작만 검증된다).
        private static void WireRoleGroupMap(BattleTestSimulationRule rule)
        {
            var asset = AssetDatabase.LoadAssetAtPath<MercenaryRoleGroupMapAsset>(RoleGroupMapAssetPath);
            if (asset == null)
            {
                Debug.LogWarning($"'{RoleGroupMapAssetPath}'를 찾을 수 없어 roleGroupMap을 연결하지 못했다.");
                return;
            }

            var so = new SerializedObject(rule);
            so.FindProperty("roleGroupMap").objectReferenceValue = asset;
            so.ApplyModifiedProperties();
        }

        private static void WireTacticsCatalog(InMemoryTacticsRepository repository)
        {
            var asset = LoadTacticsCatalogAsset();
            if (asset == null)
            {
                return;
            }

            var so = new SerializedObject(repository);
            so.FindProperty("catalog").objectReferenceValue = asset;
            so.ApplyModifiedProperties();
        }

        // TacticsPanel도 InMemoryTacticsRepository와 별개로 자신만의 catalog 필드를 갖는다(같은 에셋을
        // 각자 참조 - RoleGroupTacticsOverride 후보 UI를 그리는 데 필요).
        private static void WireTacticsPanelCatalog(TacticsPanel panel)
        {
            var asset = LoadTacticsCatalogAsset();
            if (asset == null)
            {
                return;
            }

            var so = new SerializedObject(panel);
            so.FindProperty("catalog").objectReferenceValue = asset;
            so.ApplyModifiedProperties();
        }

        private static RoleGroupTacticsCatalogAsset LoadTacticsCatalogAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<RoleGroupTacticsCatalogAsset>(TacticsCatalogAssetPath);
            if (asset == null)
            {
                Debug.LogWarning($"'{TacticsCatalogAssetPath}'를 찾을 수 없어 catalog를 연결하지 못했다.");
            }
            return asset;
        }

        private static void WireBattleTestController(
            BattleTestController controller, Button startButton, Button pauseButton, Button resetButton, Button tacticsButton,
            BattleWorldRoot battleWorldRoot, BattleFieldWorldCameraView cameraView, BattleTestUnitPaletteView paletteView,
            BattleTestResultPopupView resultPopupView, BattleTestUnitInfoPanelView unitInfoPanelView, BattleTestUnitPickerView unitPickerView,
            BattleTestSpawnPointPanelView spawnPointPanelView, BattleTestSimulationRule simulationRule)
        {
            var so = new SerializedObject(controller);
            so.FindProperty("startBattleButton").objectReferenceValue = startButton;
            so.FindProperty("pauseButton").objectReferenceValue = pauseButton;
            so.FindProperty("resetButton").objectReferenceValue = resetButton;
            so.FindProperty("tacticsButton").objectReferenceValue = tacticsButton;
            so.FindProperty("allyContainer").objectReferenceValue = battleWorldRoot.transform.Find("AllyLayer");
            so.FindProperty("enemyContainer").objectReferenceValue = battleWorldRoot.transform.Find("EnemyLayer");
            so.FindProperty("characterViewPrefab").objectReferenceValue = EditorUIBuilder.GetOrCreateBattleCharacterViewPrefab();
            so.FindProperty("protectedViewPrefab").objectReferenceValue = EditorUIBuilder.GetOrCreateBattleProtectedViewPrefab();
            so.FindProperty("cameraView").objectReferenceValue = cameraView;
            so.FindProperty("backgroundView").objectReferenceValue = battleWorldRoot.GetComponent<BattleBackgroundGridView>();
            so.FindProperty("paletteView").objectReferenceValue = paletteView;
            so.FindProperty("resultPopupView").objectReferenceValue = resultPopupView;
            so.FindProperty("unitInfoPanelView").objectReferenceValue = unitInfoPanelView;
            so.FindProperty("unitPickerView").objectReferenceValue = unitPickerView;
            so.FindProperty("spawnPointPanelView").objectReferenceValue = spawnPointPanelView;
            so.FindProperty("battleTestSimulation").objectReferenceValue = simulationRule;
            so.ApplyModifiedProperties();
        }

        private static void SyncManagedComponents(DependencyManager dependencyManager, MonoBehaviour[] managedComponents)
        {
            var so = new SerializedObject(dependencyManager);
            var managedComponentsProperty = so.FindProperty("managedComponents");

            managedComponentsProperty.arraySize = managedComponents.Length;
            for (var i = 0; i < managedComponents.Length; i++)
            {
                managedComponentsProperty.GetArrayElementAtIndex(i).objectReferenceValue = managedComponents[i];
            }

            so.ApplyModifiedProperties();
        }
    }
}
