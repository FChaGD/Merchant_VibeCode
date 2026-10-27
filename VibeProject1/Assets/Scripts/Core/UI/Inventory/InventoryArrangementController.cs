using System;

namespace Game.Core
{
    /// <summary>
    /// 인벤토리 그리드 편집(렌더링·드래그 이동·교환·회전·임시 보관·자동 정렬·선택·안내 문구)을 담당한다(Docs/설계/50번 §6.4).
    /// 버리기(선택 상태·경고 대화상자)는 협력 객체 InventoryDiscardController에 맡기고, 선택 상태인지에 따라 클릭·드래그·정렬만 가른다(설계 102번 §4.3).
    /// 원래 InventoryPopupPanel 안에 있었지만 무역품 구매 화면의 고정 패널도 같은 편집이 필요해 떠 있는 창 기능(창 드래그·
    /// 닫기 버튼·창 위치)과 분리했다. 저장소(Bootstrap 상주)의 OnChanged를 구독하므로 소유자가 교체될 때 반드시 Dispose한다.
    /// 표시 여부는 소유자가 Show/Hide로 알려 준다 - 숨겨진 동안에는 저장소가 바뀌어도 다시 그리지 않는다.
    ///
    /// 한 번에 섹션(마차) 1개만 그린다(Docs/기획/63번 §3.3, 설계 64번 §6.1). 보는 섹션은 이 컨트롤러가 기억하고, 스테퍼 클릭·
    /// 드래그 중 스테퍼 호버·외부 요청(구매 후 그 마차로 전환)으로 바뀐다. 섹션이 1개뿐인 인벤토리는 스테퍼 없이 늘 첫 섹션이다.
    /// </summary>
    internal sealed class InventoryArrangementController : IDisposable
    {
        private const string SortFailedMessage = "공간이 부족해 정렬할 수 없습니다.";
        private const string NoSectionLabel = PlaceholderText.Missing;

        private readonly InventoryArrangementElements elements;
        private readonly IInventoryReader reader;
        private readonly IInventoryArrangement arrangement;
        private readonly InventoryGridView gridView;
        private readonly InventoryStagingView stagingView; // 임시 보관이 꺼진 화면은 null
        private readonly InventoryItemDragController dragController;
        private readonly InventoryDiscardController discardController;
        private readonly bool hasSections;

        private bool isShown;
        private string selectedInstanceId;
        private string message;
        private string currentSectionId;

        public InventoryArrangementController(InventoryArrangementElements elements, IInputManager input, IGoldItemInfo goldInfo, IInventoryReader reader, IInventoryArrangement arrangement, bool allowsRotation, bool hasStaging, bool hasSections,
            IQuestCargoWarnings questWarnings = null)
        {
            this.elements = elements;
            this.reader = reader;
            this.arrangement = arrangement;
            this.hasSections = hasSections;

            elements.ItemTemplate.gameObject.SetActive(false);
            elements.CellTemplate.gameObject.SetActive(false);
            gridView = new InventoryGridView(elements.GridArea, elements.GridCells, elements.GridItems, elements.CellTemplate, elements.ItemTemplate);
            stagingView = hasStaging ? new InventoryStagingView(elements.StagingArea, elements.StagingContent, elements.ItemTemplate) : null;
            // 버리기(설계 102번 §4.3) - 쓰레기통 버튼이 드롭 대상 영역을 겸한다.
            discardController = new InventoryDiscardController(elements, arrangement, goldInfo, TryFind, HandleDiscardStateChanged, questWarnings);
            dragController = new InventoryItemDragController(reader, arrangement, gridView, stagingView, elements.DragLayer, elements.ItemTemplate, allowsRotation, () => CurrentSection?.Id, HandleDragFinished, (UnityEngine.RectTransform)elements.DiscardButton.transform, discardController.RequestDropDiscard, input);

            elements.SortButton.onClick.RemoveAllListeners();
            elements.SortButton.onClick.AddListener(Sort);
            elements.RootClick.Clicked += ClearSelection;
            reader.OnChanged += HandleInventoryChanged;

            if (hasSections)
            {
                elements.SectionPrevButton.onClick.RemoveAllListeners();
                elements.SectionPrevButton.onClick.AddListener(StepPrevious);
                elements.SectionNextButton.onClick.RemoveAllListeners();
                elements.SectionNextButton.onClick.AddListener(StepNext);
                // 드래그 중일 때만 호버로 넘긴다(기획 63번 §3.4) - 그냥 지나가는 포인터로는 바뀌지 않는다.
                elements.SectionPrevHover.CanRepeat = () => dragController.IsDragging;
                elements.SectionNextHover.CanRepeat = () => dragController.IsDragging;
                elements.SectionPrevHover.Fired += StepPrevious;
                elements.SectionNextHover.Fired += StepNext;
            }
        }

