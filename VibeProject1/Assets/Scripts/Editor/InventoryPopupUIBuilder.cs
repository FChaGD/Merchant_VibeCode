using Game.Core;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.Editor
{
    /// <summary>
    /// 인벤토리 팝업 1개의 화면 요소를 get-or-create로 조립한다(Docs/설계/40번 §7). 팝업 4종이 같은 구조를
    /// 공유하므로 특정 인스톨러(HubSceneInstaller) 내부 메서드로 두지 않고 별도 유틸리티로 뽑았다 - 다른 씬의
    /// 인스톨러가 쓰게 되더라도 인스톨러끼리 서로의 내부에 의존하지 않는다. 저수준 조립은 EditorUIBuilder에 위임한다.
    /// 요소 ID는 InventoryPopupUIElementIds(런타임 바인더 InventoryPopupElements와 공유)를 쓴다.
    /// </summary>
    internal static class InventoryPopupUIBuilder
    {
        private static readonly Color WindowColor = new(0.96f, 0.95f, 0.92f, 1f);
        private static readonly Color TitleBarColor = new(0.72f, 0.66f, 0.55f, 1f);
        private static readonly Color StagingColor = new(0.9f, 0.88f, 0.84f, 1f);
        private static readonly Color ButtonColor = new(0.85f, 0.85f, 0.85f, 1f);
        private static readonly Color SelectionOutlineColor = new(0.1f, 0.1f, 0.1f, 1f);

        // 기본 위치: 화면 좌측(Docs/기획/39번 §4.4). 이후 위치는 창 드래그로 바뀌고 런타임에만 기억한다.
        private static readonly Vector2 DefaultAnchorMin = new(0.02f, 0.16f);
        private static readonly Vector2 DefaultAnchorMax = new(0.55f, 0.8f);

        public static void Build(Transform parent, string popupId, string title)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(parent, $"InventoryPopup_{popupId}");
            var rootRect = root.GetComponent<RectTransform>();
            EditorUIBuilder.SetAnchors(rootRect, DefaultAnchorMin, DefaultAnchorMax);
            EditorUIBuilder.EnsureImage(root, WindowColor);
            EditorUIBuilder.GetOrAddComponent<PointerClickRelay>(root);
            EditorUIBuilder.EnsureMarker(root, InventoryPopupUIElementIds.Root(popupId));

            BuildTitleBar(rootRect, popupId, title);
            BuildCloseButton(rootRect, popupId);
            BuildGridArea(rootRect, popupId);
            BuildStagingArea(rootRect, popupId);
            BuildBottomRow(rootRect, popupId);
            BuildTemplates(rootRect, popupId);

            // 드래그 고스트는 창 안의 다른 요소(임시 보관 목록 마스크 포함) 위에 그려져야 한다 - 항상 마지막 자식.
            var dragLayer = EditorUIBuilder.GetOrCreateUIObject(rootRect, "DragLayer");
            EditorUIBuilder.SetStretch(dragLayer.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureMarker(dragLayer, InventoryPopupUIElementIds.DragLayer(popupId));
            dragLayer.transform.SetAsLastSibling();

            // 런타임 패널이 등록될 때 숨기지만, 씬 편집 화면에서 다른 UI를 가리지 않도록 저장 상태도 비활성.
            root.SetActive(false);
        }

        private static void BuildTitleBar(RectTransform root, string popupId, string title)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(root, "TitleBar");
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), new Vector2(0f, 0.9f), Vector2.one);
            EditorUIBuilder.EnsureImage(go, TitleBarColor);
            var window = EditorUIBuilder.GetOrAddComponent<DraggableWindow>(go);
            var so = new SerializedObject(window);
            so.FindProperty("window").objectReferenceValue = root;
            so.ApplyModifiedProperties();
            EditorUIBuilder.EnsureLabel(go.transform, title, autoSize: true, minFontSize: 12f, maxFontSize: 26f);
            EditorUIBuilder.EnsureMarker(go, InventoryPopupUIElementIds.TitleBar(popupId));
        }

        private static void BuildCloseButton(RectTransform root, string popupId)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(root, "CloseButton");
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), new Vector2(0.93f, 0.91f), new Vector2(0.99f, 0.99f));
            EditorUIBuilder.EnsureImage(go, ButtonColor);
            EditorUIBuilder.EnsureButton(go);
            EditorUIBuilder.EnsureLabel(go.transform, "X", autoSize: true, minFontSize: 10f, maxFontSize: 24f);
            EditorUIBuilder.EnsureMarker(go, InventoryPopupUIElementIds.CloseButton(popupId));
        }

        private static void BuildGridArea(RectTransform root, string popupId)
        {
            var area = EditorUIBuilder.GetOrCreateUIObject(root, "GridArea");
            EditorUIBuilder.SetAnchors(area.GetComponent<RectTransform>(), new Vector2(0.02f, 0.13f), new Vector2(0.72f, 0.88f));
            EditorUIBuilder.EnsureMarker(area, InventoryPopupUIElementIds.GridArea(popupId));

            var cells = EditorUIBuilder.GetOrCreateUIObject(area.transform, "Cells");
            EditorUIBuilder.SetStretch(cells.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureMarker(cells, InventoryPopupUIElementIds.GridCells(popupId));

            var items = EditorUIBuilder.GetOrCreateUIObject(area.transform, "Items");
            EditorUIBuilder.SetStretch(items.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureMarker(items, InventoryPopupUIElementIds.GridItems(popupId));
            items.transform.SetAsLastSibling(); // 아이템은 칸 위에 그린다.
        }

        private static void BuildStagingArea(RectTransform root, string popupId)
        {
            var header = EditorUIBuilder.GetOrCreateUIObject(root, "StagingHeader");
            EditorUIBuilder.SetAnchors(header.GetComponent<RectTransform>(), new Vector2(0.74f, 0.83f), new Vector2(0.98f, 0.88f));
            EditorUIBuilder.EnsureLabel(header.transform, "임시 보관", autoSize: true, minFontSize: 10f, maxFontSize: 20f);

            var area = EditorUIBuilder.GetOrCreateUIObject(root, "StagingArea");
            var areaRect = area.GetComponent<RectTransform>();
            EditorUIBuilder.SetAnchors(areaRect, new Vector2(0.74f, 0.13f), new Vector2(0.98f, 0.83f));
            EditorUIBuilder.EnsureImage(area, StagingColor);
            EditorUIBuilder.EnsureMarker(area, InventoryPopupUIElementIds.StagingArea(popupId));

            var (viewport, content) = EditorUIBuilder.CreateViewportAndContent(areaRect);
            var contentRect = content.GetComponent<RectTransform>();
            // 위쪽 고정·가로 늘림 - 높이는 런타임(InventoryStagingView)이 목록 길이에 맞춰 정한다.
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);
            EditorUIBuilder.EnsureMarker(content, InventoryPopupUIElementIds.StagingContent(popupId));
            EditorUIBuilder.ConfigureScrollRect(area, viewport, contentRect, horizontal: false, vertical: true);
        }

        private static void BuildBottomRow(RectTransform root, string popupId)
        {
            var sort = EditorUIBuilder.GetOrCreateUIObject(root, "SortButton");
            EditorUIBuilder.SetAnchors(sort.GetComponent<RectTransform>(), new Vector2(0.02f, 0.02f), new Vector2(0.2f, 0.11f));
            EditorUIBuilder.EnsureImage(sort, ButtonColor);
            EditorUIBuilder.EnsureButton(sort);
            EditorUIBuilder.EnsureLabel(sort.transform, "자동 정렬", autoSize: true, minFontSize: 10f, maxFontSize: 22f);
            EditorUIBuilder.EnsureMarker(sort, InventoryPopupUIElementIds.SortButton(popupId));

            var info = EditorUIBuilder.GetOrCreateUIObject(root, "Info");
            EditorUIBuilder.SetAnchors(info.GetComponent<RectTransform>(), new Vector2(0.22f, 0.02f), new Vector2(0.98f, 0.11f));
            var label = EditorUIBuilder.EnsureLabel(info.transform, string.Empty, autoSize: true, minFontSize: 10f, maxFontSize: 22f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            EditorUIBuilder.EnsureMarker(label.gameObject, InventoryPopupUIElementIds.InfoLabel(popupId));
        }

        // 런타임이 복제해 재사용하는 원본. 비활성 상태로 두며 SceneUIRoot는 비활성 요소도 수집한다.
        private static void BuildTemplates(RectTransform root, string popupId)
        {
            var templates = EditorUIBuilder.GetOrCreateUIObject(root, "Templates");
            EditorUIBuilder.SetStretch(templates.GetComponent<RectTransform>());

            var item = EditorUIBuilder.GetOrCreateUIObject(templates.transform, "ItemTemplate");
            var background = EditorUIBuilder.EnsureImage(item, Color.white);
            var outline = EditorUIBuilder.GetOrAddComponent<Outline>(item);
            outline.effectColor = SelectionOutlineColor;
            outline.effectDistance = new Vector2(3f, -3f);
            outline.enabled = false;
            var label = EditorUIBuilder.EnsureLabel(item.transform, string.Empty, autoSize: true, minFontSize: 8f, maxFontSize: 18f);
            var view = EditorUIBuilder.GetOrAddComponent<InventoryItemView>(item);
            var so = new SerializedObject(view);
            so.FindProperty("background").objectReferenceValue = background;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("selectionOutline").objectReferenceValue = outline;
            so.ApplyModifiedProperties();
            EditorUIBuilder.EnsureMarker(item, InventoryPopupUIElementIds.ItemTemplate(popupId));
            item.SetActive(false);

            var cell = EditorUIBuilder.GetOrCreateUIObject(templates.transform, "CellTemplate");
            EditorUIBuilder.EnsureImage(cell, Color.white).raycastTarget = false;
            EditorUIBuilder.EnsureMarker(cell, InventoryPopupUIElementIds.CellTemplate(popupId));
            cell.SetActive(false);
        }
    }
}
