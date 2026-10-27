using Game.Core;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.Editor
{
    /// <summary>
    /// 상행 준비 화면 조립(Docs/Refactor/2026-10-26_씬전용.md H-3, SS-10) - Hub 인스톨러 안에 있던 약 330줄을 뺐다. 지도 마커·선 프리팹도 여기서 보장한다
    /// (TripPanel 연결은 Build Bootstrap Scene, 설계 69번 §7). 지도 디버그 편집 UI는 게임 화면이 아니라 전용 디버그 인스톨러 몫이다.
    /// </summary>
    internal static class TripUIBuilder
    {
        // ==================== 상행 준비(Trip) UI ====================
        public static void Build(Transform modalPopups)
        {
            // 지도 마커·선 프리팹(TripPanel 연결은 Build Bootstrap Scene이 한다, 설계 69번 §7).
            TripMapPrefabs.GetOrCreateCityMarker();
            TripMapPrefabs.GetOrCreateGateMarker();
            TripMapPrefabs.GetOrCreateRoadLine();

            var panelRoot = EditorUIBuilder.GetOrCreateUIObject(modalPopups, "TripPanel");
            EditorUIBuilder.SetStretch(panelRoot.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureMarker(panelRoot, TripUIElementIds.PanelRoot);

            BuildTripTopButtons(panelRoot.transform);
            BuildTripMap(panelRoot.transform);
            BuildTripLocationInfo(panelRoot.transform, "OriginInfo", TripUIElementIds.OriginInfoRoot, new Vector2(0.64f, 0.58f), new Vector2(0.94f, TripUILayout.TripContentTopY));
            BuildTripLocationInfo(panelRoot.transform, "DestinationInfo", TripUIElementIds.DestinationInfoRoot, new Vector2(0.64f, 0.37f), new Vector2(0.94f, 0.57f));
            BuildTripSummary(panelRoot.transform);
            BuildTripStartButton(panelRoot.transform);
            // 지도 디버그 편집 UI는 전용 디버그 인스톨러가 만든다(Tools/Game/Debug/Install/Trip Map Editor, Docs/Refactor/2026-10-26_씬전용.md H-2).
            // 끊어진 대열 안내 레이블은 비활성 사유 툴팁으로 대체됐다(Docs/설계/100번 §5.2) - 옛 오브젝트 정리.
            EditorUIBuilder.DestroyChildIfExists(panelRoot.transform, "DisconnectedNotice");
            BuildTripStartBlockTooltip(panelRoot.transform);
            // 배치 유닛 0 출발 경고(Docs/설계/79번 §9.2) - 상행 준비 화면 전체를 덮는 공용 확인 대화상자, 마지막 형제로 최상단.
            EditorUIBuilder.BuildConfirmDialog(panelRoot.transform, "DepartureConfirmDialog", TripUIElementIds.DepartureConfirmDialog);

            panelRoot.SetActive(false);
        }

        private static void BuildTripTopButtons(Transform parent)
        {
            var closeGo = EditorUIBuilder.GetOrCreateUIObject(parent, "CloseButton");
            EditorUIBuilder.SetAnchors(closeGo.GetComponent<RectTransform>(), new Vector2(TripUILayout.TripTopSharedMaxX - TripUILayout.TripTopSharedButtonWidth, TripUILayout.TripTopRowMinY), new Vector2(TripUILayout.TripTopSharedMaxX, TripUILayout.TripTopRowMaxY));
            EditorUIBuilder.EnsureImage(closeGo, new Color(0.85f, 0.85f, 0.85f, 1f));
            EditorUIBuilder.EnsureButton(closeGo);
            EditorUIBuilder.EnsureLabel(closeGo.transform, "닫기");
            EditorUIBuilder.EnsureMarker(closeGo, TripUIElementIds.CloseButton);

            var formationGo = EditorUIBuilder.GetOrCreateUIObject(parent, "OpenFormationButton");
            EditorUIBuilder.SetAnchors(formationGo.GetComponent<RectTransform>(), new Vector2(0.80f, TripUILayout.TripTopRowMinY), new Vector2(0.89f, TripUILayout.TripTopRowMaxY));
            EditorUIBuilder.EnsureImage(formationGo, new Color(0.75f, 0.87f, 1f, 1f));
            EditorUIBuilder.EnsureButton(formationGo);
            EditorUIBuilder.EnsureLabel(formationGo.transform, "배치");
            EditorUIBuilder.EnsureMarker(formationGo, TripUIElementIds.OpenFormationButton);
        }

        private static void BuildTripMap(Transform parent)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(parent, "Map");
            EditorUIBuilder.SetAnchors(root.GetComponent<RectTransform>(), new Vector2(0.06f, 0.16f), new Vector2(0.62f, TripUILayout.TripContentTopY));
            EditorUIBuilder.EnsureImage(root, new Color(0.85f, 0.9f, 0.85f, 1f));
            EditorUIBuilder.EnsureMarker(root, TripUIElementIds.MapRoot);

            var (viewport, contentGo) = EditorUIBuilder.CreateViewportAndContent(root.transform);
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta = new Vector2(TripMapView.ContentSize, TripMapView.ContentSize);
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

            // 지역 선택 드롭다운(기획 68번 §3.2) - 지도 왼쪽 위에 겹쳐 둔다. 뷰포트 뒤에 만들어져야 위에 그려진다.
            var dropdownGo = EditorUIBuilder.GetOrCreateUIObject(root.transform, "RegionDropdown");
            EditorUIBuilder.SetAnchors(dropdownGo.GetComponent<RectTransform>(), new Vector2(0.02f, 0.92f), new Vector2(0.34f, 0.99f));
            EditorUIBuilder.EnsureDropdown(dropdownGo);
            EditorUIBuilder.EnsureMarker(dropdownGo, TripUIElementIds.RegionDropdown);
            dropdownGo.transform.SetAsLastSibling();
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
            var nameLabel = EditorUIBuilder.EnsureText(nameGo);
            nameLabel.fontSize = 20;
            nameLabel.color = Color.black;
            nameLabel.alignment = TextAlignmentOptions.MidlineLeft;
            nameLabel.raycastTarget = false;

            var descriptionGo = EditorUIBuilder.GetOrCreateUIObject(root.transform, "DescriptionLabel");
            EditorUIBuilder.SetAnchors(descriptionGo.GetComponent<RectTransform>(), new Vector2(0.06f, 0.06f), new Vector2(0.96f, 0.48f));
            var descriptionLabel = EditorUIBuilder.EnsureText(descriptionGo);
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
            EditorUIBuilder.SetAnchors(root.GetComponent<RectTransform>(), new Vector2(0.64f, 0.16f), new Vector2(0.94f, 0.36f));
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
            var label = EditorUIBuilder.EnsureText(go);
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
            // 비활성 사유 툴팁 입력(설계 100번 §5.2) - 비활성 버튼도 진입·이탈·누름 이벤트를 받는다.
            EditorUIBuilder.GetOrAddComponent<PointerHoverRelay>(go);
            EditorUIBuilder.GetOrAddComponent<PointerPressRelay>(go);
        }

        // "상행 시작" 비활성 사유 툴팁(Docs/설계/100번 §5.2) - 버튼 폭 그대로, 아래 변을 버튼 윗변 바로 위에 고정하고 높이는 문구 줄 수에 맞춘다.
        // 출발 확인 대화상자(마지막 형제로 다시 올라감)보다 먼저 만들어 대화상자가 툴팁 위에 그려지게 한다.
        private static void BuildTripStartBlockTooltip(Transform panelRoot)
        {
            var (tooltipRoot, _) = EditorUIBuilder.EnsureTooltip(panelRoot, "StartBlockTooltip", TripUIElementIds.StartBlockTooltip, TripUIElementIds.StartBlockTooltipText);
            var rect = tooltipRoot.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.30f, 0.15f);
            rect.anchorMax = new Vector2(0.70f, 0.15f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, rect.sizeDelta.y);
            tooltipRoot.transform.SetAsLastSibling();
        }
    }
}