        /// <summary>지금 보이는 섹션(보유 마차가 없으면 null). 사라진 Id를 기억하고 있으면 첫 섹션으로 돌아간다.</summary>
        public InventorySection CurrentSection
        {
            get
            {
                if (reader.TryGetSection(currentSectionId, out var section)) return section;
                return reader.TryGetSection(null, out var first) ? first : null;
            }
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
            discardController.End();
            isShown = false;
            selectedInstanceId = null;
            message = null;
        }

        /// <summary>보는 섹션을 바꾼다 - 구매한 물품이 다른 마차에 들어갔을 때 그 마차를 보여 주는 데 쓴다(기획 63번 §3.5).</summary>
        public void ShowSection(string sectionId)
        {
            if (!reader.TryGetSection(sectionId, out _)) return;

            currentSectionId = sectionId;
            if (isShown) Refresh();
        }

        /// <summary>
        /// 임시 보관 아이템을 그리드 빈칸에 자동 배치한다 - 섹션이 여럿이면 지금 보는 섹션부터 순서대로(기획 63번 §3.5). 전부 넣지
        /// 못하면 아무것도 옮기지 않고 false - 호출자가 안내 문구를 띄우고 닫기(나가기)를 막는다(Docs/기획/39번 §3.5, 48번 §4.5).
        /// </summary>
        public bool TryFlushStaging()
        {
            dragController.Cancel();
            if (arrangement.StagedItems.Count == 0) return true;

            var order = InventoryAutoSorter.OrderFrom(reader.Sections, CurrentSection?.Id);
            return InventoryAutoSorter.TryBuildStagedFlush(order, reader.Items, arrangement.StagedItems, out var placements)
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
            if (hasSections)
            {
                elements.SectionPrevHover.Fired -= StepPrevious;
                elements.SectionNextHover.Fired -= StepNext;
                elements.SectionPrevHover.CanRepeat = null;
                elements.SectionNextHover.CanRepeat = null;
            }
            dragController.Dispose();
            discardController.Dispose();
        }

        // 현재 섹션 아이템 + 임시 보관만 현재 섹션 안에서 정렬한다 - 다른 마차는 그대로(기획 63번 §3.5).
        private void Sort()
        {
            if (discardController.IsSelecting) return; // 선택 버리기 중에는 정렬을 막는다(기획 95)
            dragController.Cancel();
            var section = CurrentSection;
            if (section != null
                && InventoryAutoSorter.TryBuildSortedLayout(section, reader.Items, arrangement.StagedItems, out var placements)
                && (placements.Count == 0 || arrangement.TryApplyPlacements(placements)))
            {
                message = null;
                return; // OnChanged → Refresh
            }

            ShowMessage(SortFailedMessage);
        }

        private void StepPrevious() => Step(-1);

        private void StepNext() => Step(1);

        // 끝에서 처음으로 순환한다(기획 63번 §3.3). 드래그 중이면 원본·고스트를 유지한 채 그리드만 새 섹션으로 다시 그린다.
        private void Step(int delta)
        {
            var sections = reader.Sections;
            if (sections.Count == 0) return;

            var index = reader.IndexOfSection(CurrentSection?.Id);
            index = ((index < 0 ? 0 : index) + delta % sections.Count + sections.Count) % sections.Count;
            currentSectionId = sections[index].Id;

            if (!isShown) return;
            Refresh();
            dragController.RefreshPreview();
        }

