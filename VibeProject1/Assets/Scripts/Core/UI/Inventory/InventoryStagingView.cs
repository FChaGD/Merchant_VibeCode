using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 임시 보관 영역 목록(Docs/기획/39번 §3.5·§4.3). 칸 구분 없이 아이템을 세로로 나열하고, 각 아이템은 점유 칸
    /// 모양 그대로 그린다 - 목록에서 끌어올 때도 그리드와 같은 방식으로 "잡은 칸"을 계산하기 위함이다(설계 40번 §5.2).
    /// 닫을 때 반드시 비워지므로 개수 제한이 없고, 넘치면 스크롤한다.
    /// </summary>
    internal class InventoryStagingView
    {
        private const float Padding = 6f;
        private const float Gap = 6f;

        private readonly RectTransform area;
        private readonly RectTransform content;
        private readonly InventoryItemView itemTemplate;
        private readonly List<InventoryItemView> itemViews = new();

        public IReadOnlyList<InventoryItemView> ItemViews => itemViews;

        public InventoryStagingView(RectTransform area, RectTransform content, InventoryItemView itemTemplate)
        {
            this.area = area;
            this.content = content;
            this.itemTemplate = itemTemplate;
        }

        /// <param name="preferredCellSize">그리드 칸 크기 - 목록 폭에 안 들어가는 아이템만 줄여서 그린다.</param>
        public void Render(IReadOnlyList<InventoryItemInstance> stagedItems, float preferredCellSize, Action<InventoryItemView> configure)
        {
            var availableWidth = Mathf.Max(content.rect.width - Padding * 2f, 1f);
            var top = Padding;

            for (var i = 0; i < stagedItems.Count; i++)
            {
                var item = stagedItems[i];
                if (i >= itemViews.Count) itemViews.Add(UnityEngine.Object.Instantiate(itemTemplate, content));

                var view = itemViews[i];
                view.gameObject.SetActive(true);
                view.Bind(item, InventoryItemColorPalette.ColorFor(item.Definition.Id));

                var cellSize = Mathf.Min(preferredCellSize, availableWidth / item.Width);
                InventoryGridView.PlaceTopLeft(view.RectTransform, Padding, top, item.Width * cellSize, item.Height * cellSize);
                top += item.Height * cellSize + Gap;
                configure?.Invoke(view);
            }

            for (var i = stagedItems.Count; i < itemViews.Count; i++) itemViews[i].gameObject.SetActive(false);

            content.sizeDelta = new Vector2(content.sizeDelta.x, top + Padding);
        }

        public bool ContainsScreenPoint(Vector2 screenPoint, Camera eventCamera)
            => RectTransformUtility.RectangleContainsScreenPoint(area, screenPoint, eventCamera);
    }
}
