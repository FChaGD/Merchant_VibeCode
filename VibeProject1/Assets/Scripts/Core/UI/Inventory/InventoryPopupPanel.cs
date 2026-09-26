using System;

namespace Game.Core
{
    /// <summary>
    /// 인벤토리 팝업 1개의 조율(Docs/설계/40번 §5). 팝업 4종이 이 클래스의 인스턴스가 되는데, 같은 GameObject에
    /// 같은 타입 컴포넌트 4개를 GetComponent로 구분할 수 없어 MonoBehaviour가 아닌 plain C#으로 둔다
    /// (TownCategoryPanel과 같은 선택). Hub 로드마다 새로 만들어지므로 영속 상태(창 위치)는 외부
    /// PopupWindowPositionStore에 둔다. 저장소(Bootstrap 상주)의 OnChanged를 구독하므로 교체 시 반드시 Dispose한다.
    /// </summary>
    public sealed class InventoryPopupPanel : IUIPanel, IPanelCloseGuard, IDisposable
    {
        private const string CloseBlockedMessage = "임시 보관 물품을 넣을 공간이 부족해 닫을 수 없습니다.";
        private const string SortFailedMessage = "공간이 부족해 정렬할 수 없습니다.";

        private readonly InventoryPopupElements elements;
        private readonly IInventoryReader reader;
        private readonly IInventoryArrangement arrangement;
        private readonly PopupWindowPositionStore windowPositions;
        private readonly InventoryGridView gridView;
        private readonly InventoryStagingView stagingView;
        private readonly InventoryItemDragController dragController;

        private bool isOpen;
        private string selectedInstanceId;
        private string message;

        public string PanelId { get; }

        public InventoryPopupPanel(string popupId, InventoryPopupElements elements, IInventoryReader reader, IInventoryArrangement arrangement, IUIManager uiManager, PopupWindowPositionStore windowPositions)
        {
            PanelId = popupId;
            this.elements = elements;
            this.reader = reader;
            this.arrangement = arrangement;
            this.windowPositions = windowPositions;

            elements.ItemTemplate.gameObject.SetActive(false);
            elements.CellTemplate.gameObject.SetActive(false);
            gridView = new InventoryGridView(elements.GridArea, elements.GridCells, elements.GridItems, elements.CellTemplate, elements.ItemTemplate);
            stagingView = new InventoryStagingView(elements.StagingArea, elements.StagingContent, elements.ItemTemplate);
            dragController = new InventoryItemDragController(reader, arrangement, gridView, stagingView, elements.DragLayer, elements.ItemTemplate);

            // 닫기 버튼도 상시 호출 버튼과 같은 토글 경로를 탄다 - 닫기 차단(IPanelCloseGuard)이 코디네이터 한 곳에서만 확인된다.
            elements.CloseButton.onClick.RemoveAllListeners();
            elements.CloseButton.onClick.AddListener(() => uiManager.ToggleInventoryPopup(PanelId));
            elements.SortButton.onClick.RemoveAllListeners();
            elements.SortButton.onClick.AddListener(Sort);
            elements.RootClick.Clicked += ClearSelection;
            elements.TitleBar.Moved += position => windowPositions.Set(PanelId, position);

            reader.OnChanged += HandleInventoryChanged;
            elements.Root.gameObject.SetActive(false);
        }

        public void Open()
        {
            isOpen = true;
            message = null;
            elements.Root.gameObject.SetActive(true);
            elements.Root.SetAsLastSibling();
            if (windowPositions.TryGet(PanelId, out var position)) elements.TitleBar.SetPosition(position);
            Refresh();
        }

        // Close()는 표시/숨김만 한다. 닫기 버튼 등 패널 내부에서 닫을 때는 이 메서드를 직접 부르지 않고
        // UIManager.ToggleInventoryPopup(PanelId)로 코디네이터에 위임한다(닫기 차단 확인 경로 유지).
        public void Close()
        {
            dragController.Cancel();
            isOpen = false;
            selectedInstanceId = null;
            message = null;
            elements.Root.gameObject.SetActive(false);
        }

        /// <summary>
        /// 임시 보관 아이템을 그리드 빈칸에 자동 배치한다. 전부 넣지 못하면 안내 문구를 띄우고 닫기를 막는다
        /// (Docs/기획/39번 §3.5) - 임시 보관이 추가 수납 공간이 되지 않고, 버리기 없음 원칙도 지켜진다.
        /// </summary>
        public bool TryPrepareClose()
        {
            dragController.Cancel();
            if (arrangement.StagedItems.Count == 0) return true;

            if (InventoryAutoSorter.TryBuildStagedFlush(reader.GridWidth, reader.GridHeight, reader.Items, arrangement.StagedItems, out var placements)
                && arrangement.TryApplyPlacements(placements))
            {
                return true;
            }

            ShowMessage(CloseBlockedMessage);
            return false;
        }

        public void Dispose()
        {
            reader.OnChanged -= HandleInventoryChanged;
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
            if (isOpen && !dragController.IsDragging) Refresh();
        }

        private void Refresh()
        {
            if (selectedInstanceId != null && !Contains(selectedInstanceId)) selectedInstanceId = null;

            gridView.Render(reader, ConfigureItemView);
            stagingView.Render(arrangement.StagedItems, gridView.CellSize, ConfigureItemView);
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
            foreach (var view in stagingView.ItemViews) view.SetSelected(view.gameObject.activeSelf && view.Item.InstanceId == selectedInstanceId);
            UpdateInfoLabel();
        }

        private void ShowMessage(string text)
        {
            message = text;
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
