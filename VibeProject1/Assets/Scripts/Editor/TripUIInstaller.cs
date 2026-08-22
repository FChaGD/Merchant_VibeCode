using Game.Core;
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

            var panelRoot = EditorUIBuilder.GetOrCreateUIObject(sceneUIRoot.transform, "TripPanel");
            EditorUIBuilder.SetStretch(panelRoot.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureMarker(panelRoot, TripUIElementIds.PanelRoot);

            BuildTopButtons(panelRoot.transform);
            BuildMap(panelRoot.transform);
            BuildLocationInfo(panelRoot.transform, "OriginInfo", TripUIElementIds.OriginInfoRoot, new Vector2(0.64f, 0.64f), new Vector2(0.94f, 0.88f));
            BuildLocationInfo(panelRoot.transform, "DestinationInfo", TripUIElementIds.DestinationInfoRoot, new Vector2(0.64f, 0.40f), new Vector2(0.94f, 0.63f));
            BuildSummary(panelRoot.transform);
            BuildStartButton(panelRoot.transform);

            panelRoot.SetActive(false);

            EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log("Trip UI 하이어라키 생성/동기화 완료. 씬을 저장(Ctrl+S)해야 변경사항이 파일에 반영된다.");
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

            var originPin = BuildPin(contentRect, "OriginPin", new Vector2(-400f, -300f), new Color(0.8f, 0.35f, 0.1f, 1f));
            var destinationPin = BuildPin(contentRect, "DestinationPin", new Vector2(400f, 300f), new Color(0.2f, 0.3f, 0.6f, 1f));

            EditorUIBuilder.ConfigureScrollRect(root, viewport, contentRect, horizontal: true, vertical: true);

            var mapView = EditorUIBuilder.GetOrAddComponent<TripMapView>(root);
            var so = new SerializedObject(mapView);
            so.FindProperty("originPinButton").objectReferenceValue = originPin;
            so.FindProperty("destinationPinButton").objectReferenceValue = destinationPin;
            so.ApplyModifiedProperties();
        }

        private static Button BuildPin(Transform parent, string name, Vector2 anchoredPosition, Color color)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, name);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(64f, 64f);
            rect.anchoredPosition = anchoredPosition;
            EditorUIBuilder.EnsureImage(go, color);
            return EditorUIBuilder.EnsureButton(go);
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
    }
}
