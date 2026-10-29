using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 휴식 이벤트 고르기(설계 109 §5.5). 확정은 하루 1개 - 성립한 것 중 중요도 → 테이블 순서로 첫 번째, 대기열은 두지 않는다(남은 것은 다음 날 다시 성립하면
    /// 그때). 랜덤은 그날 확률을 통과했을 때만 가중치 추첨. 소진·조건 판정은 상행과 같은 함수를 쓴다(이력 공유).
    /// </summary>
    public static class RestEventSelector
    {
        public static EventEntry PickFixed(IReadOnlyList<EventEntry> restEvents, IEncounterHistoryReader history, EncounterConditionRegistry conditions,
            EncounterConditionContext context)
        {
            var candidates = EncounterCandidatePicker.FilterEvents(restEvents, EventOccurrence.Fixed, history, conditions, context);
            EventEntry best = null;
            // 필터 결과는 테이블 순서다 - 중요도가 더 앞선 것만 바꾸면 같은 중요도는 테이블 순서가 남는다.
            foreach (var e in candidates)
            {
                if (best == null || e.Importance < best.Importance) best = e;
            }
            return best;
        }

        public static EventEntry PickRandom(IReadOnlyList<EventEntry> restEvents, IEncounterHistoryReader history, EncounterConditionRegistry conditions,
            EncounterConditionContext context, Func<float> random01, float chance)
        {
            if (random01() >= chance) return null;
            var candidates = EncounterCandidatePicker.FilterEvents(restEvents, EventOccurrence.Random, history, conditions, context);
            return EncounterCandidatePicker.PickWeighted(candidates, e => e.Weight, random01);
        }
    }
}
