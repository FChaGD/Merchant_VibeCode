using Game.Core.DebugTools;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.Editor
{
    /// <summary>
    /// 배치(Formation) UI 하이어라키 조립 로직. 원래 Hub 전용 인스톨러에만 있었으나, Field 씬에서도
    /// "정비창 재호출"이 실제로 동작하려면 같은 화면을 Field의 SceneUIRoot 아래에도 만들어야 해서
    /// (FormationPanel의 실제 요소는 콘텐츠 씬이 언로드되면 함께 파괴된다) 공용 빌더로 뽑아냈다.
    /// HubSceneInstaller(Hub)/FieldUIInstaller(Field) 둘 다 이 클래스에만 의존하고, 서로의 내부
    /// 메서드를 참조하지 않는다.
    /// </summary>
    internal static class FormationUIBuilder
    {
        private const string PrefabFolder = "Assets/Prefabs/UI/Formation";
        private const string SlotPrefabPath = PrefabFolder + "/FormationSlot.prefab";
        private const string IconPrefabPath = PrefabFolder + "/FormationUnitIcon.prefab";
        private const string RowPrefabPath = PrefabFolder + "/FormationPaletteRow.prefab";

        // 그리드 배경(연한 민트색, BuildGrid 참고)과 타일이 육안으로 뚜렷이 구분되도록 대비되는 색 사용.
        private static readonly Color SlotBackgroundColor = new(1f, 0.85f, 0.6f, 0.9f);

        public static void Build(Transform parentRoot, FormationSlotView slotPrefab, FormationUnitIconView iconPrefab, FormationPaletteRowView rowPrefab)
        {
            var panelRoot = EditorUIBuilder.GetOrCreateUIObject(parentRoot, "FormationPanel");
            EditorUIBuilder.SetStretch(panelRoot.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureMarker(panelRoot, FormationUIElementIds.PanelRoot);

            BuildPalette(panelRoot.transform, rowPrefab);
            BuildTopRightButtons(panelRoot.transform);
            BuildGrid(panelRoot.transform, slotPrefab, iconPrefab);
            BuildInfoPanel(panelRoot.transform);
            BuildDebugPanel(panelRoot.transform);

            panelRoot.SetActive(false);
        }

        public static void EnsurePrefabFolder()
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

        public static FormationUnitIconView GetOrCreateIconPrefab()
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

        /// <summary>
        /// 정비창 팔레트 카테고리 한 줄(설계 16번) - 아이콘 표시/드래그는 FormationUnitIconView를
        /// 자식으로 합성해 재사용하고, 그 아래 잔여/전체 수 라벨을 붙인다. 소진 시 비활성화는
        /// FormationPaletteRowView가 루트의 CanvasGroup으로 처리한다.
        /// </summary>
        public static FormationPaletteRowView GetOrCreateRowPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(RowPrefabPath);
            if (existing != null)
            {
                return existing.GetComponent<FormationPaletteRowView>();
            }

            var go = new GameObject("FormationPaletteRow", typeof(RectTransform));
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(96, 116);
            var canvasGroup = go.AddComponent<CanvasGroup>();

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(go.transform, false);
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.2f);
            iconRect.anchorMax = new Vector2(1f, 1f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconImage = iconGo.AddComponent<Image>();
            iconImage.color = Color.white;
            var iconView = iconGo.AddComponent<FormationUnitIconView>();
            var iconSo = new SerializedObject(iconView);
            iconSo.FindProperty("iconImage").objectReferenceValue = iconImage;
            iconSo.ApplyModifiedProperties();

            var countGo = new GameObject("CountLabel", typeof(RectTransform));
            countGo.transform.SetParent(go.transform, false);
            var countRect = countGo.GetComponent<RectTransform>();
            countRect.anchorMin = new Vector2(0f, 0f);
            countRect.anchorMax = new Vector2(1f, 0.2f);
            countRect.offsetMin = Vector2.zero;
            countRect.offsetMax = Vector2.zero;
            var countLabel = countGo.AddComponent<TextMeshProUGUI>();
            countLabel.alignment = TextAlignmentOptions.Center;
            countLabel.fontSize = 16;
            countLabel.color = Color.black;
            countLabel.raycastTarget = false;

            var rowView = go.AddComponent<FormationPaletteRowView>();
            var rowSo = new SerializedObject(rowView);
            rowSo.FindProperty("iconView").objectReferenceValue = iconView;
            rowSo.FindProperty("countLabel").objectReferenceValue = countLabel;
            rowSo.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            rowSo.ApplyModifiedProperties();

            var savedRowPrefab = PrefabUtility.SaveAsPrefabAsset(go, RowPrefabPath);
            Object.DestroyImmediate(go);

            return savedRowPrefab.GetComponent<FormationPaletteRowView>();
        }

        public static FormationSlotView GetOrCreateSlotPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPrefabPath);
            if (existing != null)
            {
                // 색상 등은 재실행 시 최신 값으로 동기화한다 - 기존 프리팹이 옛 설정(대비가 약한 색)을
                // 갖고 있을 수 있다.
                var existingImage = existing.GetComponent<Image>();
                existingImage.color = SlotBackgroundColor;
                EditorUtility.SetDirty(existing);
                return existing.GetComponent<FormationSlotView>();
            }

            var go = new GameObject("FormationSlot", typeof(RectTransform));
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(120, 120);

            var bgImage = go.AddComponent<Image>();
            bgImage.color = SlotBackgroundColor;

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

        private static void BuildPalette(Transform parent, FormationPaletteRowView rowPrefab)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(parent, "Palette");
            EditorUIBuilder.SetAnchors(root.GetComponent<RectTransform>(), new Vector2(0.08f, 0.75f), new Vector2(0.62f, 0.85f));
            EditorUIBuilder.EnsureImage(root, new Color(1f, 0.85f, 0.85f, 1f));
            EditorUIBuilder.EnsureMarker(root, FormationUIElementIds.PaletteRoot);

            var (_, content) = BuildHorizontalScrollArea(root.transform);

            var paletteView = EditorUIBuilder.GetOrAddComponent<FormationPaletteView>(root);
            var so = new SerializedObject(paletteView);
            so.FindProperty("rowContent").objectReferenceValue = content;
            so.FindProperty("rowPrefab").objectReferenceValue = rowPrefab;
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
            // 드래그로만 스크롤한다 - 마우스 휠 스크롤은 쓰지 않는다(사용자 확인).
            root.GetComponent<ScrollRect>().scrollSensitivity = 0f;

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
    }
}
