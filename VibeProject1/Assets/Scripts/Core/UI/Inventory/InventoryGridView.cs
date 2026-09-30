using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 인벤토리 그리드 섹션 1개의 칸/아이템 배치와 드래그 미리보기 색칠(Docs/설계/40번 §5.1, 64번 §6). 칸 크기는 영역 크기 ÷
    /// 칸 수(가로·세로 중 작은 값)로 매번 계산해 섹션(마차)마다 모양이 달라도 영역에 맞춘다. 칸/아이템 오브젝트는
    /// 템플릿에서 한 번 복제한 뒤 재사용한다(매번 Destroy+Instantiate 하지 않음). 드롭 대상 판정은 칸마다
    /// IDropHandler를 두지 않고 영역 사각형 한 번으로 한다. 막힌 칸은 칸을 그리지 않는다(기획 63번 §3.2).
    ///
    /// 드래그 원본 뷰(pinned)는 재사용 풀에서 제외한다(설계 64번 §6.2) - 드래그 중 섹션을 바꿔 다시 그릴 때 EventSystem이 붙잡은
    /// 원본 오브젝트를 다른 아이템에 다시 묶거나 비활성화하면 드래그 종료 이벤트가 오지 않는다. 원본은 활성 상태로 두고
    /// 다른 섹션을 보는 동안에는 보이지만 않게 한다.
    /// </summary>
    internal class InventoryGridView
    {
        private static readonly Color CellColor = new(0.85f, 0.85f, 0.85f, 1f);
        private static readonly Color MovePreviewColor = new(0.55f, 0.85f, 0.55f, 1f);
        private static readonly Color SwapPreviewColor = new(0.95f, 0.85f, 0.4f, 1f);
        private static readonly Color InvalidPreviewColor = new(0.9f, 0.45f, 0.45f, 1f);
        private const float CellGap = 2f;

        private readonly RectTransform area;
        private readonly RectTransform cellsRoot;
        private readonly RectTransform itemsRoot;
        private readonly Image cellTemplate;
        private readonly InventoryItemView itemTemplate;

        private readonly List<Image> cells = new();
        private readonly List<InventoryItemView> itemViews = new();

        private InventoryShape shape;

        public float CellSize { get; private set; }
        public IReadOnlyList<InventoryItemView> ItemViews => itemViews;

        public InventoryGridView(RectTransform area, RectTransform cellsRoot, RectTransform itemsRoot, Image cellTemplate, InventoryItemView itemTemplate)
        {
            this.area = area;
            this.cellsRoot = cellsRoot;
            this.itemsRoot = itemsRoot;
            this.cellTemplate = cellTemplate;
            this.itemTemplate = itemTemplate;
        }

        /// <param name="section">그릴 섹션. null이면(보유 마차 없음) 칸·아이템을 모두 숨긴다.</param>
        /// <param name="items">저장소의 전체 아이템 - 이 섹션 아이템만 골라 그린다.</param>
        /// <param name="pinned">드래그 원본 뷰(없으면 null).</param>
        public void Render(InventorySection section, IEnumerable<InventoryItemInstance> items, Action<InventoryItemView> configure, InventoryItemView pinned = null)
        {
            shape = section?.Shape;
            var width = shape?.Width ?? 0;
            var height = shape?.Height ?? 0;
            var rect = area.rect;
            CellSize = width > 0 && height > 0 ? Mathf.Min(rect.width / width, rect.height / height) : 0f;

            RenderCells();

            var cursor = 0;
            if (section != null)
            {
                foreach (var item in items)
                {
                    if (item.SectionId != section.Id) continue;
                    if (pinned != null && item.InstanceId == pinned.Item.InstanceId) continue;

                    var view = NextFreeView(ref cursor, pinned);
                    view.Bind(item, InventoryItemColorPalette.ColorFor(item.Definition.Id));
                    view.SetVisible(true);
                    Place(view, item);
                    configure?.Invoke(view);
                }
            }

            for (var i = cursor; i < itemViews.Count; i++)
            {
                if (itemViews[i] != pinned) itemViews[i].gameObject.SetActive(false);
            }

            // 원본 뷰는 자기 섹션을 볼 때만 보인다. 섹션마다 칸 크기가 달라 보일 때마다 자리를 다시 잡는다.
            if (pinned != null && Owns(pinned))
            {
                var visible = section != null && pinned.Item.SectionId == section.Id;
                pinned.SetVisible(visible);
                if (visible) Place(pinned, pinned.Item);
            }
        }

        public bool Owns(InventoryItemView view) => itemViews.Contains(view);

        public bool ContainsScreenPoint(Vector2 screenPoint, Camera eventCamera)
            => RectTransformUtility.RectangleContainsScreenPoint(area, screenPoint, eventCamera);

        /// <summary>포인터가 가리키는 그리드 칸(범위 밖이면 음수/초과 좌표가 그대로 나온다).</summary>
        public bool TryGetCellAt(Vector2 screenPoint, Camera eventCamera, out GridPosition cell)
        {
            cell = default;
            if (CellSize <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(itemsRoot, screenPoint, eventCamera, out var local)) return false;

            var rect = itemsRoot.rect;
            cell = new GridPosition(Mathf.FloorToInt((local.x - rect.xMin) / CellSize), Mathf.FloorToInt((rect.yMax - local.y) / CellSize));
            return true;
        }

        public void ShowPreview(GridPosition topLeft, int width, int height, InventoryDropKind kind)
        {
            ClearPreview();
            if (shape == null) return;

            var color = kind switch
            {
                InventoryDropKind.Move => MovePreviewColor,
                InventoryDropKind.Swap => SwapPreviewColor,
                _ => InvalidPreviewColor,
            };

            for (var x = Mathf.Max(topLeft.X, 0); x < Mathf.Min(topLeft.X + width, shape.Width); x++)
            {
                for (var y = Mathf.Max(topLeft.Y, 0); y < Mathf.Min(topLeft.Y + height, shape.Height); y++)
                {
                    cells[y * shape.Width + x].color = color;
                }
            }
        }

        public void ClearPreview()
        {
            var count = shape == null ? 0 : shape.Width * shape.Height;
            for (var i = 0; i < count && i < cells.Count; i++) cells[i].color = CellColor;
        }

        private void RenderCells()
        {
            var width = shape?.Width ?? 0;
            var count = shape == null ? 0 : shape.Width * shape.Height;
            for (var i = 0; i < count; i++)
            {
                if (i >= cells.Count)
                {
                    var cell = UnityEngine.Object.Instantiate(cellTemplate, cellsRoot);
                    cell.raycastTarget = false;
                    cells.Add(cell);
                }

                var x = i % width;
                var y = i / width;
                cells[i].gameObject.SetActive(shape.IsUsable(x, y));
                cells[i].color = CellColor;
                PlaceTopLeft(cells[i].rectTransform, x * CellSize + CellGap * 0.5f, y * CellSize + CellGap * 0.5f, CellSize - CellGap, CellSize - CellGap);
            }

            for (var i = count; i < cells.Count; i++) cells[i].gameObject.SetActive(false);
        }

        private void Place(InventoryItemView view, InventoryItemInstance item)
            => PlaceTopLeft(view.RectTransform, item.Position.X * CellSize, item.Position.Y * CellSize, item.Width * CellSize, item.Height * CellSize);

        private InventoryItemView NextFreeView(ref int cursor, InventoryItemView pinned)
        {
            while (cursor < itemViews.Count && itemViews[cursor] == pinned) cursor++;
            if (cursor >= itemViews.Count) itemViews.Add(UnityEngine.Object.Instantiate(itemTemplate, itemsRoot));

            var view = itemViews[cursor++];
            view.gameObject.SetActive(true);
            return view;
        }

        // 좌상단 기준 배치 - 그리드 좌표(y = 아래)를 UI 좌표(y = 위)로 옮긴다.
        internal static void PlaceTopLeft(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(left, -top);
        }
    }
}
