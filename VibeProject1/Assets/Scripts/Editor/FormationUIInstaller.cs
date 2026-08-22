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

            var panelRoot = GetOrCreateUIObject(sceneUIRoot.transform, "FormationPanel");
            SetStretch(panelRoot.GetComponent<RectTransform>());
            EnsureMarker(panelRoot, FormationUIElementIds.PanelRoot);

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
            var root = GetOrCreateUIObject(parent, "Palette");
            SetAnchors(root.GetComponent<RectTransform>(), new Vector2(0.08f, 0.75f), new Vector2(0.62f, 0.85f));
            EnsureImage(root, new Color(1f, 0.85f, 0.85f, 1f));
            EnsureMarker(root, FormationUIElementIds.PaletteRoot);

            var (_, content) = BuildHorizontalScrollArea(root.transform);

            var paletteView = GetOrAddComponent<FormationPaletteView>(root);
            var so = new SerializedObject(paletteView);
            so.FindProperty("iconContent").objectReferenceValue = content;
            so.FindProperty("iconPrefab").objectReferenceValue = iconPrefab;
            so.ApplyModifiedProperties();
        }

        private static void BuildTopRightButtons(Transform parent)
        {
            // 이전 버전("저장" 표기)에 남아있을 수 있는 오브젝트는 제거하고 "적용"으로 새로 만든다.
            DestroyChildIfExists(parent, "SaveButton");

            var applyGo = GetOrCreateUIObject(parent, "ApplyButton");
            SetAnchors(applyGo.GetComponent<RectTransform>(), new Vector2(0.64f, 0.75f), new Vector2(0.76f, 0.85f));
            EnsureImage(applyGo, new Color(0.75f, 0.87f, 1f, 1f));
            EnsureButton(applyGo);
            EnsureLabel(applyGo.transform, "적용");
            EnsureMarker(applyGo, FormationUIElementIds.ApplyButton);

            var closeGo = GetOrCreateUIObject(parent, "CloseButton");
            SetAnchors(closeGo.GetComponent<RectTransform>(), new Vector2(0.78f, 0.75f), new Vector2(0.86f, 0.85f));
            EnsureImage(closeGo, new Color(0.85f, 0.85f, 0.85f, 1f));
            EnsureButton(closeGo);
            EnsureLabel(closeGo.transform, "닫기");
            EnsureMarker(closeGo, FormationUIElementIds.CloseButton);
        }

        private static void BuildGrid(Transform parent, FormationSlotView slotPrefab, FormationUnitIconView occupantIconPrefab)
        {
            var root = GetOrCreateUIObject(parent, "Grid");
            SetAnchors(root.GetComponent<RectTransform>(), new Vector2(0.08f, 0.30f), new Vector2(0.64f, 0.74f));
            EnsureImage(root, new Color(0.82f, 0.95f, 0.85f, 1f));
            EnsureMarker(root, FormationUIElementIds.GridRoot);

            var (_, content, layoutGroup) = BuildGridScrollArea(root.transform, new Vector2(120f, 120f), 8);

            // 이전 버전(좌우 버튼 스크롤) 설치분에 남아있을 수 있는 버튼은 더 이상 쓰지 않으므로 제거한다
            // - 드래그만으로 가로/세로 이동한다.
            DestroyChildIfExists(root.transform, "ScrollLeftButton");
            DestroyChildIfExists(root.transform, "ScrollRightButton");

            var gridView = GetOrAddComponent<FormationGridView>(root);
            var so = new SerializedObject(gridView);
            so.FindProperty("slotContent").objectReferenceValue = content;
            so.FindProperty("slotLayoutGroup").objectReferenceValue = layoutGroup;
            so.FindProperty("slotPrefab").objectReferenceValue = slotPrefab;
            so.FindProperty("occupantIconPrefab").objectReferenceValue = occupantIconPrefab;
            so.ApplyModifiedProperties();
        }

        private static void BuildInfoPanel(Transform parent)
        {
            var root = GetOrCreateUIObject(parent, "InfoPanel");
            SetAnchors(root.GetComponent<RectTransform>(), new Vector2(0.66f, 0.30f), new Vector2(0.86f, 0.74f));
            EnsureImage(root, new Color(1f, 0.9f, 0.78f, 1f));
            EnsureMarker(root, FormationUIElementIds.InfoPanelRoot);

            var iconGo = GetOrCreateUIObject(root.transform, "Icon");
            SetAnchors(iconGo.GetComponent<RectTransform>(), new Vector2(0.25f, 0.55f), new Vector2(0.75f, 0.92f));
            var iconImage = EnsureImage(iconGo, Color.white);
            iconImage.preserveAspect = true;

            var nameLabel = EnsureLabel(root.transform, string.Empty);
            SetAnchors(nameLabel.rectTransform, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.45f));

            var infoView = GetOrAddComponent<FormationInfoPanelView>(root);
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
            var root = GetOrCreateUIObject(parent, "DebugPanel");
            SetAnchors(root.GetComponent<RectTransform>(), new Vector2(0.08f, 0.87f), new Vector2(0.86f, 0.98f));
            EnsureImage(root, new Color(0f, 0f, 0f, 0.15f));
            EnsureMarker(root, FormationUIElementIds.DebugPanelRoot);

            BuildDebugLabel(root.transform, "ColumnsLabel", "X", new Vector2(0.00f, 0f), new Vector2(0.05f, 1f));
            var columnsInput = CreateInputField(root.transform, "ColumnsInput", new Vector2(0.05f, 0.1f), new Vector2(0.16f, 0.9f), TMP_InputField.ContentType.IntegerNumber);

            BuildDebugLabel(root.transform, "RowsLabel", "Y", new Vector2(0.19f, 0f), new Vector2(0.24f, 1f));
            var rowsInput = CreateInputField(root.transform, "RowsInput", new Vector2(0.24f, 0.1f), new Vector2(0.35f, 0.9f), TMP_InputField.ContentType.IntegerNumber);

            BuildDebugLabel(root.transform, "WidthLabel", "W", new Vector2(0.38f, 0f), new Vector2(0.43f, 1f));
            var widthInput = CreateInputField(root.transform, "WidthInput", new Vector2(0.43f, 0.1f), new Vector2(0.55f, 0.9f), TMP_InputField.ContentType.DecimalNumber);

            BuildDebugLabel(root.transform, "HeightLabel", "H", new Vector2(0.58f, 0f), new Vector2(0.63f, 1f));
            var heightInput = CreateInputField(root.transform, "HeightInput", new Vector2(0.63f, 0.1f), new Vector2(0.75f, 0.9f), TMP_InputField.ContentType.DecimalNumber);

            var applyGo = GetOrCreateUIObject(root.transform, "ApplyButton");
            SetAnchors(applyGo.GetComponent<RectTransform>(), new Vector2(0.78f, 0.1f), new Vector2(0.98f, 0.9f));
            EnsureImage(applyGo, new Color(0.7f, 1f, 0.7f, 1f));
            EnsureButton(applyGo);
            var applyLabel = EnsureLabel(applyGo.transform, "적용");
            applyLabel.fontSize = 18;

            var debugView = GetOrAddComponent<FormationGridDebugView>(root);
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
            var go = GetOrCreateUIObject(parent, name);
            SetAnchors(go.GetComponent<RectTransform>(), anchorMin, anchorMax);
            var label = GetOrAddComponent<TextMeshProUGUI>(go);
            label.text = text;
            label.alignment = TextAlignmentOptions.MidlineRight;
            label.fontSize = 18;
            label.color = Color.black;
            label.raycastTarget = false;
        }

        private static TMP_InputField CreateInputField(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, TMP_InputField.ContentType contentType)
        {
            var go = GetOrCreateUIObject(parent, name);
            SetAnchors(go.GetComponent<RectTransform>(), anchorMin, anchorMax);
            EnsureImage(go, new Color(1f, 1f, 1f, 0.95f));

            var textAreaGo = GetOrCreateUIObject(go.transform, "TextArea");
            var textAreaRect = textAreaGo.GetComponent<RectTransform>();
            SetStretch(textAreaRect);
            textAreaRect.offsetMin = new Vector2(6, 2);
            textAreaRect.offsetMax = new Vector2(-6, -2);
            GetOrAddComponent<RectMask2D>(textAreaGo);

            var textGo = GetOrCreateUIObject(textAreaRect, "Text");
            SetStretch(textGo.GetComponent<RectTransform>());
            var textComponent = GetOrAddComponent<TextMeshProUGUI>(textGo);
            textComponent.fontSize = 18;
            textComponent.color = Color.black;
            textComponent.alignment = TextAlignmentOptions.MidlineLeft;
            textComponent.raycastTarget = false;

            var inputField = GetOrAddComponent<TMP_InputField>(go);
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
            var (viewportRect, contentGo) = CreateViewportAndContent(root);
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

            var layoutGroup = GetOrAddComponent<GridLayoutGroup>(contentGo);
            layoutGroup.cellSize = cellSize;
            layoutGroup.spacing = Vector2.zero;
            layoutGroup.padding = new RectOffset(8, 8, 8, 8);
            layoutGroup.startCorner = GridLayoutGroup.Corner.UpperLeft;
            layoutGroup.startAxis = GridLayoutGroup.Axis.Horizontal;
            layoutGroup.childAlignment = TextAnchor.UpperLeft;
            layoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layoutGroup.constraintCount = Mathf.Max(1, columns);

            var fitter = GetOrAddComponent<ContentSizeFitter>(contentGo);
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ConfigureScrollRect(root.gameObject, viewportRect, contentRect, horizontal: true, vertical: true);

            return (viewportRect, contentRect, layoutGroup);
        }

        /// <summary>
        /// 가로 스크롤용 Viewport/Content 구조를 만들고, root에 ScrollRect를 붙여 연결한다. 팔레트가 사용한다.
        /// </summary>
        private static (RectTransform viewport, RectTransform content) BuildHorizontalScrollArea(Transform root)
        {
            var (viewportRect, contentGo) = CreateViewportAndContent(root);
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(0f, 1f);
            contentRect.pivot = new Vector2(0f, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;

            var layoutGroup = GetOrAddComponent<HorizontalLayoutGroup>(contentGo);
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = true;
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = false;
            layoutGroup.spacing = 8;
            layoutGroup.padding = new RectOffset(8, 8, 8, 8);
            layoutGroup.childAlignment = TextAnchor.MiddleLeft;

            var fitter = GetOrAddComponent<ContentSizeFitter>(contentGo);
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            ConfigureScrollRect(root.gameObject, viewportRect, contentRect, horizontal: true, vertical: false);

            return (viewportRect, contentRect);
        }

        /// <summary>
        /// 그리드/팔레트 스크롤 영역이 공유하는 Viewport(+RectMask2D)/Content 뼈대를 만든다.
        /// 앵커·레이아웃 그룹·ContentSizeFitter는 호출자가 용도에 맞게 이어서 구성한다.
        /// </summary>
        private static (RectTransform viewport, GameObject content) CreateViewportAndContent(Transform root)
        {
            var viewportGo = GetOrCreateUIObject(root, "Viewport");
            var viewportRect = viewportGo.GetComponent<RectTransform>();
            SetStretch(viewportRect);
            EnsureImage(viewportGo, new Color(1f, 1f, 1f, 0.001f));
            GetOrAddComponent<RectMask2D>(viewportGo);

            var contentGo = GetOrCreateUIObject(viewportRect, "Content");
            return (viewportRect, contentGo);
        }

        private static void ConfigureScrollRect(GameObject go, RectTransform viewport, RectTransform content, bool horizontal, bool vertical)
        {
            var scrollRect = GetOrAddComponent<ScrollRect>(go);
            scrollRect.horizontal = horizontal;
            scrollRect.vertical = vertical;
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
        }

        private static Button EnsureButton(GameObject go)
        {
            var button = GetOrAddComponent<Button>(go);
            button.targetGraphic = go.GetComponent<Image>();
            return button;
        }

        private static TMP_Text EnsureLabel(Transform parent, string text)
        {
            var labelGo = GetOrCreateUIObject(parent, "Label");
            SetStretch(labelGo.GetComponent<RectTransform>());
            var label = GetOrAddComponent<TextMeshProUGUI>(labelGo);
            label.text = text;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 24;
            label.color = Color.black;
            label.raycastTarget = false;
            return label;
        }

        private static Image EnsureImage(GameObject go, Color color)
        {
            var image = GetOrAddComponent<Image>(go);
            image.color = color;
            return image;
        }

        private static void EnsureMarker(GameObject go, string id)
        {
            var marker = GetOrAddComponent<UIElementMarker>(go);
            var so = new SerializedObject(marker);
            so.FindProperty("id").stringValue = id;
            so.ApplyModifiedProperties();
        }

        private static void DestroyChildIfExists(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
            }
        }

        private static GameObject GetOrCreateUIObject(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                return existing.gameObject;
            }

            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            Undo.SetTransformParent(go.transform, parent, $"Parent {name}");
            return go;
        }

        private static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(go);
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }

        private static void SetStretch(RectTransform rect)
        {
            SetAnchors(rect, Vector2.zero, Vector2.one);
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
            SetStretch(containerGo.GetComponent<RectTransform>());

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
