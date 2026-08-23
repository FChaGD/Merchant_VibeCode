using Game.Core;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.Editor
{
    /// <summary>
    /// Hub 씬에 배치(Formation) UI 하이어라키와 필요한 플레이스홀더 프리팹을 코드로 생성/동기화한다.
    /// 씬 YAML 수작업 편집 대신 이 도구로 재현 가능하게 만든다 - ManagerHierarchyInstaller와 동일한 방식.
    /// 각 영역의 위치/크기는 참고 목업("배치 ui.png")의 영역 비율을 그대로 따른다:
    /// 팔레트(좌상단, 넓게) / 적용·닫기 버튼(우상단) / 그리드(좌하단, 넓게) / 정보패널(우하단, 그리드와 동일 높이).
    /// 색상·스프라이트는 자리표시자이며, 실제 비주얼은 에디터에서 자유롭게 교체하면 된다.
    /// 씬 조립에 필요한 저수준 헬퍼(오브젝트 생성, 앵커 설정 등)는 EditorUIBuilder를 공유해서 쓴다 -
    /// 다른 UI 인스톨러(TripUIInstaller 등)와 서로의 구현 세부사항이 아니라 이 공용 도구에 의존한다.
    /// </summary>
    public static class FormationUIInstaller
    {
        private const string PrefabFolder = "Assets/Prefabs/UI/Formation";
        private const string SlotPrefabPath = PrefabFolder + "/FormationSlot.prefab";
        private const string IconPrefabPath = PrefabFolder + "/FormationUnitIcon.prefab";

        [MenuItem("Tools/Game/Build Formation UI")]
        public static void BuildFormationUI()
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
            var slotPrefab = GetOrCreateSlotPrefab();
            var iconPrefab = GetOrCreateIconPrefab();

            var panelRoot = EditorUIBuilder.GetOrCreateUIObject(sceneUIRoot.transform, "FormationPanel");
            EditorUIBuilder.SetStretch(panelRoot.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureMarker(panelRoot, FormationUIElementIds.PanelRoot);

            BuildPalette(panelRoot.transform, iconPrefab);
            BuildTopRightButtons(panelRoot.transform);
            BuildGrid(panelRoot.transform, slotPrefab, iconPrefab);
            BuildInfoPanel(panelRoot.transform);
            BuildDebugPanel(panelRoot.transform);

            panelRoot.SetActive(false);

            EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log("Formation UI 하이어라키 생성/동기화 완료. 씬을 저장(Ctrl+S)해야 변경사항이 파일에 반영된다. "
                + $"FormationPanel.dragGhostPrefab에는 '{IconPrefabPath}'를 수동으로 연결하라(FormationPanel은 Bootstrap 씬에 있어 이 도구가 직접 연결할 수 없다).");
        }

        private static void BuildPalette(Transform parent, FormationUnitIconView iconPrefab)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(parent, "Palette");
            EditorUIBuilder.SetAnchors(root.GetComponent<RectTransform>(), new Vector2(0.08f, 0.75f), new Vector2(0.62f, 0.85f));
            EditorUIBuilder.EnsureImage(root, new Color(1f, 0.85f, 0.85f, 1f));
            EditorUIBuilder.EnsureMarker(root, FormationUIElementIds.PaletteRoot);

            var (_, content) = BuildHorizontalScrollArea(root.transform);

            var paletteView = EditorUIBuilder.GetOrAddComponent<FormationPaletteView>(root);
            var so = new SerializedObject(paletteView);
            so.FindProperty("iconContent").objectReferenceValue = content;
            so.FindProperty("iconPrefab").objectReferenceValue = iconPrefab;
            so.ApplyModifiedProperties();
        }

        private static void BuildTopRightButtons(Transform parent)
        {
            // 이전 버전("저장" 표기)에 남아있을 수 있는 오브젝트는 제거하고 "적용"으로 새로 만든다.
            EditorUIBuilder.DestroyChildIfExists(parent, "SaveButton");

            var applyGo = EditorUIBuilder.GetOrCreateUIObject(parent, "ApplyButton");
            EditorUIBuilder.SetAnchors(applyGo.GetComponent<RectTransform>(), new Vector2(0.64f, 0.75f), new Vector2(0.76f, 0.85f));
            EditorUIBuilder.EnsureImage(applyGo, new Color(0.75f, 0.87f, 1f, 1f));
            EditorUIBuilder.EnsureButton(applyGo);
            EditorUIBuilder.EnsureLabel(applyGo.transform, "적용");
            EditorUIBuilder.EnsureMarker(applyGo, FormationUIElementIds.ApplyButton);

            var closeGo = EditorUIBuilder.GetOrCreateUIObject(parent, "CloseButton");
            EditorUIBuilder.SetAnchors(closeGo.GetComponent<RectTransform>(), new Vector2(0.78f, 0.75f), new Vector2(0.86f, 0.85f));
            EditorUIBuilder.EnsureImage(closeGo, new Color(0.85f, 0.85f, 0.85f, 1f));
            EditorUIBuilder.EnsureButton(closeGo);
            EditorUIBuilder.EnsureLabel(closeGo.transform, "닫기");
            EditorUIBuilder.EnsureMarker(closeGo, FormationUIElementIds.CloseButton);
        }

        private static void BuildGrid(Transform parent, FormationSlotView slotPrefab, FormationUnitIconView occupantIconPrefab)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(parent, "Grid");
            EditorUIBuilder.SetAnchors(root.GetComponent<RectTransform>(), new Vector2(0.08f, 0.30f), new Vector2(0.64f, 0.74f));
            EditorUIBuilder.EnsureImage(root, new Color(0.82f, 0.95f, 0.85f, 1f));
            EditorUIBuilder.EnsureMarker(root, FormationUIElementIds.GridRoot);

            var (_, content, layoutGroup) = BuildGridScrollArea(root.transform, new Vector2(120f, 120f), 8);

            // 이전 버전(좌우 버튼 스크롤) 설치분에 남아있을 수 있는 버튼은 더 이상 쓰지 않으므로 제거한다
            // - 드래그만으로 가로/세로 이동한다.
            EditorUIBuilder.DestroyChildIfExists(root.transform, "ScrollLeftButton");
            EditorUIBuilder.DestroyChildIfExists(root.transform, "ScrollRightButton");

            var gridView = EditorUIBuilder.GetOrAddComponent<FormationGridView>(root);
            var so = new SerializedObject(gridView);
            so.FindProperty("slotContent").objectReferenceValue = content;
            so.FindProperty("slotLayoutGroup").objectReferenceValue = layoutGroup;
            so.FindProperty("slotPrefab").objectReferenceValue = slotPrefab;
            so.FindProperty("occupantIconPrefab").objectReferenceValue = occupantIconPrefab;
            so.ApplyModifiedProperties();
        }

        private static void BuildInfoPanel(Transform parent)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(parent, "InfoPanel");
            EditorUIBuilder.SetAnchors(root.GetComponent<RectTransform>(), new Vector2(0.66f, 0.30f), new Vector2(0.86f, 0.74f));
            EditorUIBuilder.EnsureImage(root, new Color(1f, 0.9f, 0.78f, 1f));
            EditorUIBuilder.EnsureMarker(root, FormationUIElementIds.InfoPanelRoot);

            var iconGo = EditorUIBuilder.GetOrCreateUIObject(root.transform, "Icon");
            EditorUIBuilder.SetAnchors(iconGo.GetComponent<RectTransform>(), new Vector2(0.25f, 0.55f), new Vector2(0.75f, 0.92f));
            var iconImage = EditorUIBuilder.EnsureImage(iconGo, Color.white);
            iconImage.preserveAspect = true;

            var nameLabel = EditorUIBuilder.EnsureLabel(root.transform, string.Empty);
            EditorUIBuilder.SetAnchors(nameLabel.rectTransform, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.45f));

            var infoView = EditorUIBuilder.GetOrAddComponent<FormationInfoPanelView>(root);
            var so = new SerializedObject(infoView);
            so.FindProperty("iconImage").objectReferenceValue = iconImage;
            so.FindProperty("nameText").objectReferenceValue = nameLabel;
            so.ApplyModifiedProperties();
        }

        /// <summary>
        /// Play 모드에서 그리드 슬롯 개수/크기를 즉시 조절할 수 있는 온스크린 디버그 패널.
        /// 상단 여백(팔레트/버튼 행 위쪽, 목업에는 없는 영역)에 배치한다.
        /// </summary>
        private static void BuildDebugPanel(Transform parent)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(parent, "DebugPanel");
            EditorUIBuilder.SetAnchors(root.GetComponent<RectTransform>(), new Vector2(0.08f, 0.87f), new Vector2(0.86f, 0.98f));
            EditorUIBuilder.EnsureImage(root, new Color(0f, 0f, 0f, 0.15f));
            EditorUIBuilder.EnsureMarker(root, FormationUIElementIds.DebugPanelRoot);

            BuildDebugLabel(root.transform, "ColumnsLabel", "X", new Vector2(0.00f, 0f), new Vector2(0.05f, 1f));
            var columnsInput = CreateInputField(root.transform, "ColumnsInput", new Vector2(0.05f, 0.1f), new Vector2(0.16f, 0.9f), TMP_InputField.ContentType.IntegerNumber);

            BuildDebugLabel(root.transform, "RowsLabel", "Y", new Vector2(0.19f, 0f), new Vector2(0.24f, 1f));
            var rowsInput = CreateInputField(root.transform, "RowsInput", new Vector2(0.24f, 0.1f), new Vector2(0.35f, 0.9f), TMP_InputField.ContentType.IntegerNumber);

            BuildDebugLabel(root.transform, "WidthLabel", "W", new Vector2(0.38f, 0f), new Vector2(0.43f, 1f));
            var widthInput = CreateInputField(root.transform, "WidthInput", new Vector2(0.43f, 0.1f), new Vector2(0.55f, 0.9f), TMP_InputField.ContentType.DecimalNumber);

            BuildDebugLabel(root.transform, "HeightLabel", "H", new Vector2(0.58f, 0f), new Vector2(0.63f, 1f));
            var heightInput = CreateInputField(root.transform, "HeightInput", new Vector2(0.63f, 0.1f), new Vector2(0.75f, 0.9f), TMP_InputField.ContentType.DecimalNumber);

            var applyGo = EditorUIBuilder.GetOrCreateUIObject(root.transform, "ApplyButton");
            EditorUIBuilder.SetAnchors(applyGo.GetComponent<RectTransform>(), new Vector2(0.78f, 0.1f), new Vector2(0.98f, 0.9f));
            EditorUIBuilder.EnsureImage(applyGo, new Color(0.7f, 1f, 0.7f, 1f));
            EditorUIBuilder.EnsureButton(applyGo);
            var applyLabel = EditorUIBuilder.EnsureLabel(applyGo.transform, "적용");
            applyLabel.fontSize = 18;

            var debugView = EditorUIBuilder.GetOrAddComponent<FormationGridDebugView>(root);
            var so = new SerializedObject(debugView);
            so.FindProperty("columnsInput").objectReferenceValue = columnsInput;
            so.FindProperty("rowsInput").objectReferenceValue = rowsInput;
            so.FindProperty("slotWidthInput").objectReferenceValue = widthInput;
            so.FindProperty("slotHeightInput").objectReferenceValue = heightInput;
            so.FindProperty("applyButton").objectReferenceValue = applyGo.GetComponent<Button>();
            so.ApplyModifiedProperties();
        }

        private static void BuildDebugLabel(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, name);
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), anchorMin, anchorMax);
            var label = EditorUIBuilder.GetOrAddComponent<TextMeshProUGUI>(go);
            label.text = text;
            label.alignment = TextAlignmentOptions.MidlineRight;
            label.fontSize = 18;
            label.color = Color.black;
            label.raycastTarget = false;
        }

        private static TMP_InputField CreateInputField(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, TMP_InputField.ContentType contentType)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, name);
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), anchorMin, anchorMax);
            EditorUIBuilder.EnsureImage(go, new Color(1f, 1f, 1f, 0.95f));

            var textAreaGo = EditorUIBuilder.GetOrCreateUIObject(go.transform, "TextArea");
            var textAreaRect = textAreaGo.GetComponent<RectTransform>();
            EditorUIBuilder.SetStretch(textAreaRect);
            textAreaRect.offsetMin = new Vector2(6, 2);
            textAreaRect.offsetMax = new Vector2(-6, -2);
            EditorUIBuilder.GetOrAddComponent<RectMask2D>(textAreaGo);

            var textGo = EditorUIBuilder.GetOrCreateUIObject(textAreaRect, "Text");
            EditorUIBuilder.SetStretch(textGo.GetComponent<RectTransform>());
            var textComponent = EditorUIBuilder.GetOrAddComponent<TextMeshProUGUI>(textGo);
            textComponent.fontSize = 18;
            textComponent.color = Color.black;
            textComponent.alignment = TextAlignmentOptions.MidlineLeft;
            textComponent.raycastTarget = false;

            var inputField = EditorUIBuilder.GetOrAddComponent<TMP_InputField>(go);
            inputField.textViewport = textAreaRect;
            inputField.textComponent = textComponent;
            inputField.contentType = contentType;

            return inputField;
        }

        /// <summary>
        /// 그리드용 Viewport/Content 구조를 만든다. GridLayoutGroup으로 정사각형 타일을 X열 x Y행으로
        /// 배치하고, root에 가로/세로 모두 가능한 ScrollRect를 붙여 연결한다.
        /// </summary>
        private static (RectTransform viewport, RectTransform content, GridLayoutGroup layoutGroup) BuildGridScrollArea(Transform root, Vector2 cellSize, int columns)
        {
            var (viewportRect, contentGo) = EditorUIBuilder.CreateViewportAndContent(root);
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(0f, 1f);
            contentRect.pivot = new Vector2(0f, 1f);
            contentRect.anchoredPosition = Vector2.zero;

            // 이전 버전(가로 1줄 스크롤) 설치분에 남아있을 수 있는 HorizontalLayoutGroup은
            // GridLayoutGroup과 같은 오브젝트에 공존할 수 없으므로 제거하고 새로 구성한다.
            var staleHorizontalLayout = contentGo.GetComponent<HorizontalLayoutGroup>();
            if (staleHorizontalLayout != null)
            {
                Undo.DestroyObjectImmediate(staleHorizontalLayout);
            }

            var layoutGroup = EditorUIBuilder.GetOrAddComponent<GridLayoutGroup>(contentGo);
            layoutGroup.cellSize = cellSize;
            layoutGroup.spacing = Vector2.zero;
            layoutGroup.padding = new RectOffset(8, 8, 8, 8);
            layoutGroup.startCorner = GridLayoutGroup.Corner.UpperLeft;
            layoutGroup.startAxis = GridLayoutGroup.Axis.Horizontal;
            layoutGroup.childAlignment = TextAnchor.UpperLeft;
            layoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layoutGroup.constraintCount = Mathf.Max(1, columns);

            var fitter = EditorUIBuilder.GetOrAddComponent<ContentSizeFitter>(contentGo);
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            EditorUIBuilder.ConfigureScrollRect(root.gameObject, viewportRect, contentRect, horizontal: true, vertical: true);

            return (viewportRect, contentRect, layoutGroup);
        }

        /// <summary>
        /// 가로 스크롤용 Viewport/Content 구조를 만들고, root에 ScrollRect를 붙여 연결한다. 팔레트가 사용한다.
        /// </summary>
        private static (RectTransform viewport, RectTransform content) BuildHorizontalScrollArea(Transform root)
        {
            var (viewportRect, contentGo) = EditorUIBuilder.CreateViewportAndContent(root);
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(0f, 1f);
            contentRect.pivot = new Vector2(0f, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;

            var layoutGroup = EditorUIBuilder.GetOrAddComponent<HorizontalLayoutGroup>(contentGo);
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = true;
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = false;
            layoutGroup.spacing = 8;
            layoutGroup.padding = new RectOffset(8, 8, 8, 8);
            layoutGroup.childAlignment = TextAnchor.MiddleLeft;

            var fitter = EditorUIBuilder.GetOrAddComponent<ContentSizeFitter>(contentGo);
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            EditorUIBuilder.ConfigureScrollRect(root.gameObject, viewportRect, contentRect, horizontal: true, vertical: false);

            return (viewportRect, contentRect);
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
                AssetDatabase.CreateFolder("Assets/Prefabs/UI", "Formation");
            }
        }

        private static FormationUnitIconView GetOrCreateIconPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(IconPrefabPath);
            if (existing != null)
            {
                return existing.GetComponent<FormationUnitIconView>();
            }

            var go = new GameObject("FormationUnitIcon", typeof(RectTransform));
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(96, 96);

            var image = go.AddComponent<Image>();
            image.color = Color.white;

            var iconView = go.AddComponent<FormationUnitIconView>();
            var so = new SerializedObject(iconView);
            so.FindProperty("iconImage").objectReferenceValue = image;
            so.ApplyModifiedProperties();

            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(go, IconPrefabPath);
            Object.DestroyImmediate(go);

            return savedPrefab.GetComponent<FormationUnitIconView>();
        }

        private static FormationSlotView GetOrCreateSlotPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPrefabPath);
            if (existing != null)
            {
                return existing.GetComponent<FormationSlotView>();
            }

            var go = new GameObject("FormationSlot", typeof(RectTransform));
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(120, 120);

            var bgImage = go.AddComponent<Image>();
            bgImage.color = new Color(1f, 1f, 1f, 0.35f);

            var containerGo = new GameObject("IconContainer", typeof(RectTransform));
            containerGo.transform.SetParent(go.transform, false);
            EditorUIBuilder.SetStretch(containerGo.GetComponent<RectTransform>());

            var slotView = go.AddComponent<FormationSlotView>();
            var so = new SerializedObject(slotView);
            so.FindProperty("iconContainer").objectReferenceValue = containerGo.GetComponent<RectTransform>();
            so.ApplyModifiedProperties();

            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(go, SlotPrefabPath);
            Object.DestroyImmediate(go);

            return savedPrefab.GetComponent<FormationSlotView>();
        }
    }
}
