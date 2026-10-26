using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 고정 줄 수 목록 배치(Docs/Refactor/2026-10-26_공통.md D-4). 줄 높이 = 뷰포트 높이 ÷ 보이는 줄 수, 줄은 위에서부터 차례로 놓고
    /// 콘텐츠 높이를 줄 수에 맞춘다. 줄 오브젝트는 매번 파괴·생성하지 않고 부족한 만큼만 템플릿에서 복제해 재사용하고, 남는 줄은 숨긴다.
    /// 상인조합·마구간 보유·목록형 구매 후보·무역품 판매 목록이 같은 코드를 각자 갖고 있던 것을 모았다.
    /// </summary>
    public static class FixedRowListLayout
    {
        public static float RowHeight(float viewportHeight, int visibleRows) => Mathf.Max(viewportHeight, 1f) / visibleRows;

        public static void Render<TRow>(List<TRow> rows, int count, TRow template, RectTransform content, float rowHeight, Action<TRow, int> bind) where TRow : Component
        {
            for (var i = 0; i < count; i++)
            {
                if (i >= rows.Count) rows.Add(UnityEngine.Object.Instantiate(template, content));

                var row = rows[i];
                row.gameObject.SetActive(true);
                bind(row, i);
                var rect = (RectTransform)row.transform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -i * rowHeight);
                rect.sizeDelta = new Vector2(0f, rowHeight);
            }

            for (var i = count; i < rows.Count; i++) rows[i].gameObject.SetActive(false);
            content.sizeDelta = new Vector2(content.sizeDelta.x, count * rowHeight);
        }
    }
}
