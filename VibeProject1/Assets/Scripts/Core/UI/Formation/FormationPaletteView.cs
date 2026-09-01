using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Core
{
    /// <summary>
    /// 배치 UI의 팔레트(보유 유닛 목록) 영역. 유닛 배치 상한 기획(11번) §4 확정대로 개체 하나당
    /// 아이콘 하나가 아니라 카테고리(직업/마차/시설) 하나당 한 줄 + 잔여/전체 수를 보여준다(설계
    /// 16번) - 어떤 구체적 개체를 배치할지는 FormationPanel이 카테고리에서 가용 개체를 골라 해석한다.
    /// </summary>
    public class FormationPaletteView : MonoBehaviour
    {
        [SerializeField] private Transform rowContent;
        [SerializeField] private FormationPaletteRowView rowPrefab;

        private readonly List<FormationPaletteRowView> rows = new();

        // 행 인스턴스를 매번 파괴 후 재생성하지 않고, 카테고리 개수 변화분만큼만 생성/파괴하고
        // 나머지는 Bind로 내용만 덮어써 재사용한다(get-or-create, CLAUDE.md 최적화 규칙). 매번 전체를
        // 다시 바인딩하므로 이전에 어떤 요약이었든 결과는 동일하다.
        public void SetCategories(
            IReadOnlyList<FormationCategorySummary> categories,
            Action<FormationCategoryKey> categoryClicked,
            Action<FormationCategoryKey, PointerEventData> categoryBeginDrag,
            Action<PointerEventData> rowDrag,
            Action<PointerEventData> rowEndDrag)
        {
            if (rowPrefab == null || rowContent == null)
            {
                foreach (var row in rows)
                {
                    if (row != null)
                    {
                        Destroy(row.gameObject);
                    }
                }
                rows.Clear();

                Debug.LogWarning($"{nameof(FormationPaletteView)}에 {nameof(rowPrefab)} 또는 {nameof(rowContent)}가 지정되어 있지 않다.");
                return;
            }

            while (rows.Count > categories.Count)
            {
                var last = rows[^1];
                if (last != null)
                {
                    Destroy(last.gameObject);
                }
                rows.RemoveAt(rows.Count - 1);
            }

            while (rows.Count < categories.Count)
            {
                rows.Add(Instantiate(rowPrefab, rowContent));
            }

            for (var i = 0; i < categories.Count; i++)
            {
                rows[i].Bind(categories[i], categoryClicked, categoryBeginDrag, rowDrag, rowEndDrag);
            }
        }
    }
}
