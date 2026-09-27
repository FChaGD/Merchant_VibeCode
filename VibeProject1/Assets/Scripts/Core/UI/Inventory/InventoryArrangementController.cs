using System;

namespace Game.Core
{
    /// <summary>
    /// 인벤토리 그리드 편집(렌더링·드래그 이동·교환·회전·임시 보관·자동 정렬·선택·안내 문구)을 담당한다(Docs/설계/50번 §6.4).
    /// 원래 InventoryPopupPanel 안에 있었지만 무역품 구매 화면의 고정 패널도 같은 편집이 필요해 떠 있는 창 기능(창 드래그·
    /// 닫기 버튼·창 위치)과 분리했다. 저장소(Bootstrap 상주)의 OnChanged를 구독하므로 소유자가 교체될 때 반드시 Dispose한다.
    /// 표시 여부는 소유자가 Show/Hide로 알려 준다 - 숨겨진 동안에는 저장소가 바뀌어도 다시 그리지 않는다.
    /// </summary>
    internal sealed class InventoryArrangementController : IDisposable
    {
        private const string SortFailedMessage = "공간이 부족해 정렬할 수 없습니다.";

        private readonly InventoryArrangementElements elements;
        private readonly IInventoryReader reader;
        private readonly IInventoryArrangement arrangement;
        private readonly InventoryGridView gridView;
        private readonly InventoryStagingView stagingView; // 임시 보관이 꺼진 화면은 null
        private readonly InventoryItemDragController dragController;

        private bool isShown;
        private string selectedInstanceId;
        private string message;

        public InventoryArrangementController(InventoryArrangementElements elements, IInventoryReader reader, IInventoryArrangement arrangement, bool allowsRotation, bool hasStaging)
        {
            this.elements = elements;
            this.reader = reader;
            this.arrangement = arrangement;

            elements.ItemTemplate.gameObject.SetActive(false);
            elements.CellTemplate.gameObject.SetActive(false);
            gridView = new InventoryGridView(elements.GridArea, elements.GridCells, elements.GridItems, elements.CellTemplate, elements.ItemTemplate);
            stagingView = hasStaging ? new InventoryStagingView(elements.StagingArea, elements.StagingContent, elements.ItemTemplate) : null;
            dragController = new InventoryItemDragController(reader, arrangement, gridView, stagingView, elements.DragLayer, elements.ItemTemplate, allowsRotation);

            elements.SortButton.onClick.RemoveAllListeners();
            elements.SortButton.onClick.AddListener(Sort);
            elements.RootClick.Clicked += ClearSelection;
            reader.OnChanged += HandleInventoryChanged;
        }

        public void Show()
        {
            isShown = true;
            message = null;
            Refresh();
        }

        public void Hide()
        {
            dragController.Cancel();
            isShown = false;
            selectedInstanceId = null;
            message = null;
        }

        /// <summary>
        /// 임시 보관 아이템을 그리드 빈칸에 자동 배치한다. 전부 넣지 못하면 아무것도 옮기지 않고 false - 호출자가 안내 문구를
        /// 띄우고 닫기(나가기)를 막는다(Docs/기획/39번 §3.5, 48번 §4.5).
        /// </summary>
        public bool TryFlushStaging()
        {
            dragController.Cancel();
            if (arrangement.StagedItems.Count == 0) return true;

            return InventoryAutoSorter.TryBuildStagedFlush(reader.GridWidth, reader.GridHeight, reader.Items, arrangement.StagedItems, out var placements)
                && arrangement.TryApplyPlacements(placements);
        }

        public void ShowMessage(string text)
        {
            message = text;
            UpdateInfoLabel();
        }

        public void Dispose()
        {
            reader.OnChanged -= HandleInventoryChanged;
            elements.RootClick.Clicked -= ClearSelection;
            dragController.Dispose();
        }

        private void Sort()
        {
            dragController.Cancel();
            if (InventoryAutoSorter.TryBuildSortedLayout(reader.GridWidth, reader.GridHeight, reader.Items, arrangement.StagedItems, out var placements)
                && arrangement.TryApplyPlacements(placements))
            {
                message = null;
                return; // OnChanged → Refresh
            }

            ShowMessage(SortFailedMessage);
        }

        private void HandleInventoryChanged()
        {
            if (isShown && !dragController.IsDragging) Refresh();
        }

        private void Refresh()
        {
            if (selectedInstanceId != null && !Contains(selectedInstanceId)) selectedInstanceId = null;

            gridView.Render(reader, ConfigureItemView);
            stagingView?.Render(arrangement.StagedItems, gridView.CellSize, ConfigureItemView);
            UpdateInfoLabel();
        }

        private void ConfigureItemView(InventoryItemView view)
        {
            view.SetSelected(view.Item.InstanceId == selectedInstanceId);
            view.SetHandlers(
                clickHandler: clicked => Select(clicked.Item.InstanceId),
                beginDragHandler: (dragged, eventData) =>
                {
                    Select(dragged.Item.InstanceId);
                    dragController.Begin(dragged, eventData);
                },
                dragHandler: dragController.Drag,
                endDragHandler: dragController.End);
        }

        // 아이템 정보는 클릭(또는 드래그 시작)으로 선택했을 때만 표시한다(기획 39번 §3.7).
        private void Select(string instanceId)
        {
            selectedInstanceId = instanceId;
            message = null;
            ApplySelection();
        }

        private void ClearSelection()
        {
            selectedInstanceId = null;
            message = null;
            ApplySelection();
        }

        // 선택만 바뀔 때는 전체를 다시 그리지 않고 테두리와 정보 줄만 갱신한다 - 드래그 시작 직전에 뷰를
        // 다시 배치하면 EventSystem이 붙잡은 드래그 대상과 표시가 어긋날 수 있다.
        private void ApplySelection()
        {
            foreach (var view in gridView.ItemViews) view.SetSelected(view.gameObject.activeSelf && view.Item.InstanceId == selectedInstanceId);
            if (stagingView != null)
            {
                foreach (var view in stagingView.ItemViews) view.SetSelected(view.gameObject.activeSelf && view.Item.InstanceId == selectedInstanceId);
            }
            UpdateInfoLabel();
        }

        private void UpdateInfoLabel()
        {
            if (message != null)
            {
                elements.InfoLabel.text = message;
                return;
            }

            elements.InfoLabel.text = selectedInstanceId != null && TryFind(selectedInstanceId, out var item)
                ? item.Definition.DisplayName
                : string.Empty;
        }

        private bool Contains(string instanceId) => TryFind(instanceId, out _);

        private bool TryFind(string instanceId, out InventoryItemInstance found)
        {
            foreach (var item in reader.Items)
            {
                if (item.InstanceId == instanceId)
                {
                    found = item;
                    return true;
                }
            }

            foreach (var item in arrangement.StagedItems)
            {
                if (item.InstanceId == instanceId)
                {
                    found = item;
                    return true;
                }
            }

            found = default;
            return false;
        }
    }
}