        private void HandleInventoryChanged()
        {
            discardController.Prune();
            if (isShown && !dragController.IsDragging) Refresh();
        }

        // 선택 버리기 상태·선택이 바뀌면 표시(빨간 표시·정렬 버튼·정보 줄)만 갱신한다 - 뷰를 다시 배치하지 않는다.
        private void HandleDiscardStateChanged()
        {
            if (!isShown) return;
            ApplySelection();
        }

        private void HandleDragFinished()
        {
            if (isShown) Refresh();
        }

        private void Refresh()
        {
            if (selectedInstanceId != null && !Contains(selectedInstanceId)) selectedInstanceId = null;

            var section = CurrentSection;
            currentSectionId = section?.Id;
            var pinned = dragController.SourceView;
            gridView.Render(section, reader.Items, ConfigureItemView, pinned != null && gridView.Owns(pinned) ? pinned : null);
            // 드래그 중에는 임시 보관 목록을 다시 그리지 않는다 - 원본이 임시 보관 목록의 뷰일 수 있다.
            // 보유 마차가 없으면 칸 크기가 0이라 목록 높이에 맞춰 그린다.
            if (!dragController.IsDragging) stagingView?.Render(arrangement.StagedItems, gridView.CellSize > 0f ? gridView.CellSize : float.MaxValue, ConfigureItemView);
            elements.SortButton.interactable = !discardController.IsSelecting;
            UpdateSectionLabel();
            UpdateInfoLabel();
        }

        private void UpdateSectionLabel()
        {
            if (!hasSections) return;

            var sections = reader.Sections;
            var section = CurrentSection;
            elements.SectionLabel.text = section == null
                ? NoSectionLabel
                : $"{section.DisplayName} ({reader.IndexOfSection(section.Id) + 1}/{sections.Count})";
            var canStep = sections.Count > 1;
            elements.SectionPrevButton.interactable = canStep;
            elements.SectionNextButton.interactable = canStep;
        }

        private void ConfigureItemView(InventoryItemView view)
        {
            view.SetSelected(view.Item.InstanceId == selectedInstanceId);
            view.SetDiscardMarked(discardController.IsMarked(view.Item.InstanceId));
            view.SetHandlers(
                clickHandler: HandleItemClicked,
                beginDragHandler: (dragged, eventData) =>
                {
                    // 선택 버리기 중에는 드래그를 시작하지 않는다 - Q 회전 구독도 생기지 않는다(기획 95).
                    if (discardController.IsSelecting) return;
                    discardController.ClearFailureMessage();
                    Select(dragged.Item.InstanceId);
                    dragController.Begin(dragged, eventData);
                },
                dragHandler: dragController.Drag,
                endDragHandler: dragController.End);
        }

        // 선택 버리기 중의 클릭은 정보 선택이 아니라 버릴 아이템 토글이다(기획 95).
        private void HandleItemClicked(InventoryItemView clicked)
        {
            if (discardController.IsSelecting)
            {
                discardController.HandleItemClicked(clicked.Item.InstanceId);
                return;
            }
            discardController.ClearFailureMessage();
            Select(clicked.Item.InstanceId);
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
            foreach (var view in gridView.ItemViews) ApplyMarks(view);
            if (stagingView != null)
            {
                foreach (var view in stagingView.ItemViews) ApplyMarks(view);
            }
            elements.SortButton.interactable = !discardController.IsSelecting;
            UpdateInfoLabel();
        }

        private void ApplyMarks(InventoryItemView view)
        {
            var active = view.gameObject.activeSelf;
            view.SetSelected(active && view.Item.InstanceId == selectedInstanceId);
            view.SetDiscardMarked(active && discardController.IsMarked(view.Item.InstanceId));
        }

        private void UpdateInfoLabel()
        {
            var discardText = discardController.InfoText;
            if (discardText != null)
            {
                elements.InfoLabel.text = discardText;
                return;
            }

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
