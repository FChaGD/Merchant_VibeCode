using Game.Core;
using Game.Core.DebugTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.Editor.DebugTools
{
    /// <summary>
    /// 상행 준비 화면의 지도 디버그 편집 UI(도시·관문 팔레트, 경로 연결 토글, 일괄 삭제, 지도 저장, 지역 추가/삭제, 확인 패널)를 설치/제거한다
    /// (Docs/Refactor/2026-10-26_씬전용.md H-2, SS-9). 게임 Hub 인스톨러가 만들던 것을 디버그 도구 분리 규칙대로 뺐다 - Build Hub Scene은 이 요소를
    /// 만들지도 지우지도 않는다. 런타임 쪽(TripMapDebugEditor·디버그 뷰)은 에디터 전용이다. Hub.unity를 열고 실행한다(다른 씬이면 경고 후 건너뛴다 -
    /// Install All을 Bootstrap에서 실행하면 이 도구만 빠진다). 걷어낼 때는 Remove를 먼저 실행한 뒤 이 파일과 Core/Debug/Trip을 지우고
    /// DebugToolsBulkInstaller의 두 줄을 지운다.
    /// </summary>
    public static class TripMapEditorInstaller
    {
        private static readonly string[] ObjectNames =
        {
            "DebugCityPalette", "DebugRoadToggleButton", "DebugCityBulkDeleteButton", "DebugRoadBulkDeleteButton", "DebugMapSaveButton",
            "DebugGatePalette", "DebugGateTargetDropdown", "DebugAddRegionButton", "DebugRemoveRegionButton", "DebugConfirmPanel",
        };

        [MenuItem("Tools/Game/Debug/Install/Trip Map Editor")]
        public static void Install()
        {
            if (!TryFindTripPanel(out var panelRoot)) return;
            BuildTripDebugMapControls(panelRoot);
            BuildTripDebugConfirmPanel(panelRoot);
            EditorSceneManager.MarkSceneDirty(panelRoot.gameObject.scene);
            Debug.Log("지도 디버그 편집 UI 설치 완료. Ctrl+S로 씬을 저장하라.");
        }

        [MenuItem("Tools/Game/Debug/Remove/Trip Map Editor")]
        public static void Remove()
        {
            if (!TryFindTripPanel(out var panelRoot)) return;
            foreach (var name in ObjectNames) EditorUIBuilder.DestroyChildIfExists(panelRoot, name);
            EditorSceneManager.MarkSceneDirty(panelRoot.gameObject.scene);
            Debug.Log("지도 디버그 편집 UI 제거 완료. Ctrl+S로 씬을 저장하라.");
        }

        private static bool TryFindTripPanel(out Transform panelRoot)
        {
            panelRoot = null;
            var sceneUIRoot = Object.FindFirstObjectByType<SceneUIRoot>(FindObjectsInactive.Include);
            var marker = sceneUIRoot != null ? EditorUIBuilder.FindMarker(sceneUIRoot, TripUIElementIds.PanelRoot) : null;
            if (marker == null)
            {
                Debug.LogWarning("상행 준비 화면(TripPanel)을 찾을 수 없어 지도 디버그 편집 UI를 건너뛴다 - Hub.unity를 열고 Build Hub Scene 뒤에 실행하라.");
                return false;
            }
            panelRoot = marker.transform;
            return true;
        }

        /// <summary>
        /// 지도 위 디버그 도시 배치/경로 연결(03/04번 기획 문서) 전용 컨트롤. 상단 여백(닫기/배치
        /// 버튼과 같은 줄, 그 왼쪽)에 작게 배치한다 - 원래 지도 아래에 뒀더니 상행 시작 버튼과
        /// 겹쳤다. 정식 콘텐츠가 아니므로 실제 지역 시스템이 생기면 이 메서드와 관련 프리팹 생성
        /// 로직을 통째로 제거한다(DEBUG_FEATURES.md §2 참고).
        /// </summary>
        private static void BuildTripDebugMapControls(Transform parent)
        {
            const float top = TripUILayout.TripTopRowMinY;
            const float bottom = TripUILayout.TripTopRowMaxY;

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

            // 닫기 버튼과 구간을 양분해 왼쪽 절반에 둔다(TripTopShared* 상수 참고). 저장=긍정 동작이라
            // 기존 빨강(삭제)/황토(토글)와 구분되는 녹색 계열(Docs/기획/15번 §3.1, 설계 19번 §8).
            var saveGo = EditorUIBuilder.GetOrCreateUIObject(parent, "DebugMapSaveButton");
            EditorUIBuilder.SetAnchors(saveGo.GetComponent<RectTransform>(), new Vector2(TripUILayout.TripTopSharedMinX, top), new Vector2(TripUILayout.TripTopSharedMinX + TripUILayout.TripTopSharedButtonWidth, bottom));
            EditorUIBuilder.EnsureImage(saveGo, new Color(0.6f, 0.85f, 0.6f, 1f));
            EditorUIBuilder.EnsureButton(saveGo);
            var saveLabel = EditorUIBuilder.EnsureLabel(saveGo.transform, "지도 저장");
            saveLabel.fontSize = 10;
            EditorUIBuilder.EnsureMarker(saveGo, TripUIElementIds.DebugMapSaveButton);

            BuildTripDebugRegionControls(parent);
        }

        // 지역 시스템 디버그 컨트롤(Docs/설계/69번 §6) - 지도 아래 왼쪽(상행 시작 버튼 왼쪽 빈 구간)에 두 줄로 둔다. 윗줄: 관문
        // 팔레트 + 연결할 지역 드롭다운(놓기 전에 고른다, 기획 68번 §3.5), 아랫줄: 지역 추가·삭제.
        private static void BuildTripDebugRegionControls(Transform parent)
        {
            var gatePaletteGo = EditorUIBuilder.GetOrCreateUIObject(parent, "DebugGatePalette");
            EditorUIBuilder.SetAnchors(gatePaletteGo.GetComponent<RectTransform>(), new Vector2(0.06f, 0.085f), new Vector2(0.09f, 0.145f));
            EditorUIBuilder.EnsureMarker(gatePaletteGo, TripUIElementIds.DebugGatePaletteRoot);
            var gateIcon = EditorUIBuilder.EnsureImage(gatePaletteGo, new Color(0.55f, 0.35f, 0.75f, 1f));
            gateIcon.sprite = FormationPlaceholderIcons.GetOrCreateSquare();
            gateIcon.preserveAspect = true;
            var gatePaletteView = EditorUIBuilder.GetOrAddComponent<TripDebugCityPaletteView>(gatePaletteGo);
            var gatePaletteSo = new SerializedObject(gatePaletteView);
            gatePaletteSo.FindProperty("iconImage").objectReferenceValue = gateIcon;
            gatePaletteSo.ApplyModifiedProperties();

            var targetGo = EditorUIBuilder.GetOrCreateUIObject(parent, "DebugGateTargetDropdown");
            EditorUIBuilder.SetAnchors(targetGo.GetComponent<RectTransform>(), new Vector2(0.10f, 0.085f), new Vector2(0.28f, 0.145f));
            EditorUIBuilder.EnsureDropdown(targetGo);
            EditorUIBuilder.EnsureMarker(targetGo, TripUIElementIds.DebugGateTargetDropdown);

            BuildTripDebugButton(parent, "DebugAddRegionButton", "지역 추가", TripUIElementIds.DebugAddRegionButton, new Vector2(0.06f, 0.02f), new Vector2(0.16f, 0.075f), new Color(0.6f, 0.85f, 0.6f, 1f));
            BuildTripDebugButton(parent, "DebugRemoveRegionButton", "지역 삭제", TripUIElementIds.DebugRemoveRegionButton, new Vector2(0.18f, 0.02f), new Vector2(0.28f, 0.075f), new Color(0.9f, 0.6f, 0.6f, 1f));
        }

        private static void BuildTripDebugButton(Transform parent, string name, string text, string markerId, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, name);
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), anchorMin, anchorMax);
            EditorUIBuilder.EnsureImage(go, color);
            EditorUIBuilder.EnsureButton(go);
            EditorUIBuilder.EnsureLabel(go.transform, text, autoSize: true, minFontSize: 8f, maxFontSize: 16f);
            EditorUIBuilder.EnsureMarker(go, markerId);
        }

        // 지역 삭제 확인·알림 패널(기획 68번 §3.6) - 지도 가운데 위에 겹친다. 평소엔 숨김.
        private static void BuildTripDebugConfirmPanel(Transform parent)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(parent, "DebugConfirmPanel");
            EditorUIBuilder.SetAnchors(root.GetComponent<RectTransform>(), new Vector2(0.18f, 0.40f), new Vector2(0.50f, 0.58f));
            EditorUIBuilder.EnsureImage(root, new Color(1f, 1f, 1f, 0.97f));
            EditorUIBuilder.EnsureMarker(root, TripUIElementIds.DebugConfirmPanel);

            var messageGo = EditorUIBuilder.GetOrCreateUIObject(root.transform, "Message");
            EditorUIBuilder.SetAnchors(messageGo.GetComponent<RectTransform>(), new Vector2(0.05f, 0.42f), new Vector2(0.95f, 0.95f));
            var message = EditorUIBuilder.EnsureLabel(messageGo.transform, string.Empty, autoSize: true, minFontSize: 10f, maxFontSize: 20f);
            EditorUIBuilder.EnsureMarker(message.gameObject, TripUIElementIds.DebugConfirmMessage);

            BuildTripDebugButton(root.transform, "OkButton", "확인", TripUIElementIds.DebugConfirmOkButton, new Vector2(0.08f, 0.08f), new Vector2(0.46f, 0.36f), new Color(0.6f, 0.85f, 0.6f, 1f));
            BuildTripDebugButton(root.transform, "CancelButton", "취소", TripUIElementIds.DebugConfirmCancelButton, new Vector2(0.54f, 0.08f), new Vector2(0.92f, 0.36f), new Color(0.85f, 0.85f, 0.85f, 1f));

            // 지도 위 확인 패널은 다른 화면 요소 위, 상행 시작 사유 툴팁·출발 확인 대화상자 아래에 둔다(게임 조립과 분리 전 순서 유지).
            var tooltip = parent.Find("StartBlockTooltip");
            if (tooltip != null)
            {
                // 패널이 이미 툴팁보다 앞에 있으면 목표 인덱스에 자기 자리가 포함돼 있어 하나 줄인다(SetSiblingIndex 오프바이원).
                var target = tooltip.GetSiblingIndex();
                if (root.transform.GetSiblingIndex() < target) target--;
                root.transform.SetSiblingIndex(target);
            }
            else root.transform.SetAsLastSibling();
            root.SetActive(false);
        }
    }
}
