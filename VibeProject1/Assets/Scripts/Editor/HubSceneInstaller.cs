using Game.Core;
using Game.Core.DebugTools;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.Editor
{
    /// <summary>
    /// Hub 씬 UI 전체(Hub↔Field 씬 전환 연출용 ContentRoot, 그 안의 배치 UI/상행 준비 UI)를 한 번에
    /// 생성/동기화한다. 원래 FormationUIInstaller/TripUIInstaller/HubUIInstaller 세 메뉴로 나뉘어
    /// 있었는데, 실행 순서(ContentRoot 생성 → 그 안에 배치/상행 준비 UI 생성)에 의존하는 데다 Hub UI를
    /// 바꿀 때마다 항상 셋 다 같이 실행해야 해서 실용적 의미가 없어 하나로 합쳤다. ContentRoot를 먼저
    /// 만들고 배치/상행 준비 UI를 처음부터 그 안에 직접 생성한다 - 다 만든 뒤 사후에 재배치하면
    /// get-or-create 조회(Transform.Find, 직계 자식만 검색)가 재실행 시 옮겨지기 전 위치를 찾다가
    /// 실패해 매번 중복 생성하는 버그로 이어진다(과거에 실제로 겪음). 씬 YAML 수작업 편집 대신 이
    /// 도구로 재현 가능하게 만든다.
    /// </summary>
    public static class HubSceneInstaller
    {
        private const float MapContentSize = 2400f;
        private const string TripPrefabFolder = "Assets/Prefabs/UI/Trip";
        private const string CityMarkerPrefabPath = TripPrefabFolder + "/TripDebugCityMarker.prefab";
        private const string RoadLinePrefabPath = TripPrefabFolder + "/TripDebugRoadLine.prefab";

        [MenuItem("Tools/Game/Build Hub Scene")]
        public static void BuildHubScene()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.name != SceneNames.Hub)
            {
                Debug.LogError($"'{SceneNames.Hub}' 씬이 활성 씬이어야 한다. 현재 활성 씬: '{activeScene.name}'. Hub.unity를 열고 다시 실행하라.");
                return;
            }

            var sceneUIRoot = Object.FindFirstObjectByType<SceneUIRoot>(FindObjectsInactive.Include);
            if (sceneUIRoot == null)
            {
                Debug.LogError($"씬에서 {nameof(SceneUIRoot)}를 찾을 수 없다.");
                return;
            }

            // 콘텐츠 씬마다 자체 EventSystem이 있어야 클릭이 동작한다(FieldUIInstaller와 동일한 안전장치 -
            // EditorUIBuilder.EnsureSceneEventSystem 주석 참고).
            EditorUIBuilder.EnsureSceneEventSystem(activeScene);

            // ContentRoot를 먼저 만들고 배치/상행 UI를 그 "안"에 직접 생성한다(사후 재배치가 아니다) -
            // GetOrCreateUIObject의 get-or-create 조회는 직계 자식만 본다(Transform.Find). 예전에는
            // sceneUIRoot 바로 아래에 만든 뒤 나중에 ContentRoot로 옮겼는데, 그러면 재실행 시 옮겨지기
            // 전 위치(sceneUIRoot 직계)에서는 더 이상 못 찾아 매번 새로 하나씩 더 만들어졌다 - 마커 ID
            // 중복 등록 경고(SceneUIRoot.Awake)로 이어졌던 버그. 처음부터 최종 위치에 만들면 이 문제가
            // 구조적으로 발생하지 않는다.
            var contentRoot = EnsureContentRoot(sceneUIRoot);

            // 위 구조 변경 이전 버전이 sceneUIRoot 바로 아래에 남겨뒀을 수 있는 중복 오브젝트를 정리한다
            // (재실행 안전성 - 한 번만 정리되면 이후에는 항상 no-op). 문제가 있던 버전을 여러 번
            // 재실행했다면 같은 이름의 직계 자식이 여러 개 쌓여있을 수 있어(Transform.Find는 첫 번째
            // 매치만 반환) 전부 찾아서 지운다.
            DestroyAllDirectChildrenNamed(sceneUIRoot.transform, "FormationPanel");
            DestroyAllDirectChildrenNamed(sceneUIRoot.transform, "TripPanel");

            BuildFormationUI(contentRoot);
            BuildTripUI(contentRoot);

            EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log("Hub Scene UI 생성/동기화 완료. 씬을 저장(Ctrl+S)해야 변경사항이 파일에 반영된다. "
                + "FormationPanel.dragGhostPrefab에는 'Assets/Prefabs/UI/Formation/FormationUnitIcon.prefab'을, "
                + $"TripPanel.debugCityMarkerPrefab에는 '{CityMarkerPrefabPath}', debugRoadLinePrefab에는 '{RoadLinePrefabPath}'를 "
                + "수동으로 연결하라(FormationPanel/TripPanel은 Bootstrap 씬에 있어 이 도구가 직접 연결할 수 없다).");
        }

        // ==================== 배치(Formation) UI ====================
        // 실제 조립 로직은 FormationUIBuilder(Hub/Field 공용)에 있다 - Field 씬에서도 "정비창 재호출"이
        // 동작하려면 같은 화면이 필요해서 공용화되어 있다(FieldUIInstaller 참고).
        private static void BuildFormationUI(Transform contentRoot)
        {
            FormationUIBuilder.EnsurePrefabFolder();
            var slotPrefab = FormationUIBuilder.GetOrCreateSlotPrefab();
            var iconPrefab = FormationUIBuilder.GetOrCreateIconPrefab();
            FormationUIBuilder.Build(contentRoot, slotPrefab, iconPrefab);
        }

        // ==================== 상행 준비(Trip) UI ====================
        private static void BuildTripUI(Transform contentRoot)
        {
            EnsureTripPrefabFolder();
            GetOrCreateCityMarkerPrefab(); // TripPanel.debugCityMarkerPrefab에 수동 연결 대상(에셋만 미리 생성)
            GetOrCreateRoadLinePrefab();   // TripPanel.debugRoadLinePrefab에 수동 연결 대상(에셋만 미리 생성)

            var panelRoot = EditorUIBuilder.GetOrCreateUIObject(contentRoot, "TripPanel");
            EditorUIBuilder.SetStretch(panelRoot.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureMarker(panelRoot, TripUIElementIds.PanelRoot);

            BuildTripTopButtons(panelRoot.transform);
            BuildTripMap(panelRoot.transform);
            BuildTripLocationInfo(panelRoot.transform, "OriginInfo", TripUIElementIds.OriginInfoRoot, new Vector2(0.64f, 0.64f), new Vector2(0.94f, 0.88f));
            BuildTripLocationInfo(panelRoot.transform, "DestinationInfo", TripUIElementIds.DestinationInfoRoot, new Vector2(0.64f, 0.40f), new Vector2(0.94f, 0.63f));
            BuildTripSummary(panelRoot.transform);
            BuildTripStartButton(panelRoot.transform);
            BuildTripDebugMapControls(panelRoot.transform);

            panelRoot.SetActive(false);
        }

        private static void BuildTripTopButtons(Transform parent)
        {
            var closeGo = EditorUIBuilder.GetOrCreateUIObject(parent, "CloseButton");
            EditorUIBuilder.SetAnchors(closeGo.GetComponent<RectTransform>(), new Vector2(0.70f, 0.90f), new Vector2(0.79f, 0.97f));
            EditorUIBuilder.EnsureImage(closeGo, new Color(0.85f, 0.85f, 0.85f, 1f));
            EditorUIBuilder.EnsureButton(closeGo);
            EditorUIBuilder.EnsureLabel(closeGo.transform, "닫기");
            EditorUIBuilder.EnsureMarker(closeGo, TripUIElementIds.CloseButton);

            var formationGo = EditorUIBuilder.GetOrCreateUIObject(parent, "OpenFormationButton");
            EditorUIBuilder.SetAnchors(formationGo.GetComponent<RectTransform>(), new Vector2(0.80f, 0.90f), new Vector2(0.89f, 0.97f));
            EditorUIBuilder.EnsureImage(formationGo, new Color(0.75f, 0.87f, 1f, 1f));
            EditorUIBuilder.EnsureButton(formationGo);
            EditorUIBuilder.EnsureLabel(formationGo.transform, "배치");
            EditorUIBuilder.EnsureMarker(formationGo, TripUIElementIds.OpenFormationButton);
        }

        private static void BuildTripMap(Transform parent)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(parent, "Map");
            EditorUIBuilder.SetAnchors(root.GetComponent<RectTransform>(), new Vector2(0.06f, 0.16f), new Vector2(0.62f, 0.88f));
            EditorUIBuilder.EnsureImage(root, new Color(0.85f, 0.9f, 0.85f, 1f));
            EditorUIBuilder.EnsureMarker(root, TripUIElementIds.MapRoot);

            var (viewport, contentGo) = EditorUIBuilder.CreateViewportAndContent(root.transform);
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta = new Vector2(MapContentSize, MapContentSize);
            contentRect.anchoredPosition = Vector2.zero;
            EditorUIBuilder.EnsureImage(contentGo, new Color(0.55f, 0.75f, 0.55f, 1f));

            // 예전에는 고정 출발/도착 핀(사각형)이었으나, 지도 위에 자유 배치되는 디버그 도시 아이콘이
            // 그 역할을 대신하게 되면서 더 이상 쓰이지 않는다 - 남아있던 옛 오브젝트를 정리한다.
            EditorUIBuilder.DestroyChildIfExists(contentRect, "OriginPin");
            EditorUIBuilder.DestroyChildIfExists(contentRect, "DestinationPin");

            EditorUIBuilder.ConfigureScrollRect(root, viewport, contentRect, horizontal: true, vertical: true);

            // ScrollRect는 기본적으로 마우스 휠도 자체적으로 패닝(스크롤)에 쓴다. 같은 오브젝트의
            // TripMapView.OnScroll(확대/축소)도 동시에 반응해 휠을 돌리면 줌과 스크롤이 함께 발동됐다.
            // scrollSensitivity를 0으로 두면 ScrollRect 자신의 휠 반응만 꺼지고(드래그 패닝은 별개 경로라
            // 그대로 유지), 휠은 온전히 TripMapView의 줌 전용이 된다.
            var mapScrollRect = root.GetComponent<ScrollRect>();
            mapScrollRect.scrollSensitivity = 0f;
            // TripMapView.Awake도 런타임에 같은 값을 강제하지만(09번 설계 §6), 인스톨러가 씬 상태를
            // 명시적으로 보장하는 기존 관례를 따라 여기서도 설정해 둔다.
            mapScrollRect.inertia = false;

            EditorUIBuilder.GetOrAddComponent<TripMapView>(root);
        }

        private static void BuildTripLocationInfo(Transform parent, string objectName, string markerId, Vector2 anchorMin, Vector2 anchorMax)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(parent, objectName);
            EditorUIBuilder.SetAnchors(root.GetComponent<RectTransform>(), anchorMin, anchorMax);
            EditorUIBuilder.EnsureImage(root, new Color(1f, 0.9f, 0.78f, 1f));
            EditorUIBuilder.EnsureMarker(root, markerId);

            var iconGo = EditorUIBuilder.GetOrCreateUIObject(root.transform, "Icon");
            EditorUIBuilder.SetAnchors(iconGo.GetComponent<RectTransform>(), new Vector2(0.06f, 0.55f), new Vector2(0.32f, 0.92f));
            var iconImage = EditorUIBuilder.EnsureImage(iconGo, Color.white);
            iconImage.preserveAspect = true;

            var nameGo = EditorUIBuilder.GetOrCreateUIObject(root.transform, "NameLabel");
            EditorUIBuilder.SetAnchors(nameGo.GetComponent<RectTransform>(), new Vector2(0.36f, 0.55f), new Vector2(0.96f, 0.92f));
            var nameLabel = EditorUIBuilder.GetOrAddComponent<TextMeshProUGUI>(nameGo);
            nameLabel.fontSize = 20;
            nameLabel.color = Color.black;
            nameLabel.alignment = TextAlignmentOptions.MidlineLeft;
            nameLabel.raycastTarget = false;

            var descriptionGo = EditorUIBuilder.GetOrCreateUIObject(root.transform, "DescriptionLabel");
            EditorUIBuilder.SetAnchors(descriptionGo.GetComponent<RectTransform>(), new Vector2(0.06f, 0.06f), new Vector2(0.96f, 0.48f));
            var descriptionLabel = EditorUIBuilder.GetOrAddComponent<TextMeshProUGUI>(descriptionGo);
            descriptionLabel.fontSize = 16;
            descriptionLabel.color = Color.black;
            descriptionLabel.raycastTarget = false;

            var infoView = EditorUIBuilder.GetOrAddComponent<TripLocationInfoView>(root);
            var so = new SerializedObject(infoView);
            so.FindProperty("iconImage").objectReferenceValue = iconImage;
            so.FindProperty("nameText").objectReferenceValue = nameLabel;
            so.FindProperty("descriptionText").objectReferenceValue = descriptionLabel;
            so.ApplyModifiedProperties();
        }

        private static void BuildTripSummary(Transform parent)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(parent, "Summary");
            EditorUIBuilder.SetAnchors(root.GetComponent<RectTransform>(), new Vector2(0.64f, 0.16f), new Vector2(0.94f, 0.39f));
            EditorUIBuilder.EnsureImage(root, new Color(0.9f, 0.9f, 0.96f, 1f));
            EditorUIBuilder.EnsureMarker(root, TripUIElementIds.SummaryRoot);

            var durationLabel = BuildTripSummaryRow(root.transform, "DurationDistanceLabel", 0);
            var dangerLabel = BuildTripSummaryRow(root.transform, "DangerLabel", 1);
            var formationLabel = BuildTripSummaryRow(root.transform, "FormationSummaryLabel", 2);
            var rewardLabel = BuildTripSummaryRow(root.transform, "RewardLabel", 3);

            var summaryView = EditorUIBuilder.GetOrAddComponent<TripSummaryView>(root);
            var so = new SerializedObject(summaryView);
            so.FindProperty("durationDistanceText").objectReferenceValue = durationLabel;
            so.FindProperty("dangerText").objectReferenceValue = dangerLabel;
            so.FindProperty("formationSummaryText").objectReferenceValue = formationLabel;
            so.FindProperty("rewardText").objectReferenceValue = rewardLabel;
            so.ApplyModifiedProperties();
        }

        private static TextMeshProUGUI BuildTripSummaryRow(Transform parent, string name, int rowIndex)
        {
            const int rowCount = 4;
            const float rowHeight = 1f / rowCount;
            var top = 1f - rowIndex * rowHeight;
            var bottom = top - rowHeight;

            var go = EditorUIBuilder.GetOrCreateUIObject(parent, name);
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), new Vector2(0.06f, bottom + 0.02f), new Vector2(0.94f, top - 0.02f));
            var label = EditorUIBuilder.GetOrAddComponent<TextMeshProUGUI>(go);
            label.fontSize = 16;
            label.color = Color.black;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            return label;
        }

        private static void BuildTripStartButton(Transform parent)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, "StartButton");
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), new Vector2(0.30f, 0.03f), new Vector2(0.70f, 0.14f));
            EditorUIBuilder.EnsureImage(go, new Color(0.71f, 0.32f, 0.03f, 1f));
            EditorUIBuilder.EnsureButton(go);
            var label = EditorUIBuilder.EnsureLabel(go.transform, "상행 시작");
            label.fontSize = 30;
            label.color = Color.white;
            label.fontStyle = FontStyles.Bold;
            EditorUIBuilder.EnsureMarker(go, TripUIElementIds.StartButton);
        }

        /// <summary>
        /// 지도 위 디버그 도시 배치/경로 연결(03/04번 기획 문서) 전용 컨트롤. 상단 여백(닫기/배치
        /// 버튼과 같은 줄, 그 왼쪽)에 작게 배치한다 - 원래 지도 아래에 뒀더니 상행 시작 버튼과
        /// 겹쳤다. 정식 콘텐츠가 아니므로 실제 지역 시스템이 생기면 이 메서드와 관련 프리팹 생성
        /// 로직을 통째로 제거한다(DEBUG_FEATURES.md §2 참고).
        /// </summary>
        private static void BuildTripDebugMapControls(Transform parent)
        {
            const float top = 0.90f;
            const float bottom = 0.97f;

            var paletteGo = EditorUIBuilder.GetOrCreateUIObject(parent, "DebugCityPalette");
            EditorUIBuilder.SetAnchors(paletteGo.GetComponent<RectTransform>(), new Vector2(0.06f, top), new Vector2(0.12f, bottom));
            EditorUIBuilder.EnsureMarker(paletteGo, TripUIElementIds.DebugCityPaletteRoot);
            var paletteIcon = EditorUIBuilder.EnsureImage(paletteGo, Color.white);
            paletteIcon.sprite = FormationPlaceholderIcons.GetOrCreateCircle();
            paletteIcon.preserveAspect = true;

            var paletteView = EditorUIBuilder.GetOrAddComponent<TripDebugCityPaletteView>(paletteGo);
            var paletteSo = new SerializedObject(paletteView);
            paletteSo.FindProperty("iconImage").objectReferenceValue = paletteIcon;
            paletteSo.ApplyModifiedProperties();

            var toggleGo = EditorUIBuilder.GetOrCreateUIObject(parent, "DebugRoadToggleButton");
            EditorUIBuilder.SetAnchors(toggleGo.GetComponent<RectTransform>(), new Vector2(0.14f, top), new Vector2(0.30f, bottom));
            EditorUIBuilder.EnsureImage(toggleGo, new Color(0.95f, 0.85f, 0.6f, 1f));
            EditorUIBuilder.EnsureButton(toggleGo);
            var toggleLabel = EditorUIBuilder.EnsureLabel(toggleGo.transform, "경로 연결: OFF");
            toggleLabel.fontSize = 11;
            EditorUIBuilder.EnsureMarker(toggleGo, TripUIElementIds.DebugRoadToggleButton);

            var toggleView = EditorUIBuilder.GetOrAddComponent<TripDebugRoadToggleView>(toggleGo);
            var toggleSo = new SerializedObject(toggleView);
            toggleSo.FindProperty("toggleButton").objectReferenceValue = toggleGo.GetComponent<Button>();
            toggleSo.FindProperty("label").objectReferenceValue = toggleLabel;
            toggleSo.ApplyModifiedProperties();

            var cityDeleteGo = EditorUIBuilder.GetOrCreateUIObject(parent, "DebugCityBulkDeleteButton");
            EditorUIBuilder.SetAnchors(cityDeleteGo.GetComponent<RectTransform>(), new Vector2(0.32f, top), new Vector2(0.46f, bottom));
            EditorUIBuilder.EnsureImage(cityDeleteGo, new Color(0.9f, 0.6f, 0.6f, 1f));
            EditorUIBuilder.EnsureButton(cityDeleteGo);
            var cityDeleteLabel = EditorUIBuilder.EnsureLabel(cityDeleteGo.transform, "도시 전체삭제");
            cityDeleteLabel.fontSize = 10;
            EditorUIBuilder.EnsureMarker(cityDeleteGo, TripUIElementIds.DebugCityBulkDeleteButton);

            var roadDeleteGo = EditorUIBuilder.GetOrCreateUIObject(parent, "DebugRoadBulkDeleteButton");
            EditorUIBuilder.SetAnchors(roadDeleteGo.GetComponent<RectTransform>(), new Vector2(0.48f, top), new Vector2(0.62f, bottom));
            EditorUIBuilder.EnsureImage(roadDeleteGo, new Color(0.9f, 0.6f, 0.6f, 1f));
            EditorUIBuilder.EnsureButton(roadDeleteGo);
            var roadDeleteLabel = EditorUIBuilder.EnsureLabel(roadDeleteGo.transform, "경로 전체삭제");
            roadDeleteLabel.fontSize = 10;
            EditorUIBuilder.EnsureMarker(roadDeleteGo, TripUIElementIds.DebugRoadBulkDeleteButton);
        }

        private static void EnsureTripPrefabFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
            }
            if (!AssetDatabase.IsValidFolder(TripPrefabFolder))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs/UI", "Trip");
            }
        }

        private static TripDebugCityMarkerView GetOrCreateCityMarkerPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CityMarkerPrefabPath);
            if (existing != null)
            {
                return existing.GetComponent<TripDebugCityMarkerView>();
            }

            var go = new GameObject("TripDebugCityMarker", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(48f, 48f);

            var image = go.AddComponent<Image>();
            image.sprite = FormationPlaceholderIcons.GetOrCreateCircle();
            image.color = Color.white;
            image.raycastTarget = true;

            var markerView = go.AddComponent<TripDebugCityMarkerView>();
            var so = new SerializedObject(markerView);
            so.FindProperty("iconImage").objectReferenceValue = image;
            so.ApplyModifiedProperties();

            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(go, CityMarkerPrefabPath);
            Object.DestroyImmediate(go);

            return savedPrefab.GetComponent<TripDebugCityMarkerView>();
        }

        private static TripDebugRoadLineView GetOrCreateRoadLinePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(RoadLinePrefabPath);
            if (existing != null)
            {
                // 기존 프리팹이라도 최신 필드 연결 상태로 동기화한다(재실행 안전성) - lineImage 연결이
                // 나중에 추가됐으므로, 예전에 생성된 프리팹에는 누락돼 있을 수 있다.
                var existingView = existing.GetComponent<TripDebugRoadLineView>();
                var existingSo = new SerializedObject(existingView);
                existingSo.FindProperty("lineImage").objectReferenceValue = existing.GetComponent<Image>();
                existingSo.ApplyModifiedProperties();
                return existingView;
            }

            var go = new GameObject("TripDebugRoadLine", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(100f, 6f);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.3f, 0.3f, 0.3f, 0.9f);
            image.raycastTarget = true;

            var lineView = go.AddComponent<TripDebugRoadLineView>();
            var so = new SerializedObject(lineView);
            so.FindProperty("lineImage").objectReferenceValue = image;
            so.ApplyModifiedProperties();

            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(go, RoadLinePrefabPath);
            Object.DestroyImmediate(go);

            return savedPrefab.GetComponent<TripDebugRoadLineView>();
        }

        // ==================== Hub↔Field 전환 연출용 ContentRoot ====================
        // 슬라이드 대상은 "Hub 배경"이 아니라 "그 순간 화면에 보이는 Hub 콘텐츠 전체"여야 한다
        // (Docs/설계/10_씬전환_연출_아키텍처.md §9). 배치/상행 준비 UI는 이 메서드가 반환하는 Transform
        // 안에 처음부터 직접 생성되므로(BuildFormationUI/BuildTripUI 참고) 별도 재배치가 필요 없다.
        // Background/버튼은 이 도구가 만드는 게 아니라 Hub 씬에 이미 존재하는 요소라 여전히 찾아서
        // 옮겨야 한다.
        private static Transform EnsureContentRoot(SceneUIRoot sceneUIRoot)
        {
            var contentRootGo = EditorUIBuilder.GetOrCreateUIObject(sceneUIRoot.transform, "ContentRoot");
            EditorUIBuilder.SetStretch(contentRootGo.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureMarker(contentRootGo, HubUIElementIds.ContentRoot);

            // ContentRoot는 Hub의 배경 레이어다 - 다른 콘텐츠 씬 오브젝트보다 항상 아래(렌더링 순서상
            // 뒤)에 있어야 한다. get-or-create로 새로 만들면 sceneUIRoot의 마지막 자식으로 추가되는데,
            // 그러면 다른 오브젝트보다 위에 그려질 수 있다 - 매번 첫 번째 자식으로 고정한다.
            contentRootGo.transform.SetAsFirstSibling();

            ReparentIfFound(sceneUIRoot, HubUIElementIds.Background, contentRootGo.transform);
            ReparentIfFound(sceneUIRoot, HubUIElementIds.DepartureButton, contentRootGo.transform);
            ReparentIfFound(sceneUIRoot, HubUIElementIds.FormationButton, contentRootGo.transform);

            return contentRootGo.transform;
        }

        private static void DestroyAllDirectChildrenNamed(Transform parent, string name)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child.name == name)
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }
        }

        private static void ReparentIfFound(SceneUIRoot sceneUIRoot, string id, Transform newParent)
        {
            UIElementMarker found = null;
            foreach (var marker in sceneUIRoot.GetComponentsInChildren<UIElementMarker>(true))
            {
                if (marker.Id == id)
                {
                    found = marker;
                    break;
                }
            }

            if (found == null)
            {
                Debug.LogWarning($"Hub UI에서 '{id}' 요소를 찾을 수 없어 재배치를 건너뛴다. UIElementMarker가 부착되어 있는지 확인하라.");
                return;
            }

            if (found.transform.parent != newParent)
            {
                Undo.SetTransformParent(found.transform, newParent, $"Reparent {id}");
            }
        }
    }
}
