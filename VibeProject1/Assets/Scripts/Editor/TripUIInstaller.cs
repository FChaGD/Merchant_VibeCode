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
    /// Hub 씬에 상행 준비 UI 하이어라키를 코드로 생성/동기화한다. 씬 YAML 수작업 편집 대신 이 도구로
    /// 재현 가능하게 만든다 - FormationUIInstaller와 동일한 방식. 각 영역의 위치/크기는 기획 검토용
    /// 와이어프레임의 배치를 참고한 자리표시자 비율이며, 실제 비주얼은 에디터에서 자유롭게 교체하면 된다.
    /// </summary>
    public static class TripUIInstaller
    {
        private const float MapContentSize = 2400f;
        private const string PrefabFolder = "Assets/Prefabs/UI/Trip";
        private const string CityMarkerPrefabPath = PrefabFolder + "/TripDebugCityMarker.prefab";
        private const string RoadLinePrefabPath = PrefabFolder + "/TripDebugRoadLine.prefab";

        [MenuItem("Tools/Game/Build Trip UI")]
        public static void BuildTripUI()
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

            EnsurePrefabFolder();
            GetOrCreateCityMarkerPrefab(); // TripPanel.debugCityMarkerPrefab에 수동 연결 대상(에셋만 미리 생성)
            GetOrCreateRoadLinePrefab();   // TripPanel.debugRoadLinePrefab에 수동 연결 대상(에셋만 미리 생성)

            var panelRoot = EditorUIBuilder.GetOrCreateUIObject(sceneUIRoot.transform, "TripPanel");
            EditorUIBuilder.SetStretch(panelRoot.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureMarker(panelRoot, TripUIElementIds.PanelRoot);

            BuildTopButtons(panelRoot.transform);
            BuildMap(panelRoot.transform);
            BuildLocationInfo(panelRoot.transform, "OriginInfo", TripUIElementIds.OriginInfoRoot, new Vector2(0.64f, 0.64f), new Vector2(0.94f, 0.88f));
            BuildLocationInfo(panelRoot.transform, "DestinationInfo", TripUIElementIds.DestinationInfoRoot, new Vector2(0.64f, 0.40f), new Vector2(0.94f, 0.63f));
            BuildSummary(panelRoot.transform);
            BuildStartButton(panelRoot.transform);
            BuildDebugMapControls(panelRoot.transform);

            panelRoot.SetActive(false);

            EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log("Trip UI 하이어라키 생성/동기화 완료. 씬을 저장(Ctrl+S)해야 변경사항이 파일에 반영된다. "
                + $"TripPanel.debugCityMarkerPrefab에는 '{CityMarkerPrefabPath}', debugRoadLinePrefab에는 '{RoadLinePrefabPath}'를 "
                + "수동으로 연결하라(TripPanel은 Bootstrap 씬에 있어 이 도구가 직접 연결할 수 없다).");
        }

        private static void BuildTopButtons(Transform parent)
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

        private static void BuildMap(Transform parent)
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
            root.GetComponent<ScrollRect>().scrollSensitivity = 0f;

            EditorUIBuilder.GetOrAddComponent<TripMapView>(root);
        }

        private static void BuildLocationInfo(Transform parent, string objectName, string markerId, Vector2 anchorMin, Vector2 anchorMax)
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

        private static void BuildSummary(Transform parent)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(parent, "Summary");
            EditorUIBuilder.SetAnchors(root.GetComponent<RectTransform>(), new Vector2(0.64f, 0.16f), new Vector2(0.94f, 0.39f));
            EditorUIBuilder.EnsureImage(root, new Color(0.9f, 0.9f, 0.96f, 1f));
            EditorUIBuilder.EnsureMarker(root, TripUIElementIds.SummaryRoot);

            var durationLabel = BuildSummaryRow(root.transform, "DurationDistanceLabel", 0);
            var dangerLabel = BuildSummaryRow(root.transform, "DangerLabel", 1);
            var formationLabel = BuildSummaryRow(root.transform, "FormationSummaryLabel", 2);
            var rewardLabel = BuildSummaryRow(root.transform, "RewardLabel", 3);

            var summaryView = EditorUIBuilder.GetOrAddComponent<TripSummaryView>(root);
            var so = new SerializedObject(summaryView);
            so.FindProperty("durationDistanceText").objectReferenceValue = durationLabel;
            so.FindProperty("dangerText").objectReferenceValue = dangerLabel;
            so.FindProperty("formationSummaryText").objectReferenceValue = formationLabel;
            so.FindProperty("rewardText").objectReferenceValue = rewardLabel;
            so.ApplyModifiedProperties();
        }

        private static TextMeshProUGUI BuildSummaryRow(Transform parent, string name, int rowIndex)
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

        private static void BuildStartButton(Transform parent)
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
        /// 로직을 통째로 제거한다.
        /// </summary>
        private static void BuildDebugMapControls(Transform parent)
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

        private static void EnsurePrefabFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
            }
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
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
    }
}
