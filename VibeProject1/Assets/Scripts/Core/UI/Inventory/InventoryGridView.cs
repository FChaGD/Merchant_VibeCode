using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 인벤토리 그리드의 칸/아이템 배치와 드래그 미리보기 색칠(Docs/설계/40번 §5.1). 칸 크기는 영역 크기 ÷
    /// 칸 수(가로·세로 중 작은 값)로 매번 계산해 마차 수가 바뀌어도 영역에 맞춘다. 칸/아이템 오브젝트는
    /// 템플릿에서 한 번 복제한 뒤 재사용한다(매번 Destroy+Instantiate 하지 않음). 드롭 대상 판정은 칸마다
    /// IDropHandler를 두지 않고 영역 사각형 한 번으로 한다.
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

        private int gridWidth;
        private int gridHeight;

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

        public void Render(IInventoryReader reader, Action<InventoryItemView> configure)
        {
            gridWidth = reader.GridWidth;
            gridHeight = reader.GridHeight;
            var rect = area.rect;
            CellSize = gridWidth > 0 && gridHeight > 0 ? Mathf.Min(rect.width / gridWidth, rect.height / gridHeight) : 0f;

            RenderCells();

            var used = 0;
            foreach (var item in reader.Items)
            {
                // 축소(InventoryGrid.Resize)로 범위 밖에 걸친 아이템은 칸 위에 그릴 자리가 없다 - 표시에서 제외.
                if (item.Position.X + item.Width > gridWidth || item.Position.Y + item.Height > gridHeight) continue;

                var view = GetOrCreateItemView(used++);
                view.Bind(item, InventoryItemColorPalette.ColorFor(item.Definition.Id));
                PlaceTopLeft(view.RectTransform, item.Position.X * CellSize, item.Position.Y * CellSize, item.Width * CellSize, item.Height * CellSize);
                configure?.Invoke(view);
            }

            for (var i = used; i < itemViews.Count; i++) itemViews[i].gameObject.SetActive(false);
        }

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
            var color = kind switch
            {
                InventoryDropKind.Move => MovePreviewColor,
                InventoryDropKind.Swap => SwapPreviewColor,
                _ => InvalidPreviewColor,
            };

            for (var x = Mathf.Max(topLeft.X, 0); x < Mathf.Min(topLeft.X + width, gridWidth); x++)
            {
                for (var y = Mathf.Max(topLeft.Y, 0); y < Mathf.Min(topLeft.Y + height, gridHeight); y++)
                {
                    cells[y * gridWidth + x].color = color;
                }
            }
        }

        public void ClearPreview()
        {
            for (var i = 0; i < gridWidth * gridHeight && i < cells.Count; i++) cells[i].color = CellColor;
        }

        private void RenderCells()
        {
            var count = gridWidth * gridHeight;
            for (var i = 0; i < count; i++)
            {
                if (i >= cells.Count)
                {
                    var cell = UnityEngine.Object.Instantiate(cellTemplate, cellsRoot);
                    cell.raycastTarget = false;
                    cells.Add(cell);
                }

                var x = i % gridWidth;
                var y = i / gridWidth;
                cells[i].gameObject.SetActive(true);
                cells[i].color = CellColor;
                PlaceTopLeft(cells[i].rectTransform, x * CellSize + CellGap * 0.5f, y * CellSize + CellGap * 0.5f, CellSize - CellGap, CellSize - CellGap);
            }

            for (var i = count; i < cells.Count; i++) cells[i].gameObject.SetActive(false);
        }

        private InventoryItemView GetOrCreateItemView(int index)
        {
            if (index >= itemViews.Count)
            {
                itemViews.Add(UnityEngine.Object.Instantiate(itemTemplate, itemsRoot));
            }

            var view = itemViews[index];
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
