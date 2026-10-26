using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Core
{
    /// <summary>
    /// 인벤토리 아이템 드래그 한 건의 상태·고스트·Q 회전·드롭 적용을 전담한다(Docs/설계/40번 §5.2~5.3).
    /// FormationDragCoordinator는 1칸=유닛 1개(슬롯 인덱스) 전제라 다칸 점유·회전을 다루지 못해 재사용하지 않고,
    /// "뷰는 이벤트만 위임, 판단은 상위" 패턴만 따른다.
    ///
    /// 기준점: 드래그 시작 시 포인터가 눌린 아이템 내부 칸(잡은 칸)의 중심이 포인터에 오도록 고스트를 그리고,
    /// 목표 좌상단 칸 = 포인터 칸 − 잡은 칸. Q 회전(시계방향 90°) 시 잡은 칸 좌표도 같이 회전해 잡은 칸이
    /// 포인터 아래를 벗어나지 않는다(기획 39번 §3.4).
    ///
    /// Q 입력은 드래그 중에만 InputManager에 구독하고 드래그가 끝나면 핸들을 해제한다(설계 99번 §5.2) - 상시 구독하면
    /// 드래그가 아닐 때의 Q가 다른 기능과 겹친다. 회전을 허용하지 않는 팝업이나 InputManager가 없으면 구독하지 않는다.
    /// 임시 보관 뷰가 없으면 그리드 밖 드롭은 전부 원위치다(설계 42번 §4.3).
    ///
    /// 드롭 섹션은 드롭 시점에 화면이 보여 주는 섹션이다(currentSectionId) - 드래그 중 스테퍼 호버로 마차를 바꾸면 다른 마차에
    /// 놓게 된다(기획 63번 §3.4). 판정·적용은 한 번의 재배치라 실패하면 원래 마차·원래 칸에 그대로 남는다.
    /// </summary>
    internal sealed class InventoryItemDragController : IDisposable
    {
        private readonly IInventoryReader reader;
        private readonly IInventoryArrangement arrangement;
        private readonly InventoryGridView gridView;
        private readonly InventoryStagingView stagingView; // 임시 보관이 꺼진 팝업은 null
        private readonly RectTransform dragLayer;
        private readonly InventoryItemView ghost;
        private readonly bool allowsRotation;
        private readonly IInputManager input; // 없으면 Q 회전만 빠진다
        private IDisposable rotateSubscription;
        private readonly Func<string> currentSectionId;
        private readonly Action onFinished;
        // 쓰레기통(설계 102번 §4.4) - 포인터가 이 영역 안이면 다른 대상보다 먼저 버리기 드롭으로 본다.
        private readonly RectTransform trashArea;
        private readonly Action<string> onDiscardDrop;

        private InventoryItemView sourceView;
        private InventoryItemInstance dragged;
        private GridPosition grab;
        private int quarterTurns;
        private Vector2 pointerPosition;
        private Camera eventCamera;

        public bool IsDragging { get; private set; }
        /// <summary>드래그를 시작한 뷰(드래그 중이 아니면 null) - 그리드를 다시 그릴 때 재사용 풀에서 빼야 한다(설계 64번 §6.2).</summary>
        public InventoryItemView SourceView => IsDragging ? sourceView : null;

        /// <param name="currentSectionId">드롭할 섹션 - 편집 본문이 지금 보여 주는 섹션.</param>
        /// <param name="onFinished">드롭 처리가 끝난 뒤(성공·실패 모두) - 섹션을 바꾼 채 실패하면 저장소 변경 이벤트가 없어 화면을 따로 정리해야 한다.</param>
        public InventoryItemDragController(IInventoryReader reader, IInventoryArrangement arrangement, InventoryGridView gridView, InventoryStagingView stagingView, RectTransform dragLayer, InventoryItemView itemTemplate, bool allowsRotation, Func<string> currentSectionId, Action onFinished, RectTransform trashArea, Action<string> onDiscardDrop, IInputManager input)
        {
            this.currentSectionId = currentSectionId;
            this.onFinished = onFinished;
            this.trashArea = trashArea;
            this.onDiscardDrop = onDiscardDrop;
            this.allowsRotation = allowsRotation;
            this.input = input;
            this.reader = reader;
            this.arrangement = arrangement;
            this.gridView = gridView;
            this.stagingView = stagingView;
            this.dragLayer = dragLayer;

            ghost = UnityEngine.Object.Instantiate(itemTemplate, dragLayer);
            ghost.SetRaycastTarget(false);
            ghost.gameObject.SetActive(false);
        }

        public void Begin(InventoryItemView view, PointerEventData eventData)
        {
            Cancel();

            sourceView = view;
            dragged = view.Item;
            grab = view.GetLocalCell(eventData);
            quarterTurns = dragged.QuarterTurns;
            pointerPosition = eventData.position;
            eventCamera = eventData.pressEventCamera;
            IsDragging = true;

            view.SetDimmed(true);
            ghost.Bind(dragged, InventoryItemColorPalette.ColorFor(dragged.Definition.Id));
            ghost.gameObject.SetActive(true);
            ghost.transform.SetAsLastSibling();
            if (allowsRotation && input != null) rotateSubscription = input.Subscribe(GameInputActionIds.RotateHeldItem, Rotate);
            Refresh();
        }

        public void Drag(PointerEventData eventData)
        {
            if (!IsDragging) return;

            pointerPosition = eventData.position;
            eventCamera = eventData.pressEventCamera;
            Refresh();
        }

        public void End(PointerEventData eventData)
        {
            if (!IsDragging) return;

            pointerPosition = eventData.position;
            eventCamera = eventData.pressEventCamera;
            var itemId = dragged.InstanceId;
            var turns = quarterTurns;
            var target = TryGetTarget(out var topLeft);
            Cancel();

            // 판정은 Cancel 뒤에 적용한다 - 적용이 OnChanged로 뷰를 다시 그리므로 드래그 표시부터 정리해 둔다.
            // 불가 위치·그 밖의 바깥 드롭은 아무것도 하지 않는다 = 원위치·원래 회전 유지(기획 39번 §3.2, §4.1).
            if (target == DropTarget.Grid)
            {
                var result = InventoryDropResolver.ResolveGridDrop(reader, arrangement, FindCurrent(itemId), currentSectionId(), topLeft, turns);
                if (result.Kind != InventoryDropKind.Invalid) arrangement.TryApplyPlacements(result.Placements);
            }
            else if (target == DropTarget.Trash)
            {
                // 재배치하지 않는다 - 아이템은 원위치에 둔 채 경고를 띄우고, 확인하면 그때 버린다(기획 95).
                onDiscardDrop?.Invoke(itemId);
            }
            else if (target == DropTarget.Staging)
            {
                arrangement.TryApplyPlacements(new[] { ItemPlacement.ToStaging(itemId, turns) });
            }

            onFinished?.Invoke();
        }

        /// <summary>드래그 중 보이는 섹션이 바뀌었을 때 미리보기를 새 섹션 기준으로 다시 판정한다.</summary>
        public void RefreshPreview()
        {
            if (IsDragging) Refresh();
        }

        public void Cancel()
        {
            rotateSubscription?.Dispose();
            rotateSubscription = null;
            if (ghost != null) ghost.gameObject.SetActive(false);
            gridView.ClearPreview();
            if (sourceView != null) sourceView.SetDimmed(false);
            sourceView = null;
            IsDragging = false;
        }

        public void Dispose()
        {
            Cancel();
        }

        private void Rotate()
        {
            // 씬 언로드 등으로 고스트가 파괴된 뒤 콜백이 오면 무시한다.
            if (!IsDragging || ghost == null) return;

            grab = InventoryRotation.RotateLocalCellClockwise(grab, CurrentHeight);
            quarterTurns = InventoryRotation.RotateClockwise(quarterTurns);
            Refresh();
        }

        private int CurrentWidth => quarterTurns % 2 == 1 ? dragged.Definition.FootprintHeight : dragged.Definition.FootprintWidth;
        private int CurrentHeight => quarterTurns % 2 == 1 ? dragged.Definition.FootprintWidth : dragged.Definition.FootprintHeight;

        private void Refresh()
        {
            UpdateGhost();

            if (TryGetTarget(out var topLeft) == DropTarget.Grid)
            {
                var result = InventoryDropResolver.ResolveGridDrop(reader, arrangement, dragged, currentSectionId(), topLeft, quarterTurns);
                gridView.ShowPreview(topLeft, CurrentWidth, CurrentHeight, result.Kind);
            }
            else
            {
                gridView.ClearPreview();
            }
        }

        // 고스트의 pivot을 잡은 칸의 중심에 두고 포인터 위치에 놓는다 - 회전해도 pivot만 다시 계산하면 된다.
        private void UpdateGhost()
        {
            var width = CurrentWidth;
            var height = CurrentHeight;
            var cellSize = gridView.CellSize;
            var rect = ghost.RectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width * cellSize, height * cellSize);
            rect.pivot = new Vector2((grab.X + 0.5f) / width, 1f - (grab.Y + 0.5f) / height);

            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(dragLayer, pointerPosition, eventCamera, out var world))
            {
                rect.position = world;
            }
        }

        private enum DropTarget
        {
            None,
            Grid,
            Staging,
            Trash,
        }

        private DropTarget TryGetTarget(out GridPosition topLeft)
        {
            topLeft = default;
            if (trashArea != null && RectTransformUtility.RectangleContainsScreenPoint(trashArea, pointerPosition, eventCamera)) return DropTarget.Trash;
            if (gridView.ContainsScreenPoint(pointerPosition, eventCamera) && gridView.TryGetCellAt(pointerPosition, eventCamera, out var cell))
            {
                topLeft = new GridPosition(cell.X - grab.X, cell.Y - grab.Y);
                return DropTarget.Grid;
            }

            return stagingView != null && stagingView.ContainsScreenPoint(pointerPosition, eventCamera) ? DropTarget.Staging : DropTarget.None;
        }

        // 드롭 시점의 저장소 상태로 다시 조회한다 - 드래그 중 다른 경로로 상태가 바뀌었어도 최신 값으로 판정한다.
        private InventoryItemInstance FindCurrent(string instanceId)
        {
            foreach (var staged in arrangement.StagedItems)
            {
                if (staged.InstanceId == instanceId) return staged;
            }

            foreach (var item in reader.Items)
            {
                if (item.InstanceId == instanceId) return item;
            }

            return dragged;
        }
    }
}
