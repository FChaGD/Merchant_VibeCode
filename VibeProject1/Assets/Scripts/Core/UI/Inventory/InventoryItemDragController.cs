using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

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
    /// Q 입력은 드래그 중에만 켜지는 InputAction으로 받는다 - 프로젝트가 Input System 전용 설정이라 구식
    /// Input.GetKey는 쓸 수 없고, Update 폴링 대신 performed 콜백으로 처리한다. IInputManager는 아직 뼈대뿐이라
    /// 경유하지 않는다 - 입력 매니저가 설계되면 이 바인딩을 옮긴다(설계 40번 §10).
    /// </summary>
    internal sealed class InventoryItemDragController : IDisposable
    {
        private const string RotateBinding = "<Keyboard>/q";

        private readonly IInventoryReader reader;
        private readonly IInventoryArrangement arrangement;
        private readonly InventoryGridView gridView;
        private readonly InventoryStagingView stagingView;
        private readonly RectTransform dragLayer;
        private readonly InventoryItemView ghost;
        private readonly InputAction rotateAction;

        private InventoryItemView sourceView;
        private InventoryItemInstance dragged;
        private GridPosition grab;
        private int quarterTurns;
        private Vector2 pointerPosition;
        private Camera eventCamera;

        public bool IsDragging { get; private set; }

        public InventoryItemDragController(IInventoryReader reader, IInventoryArrangement arrangement, InventoryGridView gridView, InventoryStagingView stagingView, RectTransform dragLayer, InventoryItemView itemTemplate)
        {
            this.reader = reader;
            this.arrangement = arrangement;
            this.gridView = gridView;
            this.stagingView = stagingView;
            this.dragLayer = dragLayer;

            ghost = UnityEngine.Object.Instantiate(itemTemplate, dragLayer);
            ghost.SetRaycastTarget(false);
            ghost.gameObject.SetActive(false);

            rotateAction = new InputAction("RotateHeldInventoryItem", InputActionType.Button, RotateBinding);
            rotateAction.performed += _ => Rotate();
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
            rotateAction.Enable();
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
                var result = InventoryDropResolver.ResolveGridDrop(reader, arrangement, FindCurrent(itemId), topLeft, turns);
                if (result.Kind != InventoryDropKind.Invalid) arrangement.TryApplyPlacements(result.Placements);
            }
            else if (target == DropTarget.Staging)
            {
                arrangement.TryApplyPlacements(new[] { ItemPlacement.ToStaging(itemId, turns) });
            }
        }

        public void Cancel()
        {
            rotateAction.Disable();
            if (ghost != null) ghost.gameObject.SetActive(false);
            gridView.ClearPreview();
            if (sourceView != null) sourceView.SetDimmed(false);
            sourceView = null;
            IsDragging = false;
        }

        public void Dispose()
        {
            Cancel();
            rotateAction.Dispose();
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
                var result = InventoryDropResolver.ResolveGridDrop(reader, arrangement, dragged, topLeft, quarterTurns);
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
        }

        private DropTarget TryGetTarget(out GridPosition topLeft)
        {
            topLeft = default;
            if (gridView.ContainsScreenPoint(pointerPosition, eventCamera) && gridView.TryGetCellAt(pointerPosition, eventCamera, out var cell))
            {
                topLeft = new GridPosition(cell.X - grab.X, cell.Y - grab.Y);
                return DropTarget.Grid;
            }

            return stagingView.ContainsScreenPoint(pointerPosition, eventCamera) ? DropTarget.Staging : DropTarget.None;
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
