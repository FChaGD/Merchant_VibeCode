using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 정비창 팔레트의 범주별 요약(전체 수·배치 가능 수) 계산(Docs/Refactor/2026-10-26_공통.md C-3a). 편집기(FormationGridEditor) 안에 있어
    /// 테스트할 수 없던 데이터 계산을 화면 밖으로 뺐다. 범주 순서는 목록에서 처음 나온 순서, 이름·아이콘은 범주의 첫 유닛 것이다.
    /// 배치 불가 판정(사망 용병·배치됨·진행 중 활동에 예약됨)은 호출자가 넘긴다 - 이 클래스는 저장소를 모른다.
    /// </summary>
    public static class FormationPaletteSummaryBuilder
    {
        public static List<FormationCategorySummary> Build(IReadOnlyList<IFormationUnit> roster, Func<IFormationUnit, bool> showsInPalette, Func<IFormationUnit, bool> isUnavailable)
        {
            var order = new List<FormationCategoryKey>();
            var totals = new Dictionary<FormationCategoryKey, int>();
            var availables = new Dictionary<FormationCategoryKey, int>();
            var firsts = new Dictionary<FormationCategoryKey, IFormationUnit>();

            foreach (var unit in roster)
            {
                if (!showsInPalette(unit)) continue;

                var key = FormationCategoryKey.Of(unit);
                if (!totals.ContainsKey(key))
                {
                    order.Add(key);
                    totals[key] = 0;
                    availables[key] = 0;
                    firsts[key] = unit;
                }

                totals[key]++;
                if (!isUnavailable(unit)) availables[key]++;
            }

            var summaries = new List<FormationCategorySummary>(order.Count);
            foreach (var key in order)
            {
                summaries.Add(new FormationCategorySummary(key, firsts[key].DisplayName, firsts[key].Icon, totals[key], availables[key]));
            }
            return summaries;
        }
    }
}
