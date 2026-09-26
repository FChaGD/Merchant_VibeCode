using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 인벤토리 팝업 1개의 화면 요소 묶음(TownCategoryDepthElements와 같은 방식). 인스톨러(InventoryPopupUIBuilder)가
    /// 만든 요소를 씬 로드 시 한 번만 찾아 둔다. 하나라도 없으면 팝업 전체를 등록하지 않는다 - 요소가 빠진 팝업은
    /// 열 수는 있어도 조작이 깨지기 때문이다.
    /// </summary>
    public sealed class InventoryPopupElements
    {
        public RectTransform Root { get; private set; }
        public PointerClickRelay RootClick { get; private set; }
        public DraggableWindow TitleBar { get; private set; }
        public Button CloseButton { get; private set; }
        public RectTransform GridArea { get; private set; }
        public RectTransform GridCells { get; private set; }
        public RectTransform GridItems { get; private set; }
        public RectTransform StagingArea { get; private set; }
        public RectTransform StagingContent { get; private set; }
        public Button SortButton { get; private set; }
        public TMP_Text InfoLabel { get; private set; }
        public RectTransform DragLayer { get; private set; }
        public InventoryItemView ItemTemplate { get; private set; }
        public Image CellTemplate { get; private set; }

        public static bool TryBind(SceneUIRoot sceneUIRoot, string popupId, out InventoryPopupElements elements)
        {
            elements = null;
            var bound = new InventoryPopupElements();
            var ok = TryGet(sceneUIRoot, InventoryPopupUIElementIds.Root(popupId), out RectTransform root)
                & TryGet(sceneUIRoot, InventoryPopupUIElementIds.Root(popupId), out PointerClickRelay rootClick)
                & TryGet(sceneUIRoot, InventoryPopupUIElementIds.TitleBar(popupId), out DraggableWindow titleBar)
                & TryGet(sceneUIRoot, InventoryPopupUIElementIds.CloseButton(popupId), out Button closeButton)
                & TryGet(sceneUIRoot, InventoryPopupUIElementIds.GridArea(popupId), out RectTransform gridArea)
                & TryGet(sceneUIRoot, InventoryPopupUIElementIds.GridCells(popupId), out RectTransform gridCells)
                & TryGet(sceneUIRoot, InventoryPopupUIElementIds.GridItems(popupId), out RectTransform gridItems)
                & TryGet(sceneUIRoot, InventoryPopupUIElementIds.StagingArea(popupId), out RectTransform stagingArea)
                & TryGet(sceneUIRoot, InventoryPopupUIElementIds.StagingContent(popupId), out RectTransform stagingContent)
                & TryGet(sceneUIRoot, InventoryPopupUIElementIds.SortButton(popupId), out Button sortButton)
                & TryGet(sceneUIRoot, InventoryPopupUIElementIds.InfoLabel(popupId), out TMP_Text infoLabel)
                & TryGet(sceneUIRoot, InventoryPopupUIElementIds.DragLayer(popupId), out RectTransform dragLayer)
                & TryGet(sceneUIRoot, InventoryPopupUIElementIds.ItemTemplate(popupId), out InventoryItemView itemTemplate)
                & TryGet(sceneUIRoot, InventoryPopupUIElementIds.CellTemplate(popupId), out Image cellTemplate);

            if (!ok) return false;

            bound.Root = root;
            bound.RootClick = rootClick;
            bound.TitleBar = titleBar;
            bound.CloseButton = closeButton;
            bound.GridArea = gridArea;
            bound.GridCells = gridCells;
            bound.GridItems = gridItems;
            bound.StagingArea = stagingArea;
            bound.StagingContent = stagingContent;
            bound.SortButton = sortButton;
            bound.InfoLabel = infoLabel;
            bound.DragLayer = dragLayer;
            bound.ItemTemplate = itemTemplate;
            bound.CellTemplate = cellTemplate;
            elements = bound;
            return true;
        }

        // 빠진 요소를 한 번에 전부 드러내려고 단축 평가(&&) 대신 &로 모든 조회를 수행한다.
        private static bool TryGet<T>(SceneUIRoot sceneUIRoot, string id, out T component) where T : Component
        {
            if (sceneUIRoot.TryGetElement(id, out component)) return true;

            Debug.LogWarning($"인벤토리 팝업에서 '{id}' 요소({typeof(T).Name})를 찾을 수 없다. {nameof(UIElementMarker)}가 부착되어 있는지 확인하라(Tools > Game > Build Hub Scene).");
            return false;
        }
    }
}
