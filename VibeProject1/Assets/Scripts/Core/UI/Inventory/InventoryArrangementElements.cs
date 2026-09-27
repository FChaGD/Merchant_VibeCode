using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 인벤토리 그리드 편집 화면 요소 묶음(Docs/설계/50번 §6.4). 떠 있는 인벤토리 팝업과 무역품 구매 화면의 고정 패널이
    /// 같은 편집 본문을 쓰므로, 창 제목 줄·닫기 버튼 같은 팝업 전용 요소와 분리했다. 요소 ID는 InventoryPopupUIElementIds에
    /// 접두사(팝업 Id 또는 고정 패널 Id)를 붙여 찾는다. 하나라도 없으면 묶음 전체를 바인딩하지 않는다 - 요소가 빠진 편집
    /// 화면은 열 수는 있어도 조작이 깨지기 때문이다.
    /// </summary>
    public sealed class InventoryArrangementElements
    {
        public RectTransform Root { get; private set; }
        public PointerClickRelay RootClick { get; private set; }
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

        public static bool TryBind(SceneUIRoot sceneUIRoot, string prefix, bool hasStaging, out InventoryArrangementElements elements)
        {
            elements = null;
            var ok = TryGet(sceneUIRoot, InventoryPopupUIElementIds.Root(prefix), out RectTransform root)
                & TryGet(sceneUIRoot, InventoryPopupUIElementIds.Root(prefix), out PointerClickRelay rootClick)
                & TryGet(sceneUIRoot, InventoryPopupUIElementIds.GridArea(prefix), out RectTransform gridArea)
                & TryGet(sceneUIRoot, InventoryPopupUIElementIds.GridCells(prefix), out RectTransform gridCells)
                & TryGet(sceneUIRoot, InventoryPopupUIElementIds.GridItems(prefix), out RectTransform gridItems)
                & TryGet(sceneUIRoot, InventoryPopupUIElementIds.SortButton(prefix), out Button sortButton)
                & TryGet(sceneUIRoot, InventoryPopupUIElementIds.InfoLabel(prefix), out TMP_Text infoLabel)
                & TryGet(sceneUIRoot, InventoryPopupUIElementIds.DragLayer(prefix), out RectTransform dragLayer)
                & TryGet(sceneUIRoot, InventoryPopupUIElementIds.ItemTemplate(prefix), out InventoryItemView itemTemplate)
                & TryGet(sceneUIRoot, InventoryPopupUIElementIds.CellTemplate(prefix), out Image cellTemplate);

            RectTransform stagingArea = null;
            RectTransform stagingContent = null;
            if (hasStaging)
            {
                ok &= TryGet(sceneUIRoot, InventoryPopupUIElementIds.StagingArea(prefix), out stagingArea)
                    & TryGet(sceneUIRoot, InventoryPopupUIElementIds.StagingContent(prefix), out stagingContent);
            }

            if (!ok) return false;

            elements = new InventoryArrangementElements
            {
                Root = root,
                RootClick = rootClick,
                GridArea = gridArea,
                GridCells = gridCells,
                GridItems = gridItems,
                StagingArea = stagingArea,
                StagingContent = stagingContent,
                SortButton = sortButton,
                InfoLabel = infoLabel,
                DragLayer = dragLayer,
                ItemTemplate = itemTemplate,
                CellTemplate = cellTemplate,
            };
            return true;
        }

        // 빠진 요소를 한 번에 전부 드러내려고 단축 평가(&&) 대신 &로 모든 조회를 수행한다.
        internal static bool TryGet<T>(SceneUIRoot sceneUIRoot, string id, out T component) where T : Component
        {
            if (sceneUIRoot.TryGetElement(id, out component)) return true;

            Debug.LogWarning($"화면 요소 '{id}'({typeof(T).Name})를 찾을 수 없다. {nameof(UIElementMarker)}가 부착되어 있는지 확인하라(Tools > Game > Build Hub Scene).");
            return false;
        }
    }
}
