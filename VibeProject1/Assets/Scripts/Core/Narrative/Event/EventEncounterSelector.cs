using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 이벤트 판정 한 번의 고르기(설계 89 §4.1 ①, 87 §5.4). 판정 신호마다 성립한 확정 이벤트를 대기열에 넣고, 차례가 있으면(실행 직전 재검사 통과) 그것을
    /// 고른다 - 그 시점에는 랜덤 판정을 하지 않는다(확정 > 랜덤). 없으면 5분 평균 2회(회당 약 3.3%, 난이도 무관) 판정 후 랜덤 후보 가중치 추첨.
    /// </summary>
    public static class EventEncounterSelector
    {
        public const float RandomEventsPerFiveMinutes = 2f;

        public static float RandomEventChance => TripTravelRules.ComputeCheckProbability(RandomEventsPerFiveMinutes, TripCheckSchedule.EventIntervalSeconds);

        public static EventEntry PickForCheck(FixedEventQueue queue, IReadOnlyList<EventEntry> events, IEncounterHistoryReader history,
            EncounterConditionRegistry conditions, EncounterConditionContext context, Func<float> random01)
        {
            queue.EnqueueSatisfied(SatisfiedFixed(events, history, conditions, context));
            if (queue.TryDequeue(e => IsAvailable(e, history, conditions, context), out var fixedEvent)) return fixedEvent;

            if (random01() >= RandomEventChance) return null;
            var randoms = EncounterCandidatePicker.FilterEvents(events, EventOccurrence.Random, history, conditions, context);
            return EncounterCandidatePicker.PickWeighted(randoms, e => e.Weight, random01);
        }

        private static List<(EventEntry entry, int tableIndex)> SatisfiedFixed(IReadOnlyList<EventEntry> events, IEncounterHistoryReader history,
            EncounterConditionRegistry conditions, EncounterConditionContext context)
        {
            var list = new List<(EventEntry, int)>();
            for (var i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e.Occurrence == EventOccurrence.Fixed && IsAvailable(e, history, conditions, context)) list.Add((e, i));
            }
            return list;
        }

        private static bool IsAvailable(EventEntry e, IEncounterHistoryReader history, EncounterConditionRegistry conditions, EncounterConditionContext context)
            => !EncounterCandidatePicker.IsEventExhausted(e, history) && conditions.EvaluateAll(e.Conditions, context);
    }
}
