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
        private const string PathLinePrefabPath = PrefabFolder + "/FormationPathLine.prefab";
        private const string TravelerIconPrefabPath = PrefabFolder + "/FormationTravelerIcon.prefab";
        private const string ActivityOverlayPrefabPath = PrefabFolder + "/FormationActivityOverlay.prefab";

        // 그리드 배경(연한 민트색, BuildGrid 참고)과 타일이 육안으로 뚜렷이 구분되도록 대비되는 색 사용.
        private static readonly Color SlotBackgroundColor = new(1f, 0.85f, 0.6f, 0.9f);

        // includeApplyButton: Hub는 true(로컬 편집+적용 버튼), Field는 false(즉시 반영이라 적용
        // 버튼 자체가 없음, Docs/설계/25번 §2.3/§9-11).
        public static void Build(Transform parentRoot, FormationSlotView slotPrefab, FormationUnitIconView iconPrefab, FormationPaletteRowView rowPrefab, FormationPathLineView pathLinePrefab, Image travelerIconPrefab, FormationActivityOverlayView activityOverlayPrefab, bool includeApplyButton)
        {
            var panelRoot = EditorUIBuilder.GetOrCreateUIObject(parentRoot, "FormationPanel");
            EditorUIBuilder.SetStretch(panelRoot.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureMarker(panelRoot, FormationUIElementIds.PanelRoot);

            BuildPalette(panelRoot.transform, rowPrefab);
            BuildTopRightButtons(panelRoot.transform, includeApplyButton);
            BuildGrid(panelRoot.transform, slotPrefab, iconPrefab, pathLinePrefab, travelerIconPrefab, activityOverlayPrefab);
            BuildInfoPanel(panelRoot.transform);
            BuildDebugPanel(panelRoot.transform);
            BuildRepairControls(panelRoot.transform);

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
            var countLabel = EditorUIBuilder.EnsureText(countGo);
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

        // 배치/이동 진행 표시(반투명 아이콘 + 잔여 초, 기획 20번 §3.2/§3.3, 설계 25번 §5.1 갱신) -
        // 슬롯의 자식이 아니라 FormationGridView가 slotContent 아래 별도 풀로 관리하는 독립
        // 프리팹이다(렌더 순서를 슬롯/경로선/이동 아이콘과 독립적으로 강제해야 해서, 실전 확인).
        // 경로선/이동 아이콘과 같은 이유로 slotContent 좌상단 기준 좌표계 + LayoutElement.ignoreLayout이 필요하다.
        public static FormationActivityOverlayView GetOrCreateActivityOverlayPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ActivityOverlayPrefabPath);
            if (existing != null && HasUpToDateAnchor(existing))
            {
                return existing.GetComponent<FormationActivityOverlayView>();
            }

            var go = new GameObject("FormationActivityOverlay", typeof(RectTransform));
            var goRect = (RectTransform)go.transform;
            goRect.anchorMin = goRect.anchorMax = new Vector2(0f, 1f);
            goRect.pivot = new Vector2(0.5f, 0.5f);
            goRect.sizeDelta = new Vector2(96f, 96f);
            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(go.transform, false);
            EditorUIBuilder.SetStretch((RectTransform)iconGo.transform);
            var iconImage = iconGo.AddComponent<Image>();
            iconImage.color = new Color(1f, 1f, 1f, 0.5f); // 반투명(사용자 확정 - 출발/도착 마크는 이 상태 유지)
            iconImage.raycastTarget = false;

            var textGo = new GameObject("RemainingSecondsText", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 0.4f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = EditorUIBuilder.EnsureText(textGo);
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 20;
            text.color = Color.black;
            text.raycastTarget = false;

            var overlay = go.AddComponent<FormationActivityOverlayView>();
            var so = new SerializedObject(overlay);
            so.FindProperty("iconImage").objectReferenceValue = iconImage;
            so.FindProperty("remainingSecondsText").objectReferenceValue = text;
            so.ApplyModifiedProperties();

            go.SetActive(false); // 프리팹 자체는 비활성 원본 - Instantiate 후 SetActivityOverlays가 켠다

            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(go, ActivityOverlayPrefabPath);
            Object.DestroyImmediate(go);

            return savedPrefab.GetComponent<FormationActivityOverlayView>();
        }

        // 이동 경로선 세그먼트 프리팹(설계 25번 §5.2) - 오직 선분만 담당한다. 이동 중인 유닛
        // 아이콘은 렌더 순서(선은 아래/아이콘은 위, 실전 확인)가 달라야 해서 별도 프리팹
        // (GetOrCreateTravelerIconPrefab)으로 분리했다 - 같은 오브젝트에 두면 형제 인덱스를
        // 독립적으로 강제할 수 없다. FormationGridView.pathLinePrefab이 Instantiate로 재사용한다.
        public static FormationPathLineView GetOrCreatePathLinePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PathLinePrefabPath);
            if (existing != null && HasUpToDateAnchor(existing))
            {
                return existing.GetComponent<FormationPathLineView>();
            }

            var go = new GameObject("FormationPathLine", typeof(RectTransform));
            // 이 루트의 앵커/피벗을 slotContent와 같은 좌상단 기준(0,1)으로 맞춘다 - 기본값(중앙
            // 고정 앵커)으로 두면 이 루트 자신이 slotContent 중앙에 위치하게 되어, 자식(선분)에
            // slotContent 좌상단 기준으로 계산해 넣는 anchoredPosition(GetSlotAnchoredPosition
            // 참고)이 엉뚱한 원점에서 계산돼 화면 밖으로 어긋난다(실전 확인된 버그).
            var goRect = (RectTransform)go.transform;
            goRect.anchorMin = goRect.anchorMax = new Vector2(0f, 1f);
            goRect.pivot = new Vector2(0f, 1f);
            goRect.anchoredPosition = Vector2.zero;
            // slotContent에는 GridLayoutGroup이 붙어 있어(BuildGridScrollArea) 직계 자식을 전부
            // 격자 칸으로 취급해 강제로 재배치한다 - 이 경로선은 슬롯과 같은 좌표계를 공유해야
            // 하지만 격자 칸이 아니므로, LayoutElement.ignoreLayout으로 그 강제 배치에서 제외한다
            // (실전 확인된 버그 - 이 설정이 없으면 다음 빈 격자 칸 위치로 밀려나 화면 밖으로
            // 벗어나 보이지 않았다).
            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;

            var segmentGo = new GameObject("PathSegment", typeof(RectTransform), typeof(Image));
            segmentGo.transform.SetParent(go.transform, false);
            var segmentRect = (RectTransform)segmentGo.transform;
            segmentRect.anchorMin = segmentRect.anchorMax = new Vector2(0f, 1f);
            segmentRect.pivot = new Vector2(0.5f, 0.5f);
            var segmentImage = segmentGo.GetComponent<Image>();
            segmentImage.color = new Color(1f, 0.9f, 0.2f, 1f); // 불투명(사용자 확정)
            segmentImage.raycastTarget = false;
            segmentGo.SetActive(false); // 프리팹 자체는 비활성 원본 - Instantiate 후 SetPath가 켠다

            var pathLineView = go.AddComponent<FormationPathLineView>();
            var so = new SerializedObject(pathLineView);
            so.FindProperty("segmentPrefab").objectReferenceValue = segmentImage;
            so.ApplyModifiedProperties();

            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(go, PathLinePrefabPath);
            Object.DestroyImmediate(go);

            return savedPrefab.GetComponent<FormationPathLineView>();
        }

        // 이동 중인 유닛 아이콘 프리팹(기획 20번 §3.3, 설계 25번 §5.2 갱신) - 항상 불투명, 항상
        // 모든 슬롯/오버레이/경로선보다 위(FormationGridView.SetMovePaths가 매번 SetAsLastSibling
        // 강제). 경로선과 마찬가지로 slotContent 좌상단 기준 좌표계+LayoutElement.ignoreLayout이 필요하다.
        public static Image GetOrCreateTravelerIconPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(TravelerIconPrefabPath);
            if (existing != null && HasUpToDateAnchor(existing))
            {
                return existing.GetComponent<Image>();
            }

            var go = new GameObject("FormationTravelerIcon", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(72f, 72f);
            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;
            var image = go.GetComponent<Image>();
            image.color = Color.white; // 불투명(사용자 확정) - 유닛 아이콘 스프라이트를 그대로 보여준다
            image.raycastTarget = false;
            go.SetActive(false); // 프리팹 자체는 비활성 원본 - Instantiate 후 SetMovePaths가 켠다

            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(go, TravelerIconPrefabPath);
            Object.DestroyImmediate(go);

            return savedPrefab.GetComponent<Image>();
        }

        // 설계 25번 §5 구버전(GridLayoutGroup에 의해 화면 밖으로 밀려나던 LayoutElement 누락 버전,
        // 또는 루트 앵커가 slotContent 좌상단 기준과 안 맞아 자식 좌표가 어긋나던 버전)인지 판별 -
        // 있으면 통째로 재생성한다. 피벗은 검사하지 않는다 - 경로선(위치 컨테이너, pivot=(0,1))과
        // 이동 아이콘/오버레이 마크(자기 자신이 중심에 오도록, pivot=(0.5,0.5))가 서로 다른 값을
        // 정당하게 쓰기 때문이다.
        private static bool HasUpToDateAnchor(GameObject prefabRoot)
        {
            var layoutElement = prefabRoot.GetComponent<LayoutElement>();
            if (layoutElement == null || !layoutElement.ignoreLayout) return false;

            var rect = (RectTransform)prefabRoot.transform;
            return rect.anchorMin == new Vector2(0f, 1f) && rect.anchorMax == new Vector2(0f, 1f);
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

        private static void BuildTopRightButtons(Transform parent, bool includeApplyButton)
        {
            // 이전 버전("저장" 표기)에 남아있을 수 있는 오브젝트는 제거하고 "적용"으로 새로 만든다.
            EditorUIBuilder.DestroyChildIfExists(parent, "SaveButton");

            if (includeApplyButton)
            {
                var applyGo = EditorUIBuilder.GetOrCreateUIObject(parent, "ApplyButton");
                EditorUIBuilder.SetAnchors(applyGo.GetComponent<RectTransform>(), new Vector2(0.64f, 0.75f), new Vector2(0.76f, 0.85f));
                EditorUIBuilder.EnsureImage(applyGo, new Color(0.75f, 0.87f, 1f, 1f));
                EditorUIBuilder.EnsureButton(applyGo);
                EditorUIBuilder.EnsureLabel(applyGo.transform, "적용");
                EditorUIBuilder.EnsureMarker(applyGo, FormationUIElementIds.ApplyButton);
            }
            else
            {
                // Field는 즉시 반영이라 적용 버튼이 없다(Docs/기획/20번 §3.1) - 이전에 Hub와 같은
                // 프리팹 구성을 썼을 때 남아있을 수 있는 버튼을 정리한다(재실행 안전성).
                EditorUIBuilder.DestroyChildIfExists(parent, "ApplyButton");
            }

            var closeGo = EditorUIBuilder.GetOrCreateUIObject(parent, "CloseButton");
            EditorUIBuilder.SetAnchors(closeGo.GetComponent<RectTransform>(), includeApplyButton ? new Vector2(0.78f, 0.75f) : new Vector2(0.64f, 0.75f), includeApplyButton ? new Vector2(0.86f, 0.85f) : new Vector2(0.76f, 0.85f));
            EditorUIBuilder.EnsureImage(closeGo, new Color(0.85f, 0.85f, 0.85f, 1f));
            EditorUIBuilder.EnsureButton(closeGo);
            EditorUIBuilder.EnsureLabel(closeGo.transform, "닫기");
            EditorUIBuilder.EnsureMarker(closeGo, FormationUIElementIds.CloseButton);
        }

        private static void BuildGrid(Transform parent, FormationSlotView slotPrefab, FormationUnitIconView occupantIconPrefab, FormationPathLineView pathLinePrefab, Image travelerIconPrefab, FormationActivityOverlayView activityOverlayPrefab)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(parent, "Grid");
            EditorUIBuilder.SetAnchors(root.GetComponent<RectTransform>(), new Vector2(0.08f, 0.30f), new Vector2(0.64f, 0.74f));
            // 격자 바탕 = 판 밖(어두운 회색). 민트 판 칸은 FormationGridView/FormationSlotView가 칸마다 칠한다(2026-09-29, 판 경계 구분).
            EditorUIBuilder.EnsureImage(root, new Color(0.25f, 0.25f, 0.27f, 1f));
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
            so.FindProperty("pathLinePrefab").objectReferenceValue = pathLinePrefab;
            so.FindProperty("travelerIconPrefab").objectReferenceValue = travelerIconPrefab;
            so.FindProperty("activityOverlayPrefab").objectReferenceValue = activityOverlayPrefab;
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
        /// 상행 중 정비창 정리 모드(Docs/설계/79번 §8)의 안내 라벨·[완료] 버튼. 마을 정비창도 이 빌더를 쓰지만 쓰지 않으므로 둘 다
        /// 저장 상태는 비활성이고 Field 정리 모드에서만 런타임이 켠다(라벨·버튼 자기 오브젝트를 토글하므로 마커도 그 오브젝트에 둔다).
        /// 상단은 디버그 핀 패널이 차지해 안내는 격자 바로 아래, [완료]는 정리 모드에서 숨는 Field 닫기 버튼 자리에 둔다.
        /// 정비창 패널 루트에 배경이 없어 뒤 전투 화면(어두운 배경)이 비치므로 안내 문구는 밝은 색으로 쓴다.
        /// </summary>
        private static void BuildRepairControls(Transform parent)
        {
            var guideGo = EditorUIBuilder.GetOrCreateUIObject(parent, "RepairGuideLabel");
            EditorUIBuilder.SetAnchors(guideGo.GetComponent<RectTransform>(), new Vector2(0.08f, 0.22f), new Vector2(0.86f, 0.29f));
            var guide = EditorUIBuilder.EnsureText(guideGo);
            guide.text = string.Empty;
            guide.alignment = TextAlignmentOptions.Center;
            guide.enableAutoSizing = true;
            guide.fontSizeMin = 14f;
            guide.fontSizeMax = 28f;
            guide.fontStyle = FontStyles.Bold;
            guide.color = new Color(1f, 0.85f, 0.35f, 1f);
            guide.raycastTarget = false;
            EditorUIBuilder.EnsureMarker(guideGo, FormationUIElementIds.RepairGuideLabel);
            guideGo.SetActive(false);

            var doneGo = EditorUIBuilder.GetOrCreateUIObject(parent, "RepairDoneButton");
            EditorUIBuilder.SetAnchors(doneGo.GetComponent<RectTransform>(), new Vector2(0.64f, 0.75f), new Vector2(0.76f, 0.85f));
            EditorUIBuilder.EnsureImage(doneGo, new Color(0.75f, 0.87f, 1f, 1f));
            EditorUIBuilder.EnsureButton(doneGo);
            EditorUIBuilder.EnsureLabel(doneGo.transform, "완료");
            EditorUIBuilder.EnsureMarker(doneGo, FormationUIElementIds.RepairDoneButton);
            doneGo.SetActive(false);
        }

        /// <summary>
        /// 정비창 디버그 핀 패널(Docs/기획/59번 §4.5, 설계 60번 §8): 모양 마스크 입력(기획 65번) + 형식 오류 안내 + 끌어서 격자에 놓는 핀 아이콘. 격자 위 핀 표시를 누르면 제거를
        /// 시도한다. 상단 여백(팔레트/버튼 행 위쪽, 목업에는 없는 영역)에 배치한다. 핀 저장소는 디버그 설치기(FormationDebugPinInstaller)가
        /// Bootstrap에 붙인다 - 저장소가 없으면 이 패널은 보이지만 핀이 저장되지 않는다.
        /// </summary>
        private static void BuildDebugPanel(Transform parent)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(parent, "DebugPanel");
            EditorUIBuilder.SetAnchors(root.GetComponent<RectTransform>(), new Vector2(0.08f, 0.87f), new Vector2(0.86f, 0.98f));
            EditorUIBuilder.EnsureImage(root, new Color(0f, 0f, 0f, 0.15f));
            EditorUIBuilder.EnsureMarker(root, FormationUIElementIds.DebugPanelRoot);

            // 옛 입력 정리 - 판 크기 조절(열·행·칸 크기·적용)은 외곽 판이 50 × 50 고정이 되며, 핀 반경 1칸은 상하좌우 4칸으로(기획 61번),
            // 다시 모양 마스크 1칸으로(기획 65번) 바뀌며 없앴다.
            foreach (var stale in new[]
            {
                "ColumnsLabel", "ColumnsInput", "RowsLabel", "RowsInput", "WidthLabel", "WidthInput", "HeightLabel", "HeightInput", "ApplyButton", "RadiusLabel", "RadiusInput",
                "UpLabel", "UpInput", "DownLabel", "DownInput", "LeftLabel", "LeftInput", "RightLabel", "RightInput",
            })
            {
                EditorUIBuilder.DestroyChildIfExists(root.transform, stale);
            }

            // 핀 모양 마스크 입력(기획 65번 §3.3) - 9 × 9 기본값이 89자라 폭을 넉넉히 준다.
            BuildDebugLabel(root.transform, "ShapeLabel", "모양", new Vector2(0.00f, 0f), new Vector2(0.06f, 1f));
            var shapeInput = CreateInputField(root.transform, "ShapeInput", new Vector2(0.065f, 0.1f), new Vector2(0.44f, 0.9f), TMP_InputField.ContentType.Standard);

            var pinGo = EditorUIBuilder.GetOrCreateUIObject(root.transform, "PinHandle");
            EditorUIBuilder.SetAnchors(pinGo.GetComponent<RectTransform>(), new Vector2(0.46f, 0.15f), new Vector2(0.50f, 0.85f));
            EditorUIBuilder.EnsureImage(pinGo, new Color(0.85f, 0.1f, 0.85f, 0.95f));
            var pinHandle = EditorUIBuilder.GetOrAddComponent<FormationDebugPinHandle>(pinGo);

            BuildDebugLabel(root.transform, "PinHint", "← 핀을 격자에 끌어다 놓기 / 핀 클릭 = 제거", new Vector2(0.51f, 0.5f), new Vector2(0.99f, 1f));
            root.transform.Find("PinHint").GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;

            // 모양 형식 오류 안내(기획 65번 §3.3) - 런타임이 채운다.
            BuildDebugLabel(root.transform, "PinMessage", string.Empty, new Vector2(0.51f, 0f), new Vector2(0.99f, 0.5f));
            var pinMessage = root.transform.Find("PinMessage").GetComponent<TextMeshProUGUI>();
            pinMessage.alignment = TextAlignmentOptions.MidlineLeft;
            pinMessage.color = new Color(0.75f, 0.1f, 0.1f, 1f);

            var debugView = EditorUIBuilder.GetOrAddComponent<FormationGridDebugView>(root);
            var so = new SerializedObject(debugView);
            so.FindProperty("shapeInput").objectReferenceValue = shapeInput;
            so.FindProperty("messageLabel").objectReferenceValue = pinMessage;
            so.FindProperty("pinHandle").objectReferenceValue = pinHandle;
            so.ApplyModifiedProperties();
        }

        private static void BuildDebugLabel(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, name);
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), anchorMin, anchorMax);
            var label = EditorUIBuilder.EnsureText(go);
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
            var textComponent = EditorUIBuilder.EnsureText(textGo);
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
