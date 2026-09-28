using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 목록형 구매 화면의 후보 목록 렌더링(줄 재사용·줄 높이·빈 목록 문구, Docs/설계/56번 §7.1). 용병단 접촉·마구간 패널이 후보
    /// 타입만 다를 뿐 같은 목록 처리를 하므로 여기로 모았다 - 후보를 줄에 채우는 방법만 호출자가 넘긴다.
    /// </summary>
    public sealed class RosterShopCandidateList
    {
        private const int VisibleRows = 6; // 목록 영역에 한 번에 보이는 줄 수 - 줄 높이를 영역 높이 비율로 정한다(잠정)

        private readonly RosterShopElements elements;
        private readonly List<RosterShopCandidateRowView> rowViews = new();

        public RosterShopCandidateList(RosterShopElements elements)
        {
            this.elements = elements;
            elements.CandidateRowTemplate.gameObject.SetActive(false);
        }

        public void Render(int count, Action<RosterShopCandidateRowView, int> bindRow, string emptyText)
        {
            var viewportHeight = Mathf.Max(elements.CandidateViewport.rect.height, 1f);
            var rowHeight = viewportHeight / VisibleRows;

            for (var i = 0; i < count; i++)
            {
                if (i >= rowViews.Count) rowViews.Add(UnityEngine.Object.Instantiate(elements.CandidateRowTemplate, elements.CandidateContent));

                var row = rowViews[i];
                row.gameObject.SetActive(true);
                bindRow(row, i);
                var rect = row.RectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -i * rowHeight);
                rect.sizeDelta = new Vector2(0f, rowHeight);
            }

            for (var i = count; i < rowViews.Count; i++) rowViews[i].gameObject.SetActive(false);

            elements.CandidateContent.sizeDelta = new Vector2(elements.CandidateContent.sizeDelta.x, count * rowHeight);
            elements.EmptyCandidateLabel.gameObject.SetActive(count == 0);
            elements.EmptyCandidateLabel.text = emptyText;
        }

        public void SetSelected(int selectedIndex)
        {
            for (var i = 0; i < rowViews.Count; i++) rowViews[i].SetSelected(i == selectedIndex);
        }
    }
}
