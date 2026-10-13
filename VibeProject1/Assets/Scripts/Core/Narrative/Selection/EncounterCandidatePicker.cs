using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 대화·이벤트 공용 후보 거르기와 가중치 추첨(설계 87 §5.2). 순수 함수 묶음 - 난수는 주입받아 테스트에서 고정한다.
    /// 횟수 제한: 이벤트 전부(Once = 게임 전체, PerTrip = 이번 상행), 대화는 랜덤·수동만 이번 상행 1회. 계기(확정) 대화와 반복 대화는 제한 없다
    /// (기획 86 §4.3, §8-9 - 확정 대화 제한은 실전 테스트 후 결정).
    /// </summary>
    public static class EncounterCandidatePicker
    {
        public static bool IsEventExhausted(EventEntry entry, IEncounterHistoryReader history)
            => entry.Repeat == EventRepeat.Once ? history.HasOccurred(entry.Id) : history.OccurredThisTrip(entry.Id);

        public static bool IsDialogueExhausted(DialogueEntry entry, IEncounterHistoryReader history)
            => (entry.Occurrence == DialogueOccurrence.Random || entry.Occurrence == DialogueOccurrence.Manual) && history.OccurredThisTrip(entry.Id);

        public static List<EventEntry> FilterEvents(IReadOnlyList<EventEntry> entries, EventOccurrence occurrence, IEncounterHistoryReader history,
            EncounterConditionRegistry conditions, EncounterConditionContext context)
        {
            var result = new List<EventEntry>();
            foreach (var entry in entries)
            {
                if (entry.Occurrence != occurrence) continue;
                if (IsEventExhausted(entry, history)) continue;
                if (!conditions.EvaluateAll(entry.Conditions, context)) continue;
                result.Add(entry);
            }
            return result;
        }

        public static List<DialogueEntry> FilterDialogues(IReadOnlyList<DialogueEntry> entries, Func<DialogueEntry, bool> kind, IEncounterHistoryReader history,
            Func<string, bool> isSpeakerAvailable, EncounterConditionRegistry conditions, EncounterConditionContext context)
        {
            var result = new List<DialogueEntry>();
            foreach (var entry in entries)
            {
                if (!kind(entry)) continue;
                if (IsDialogueExhausted(entry, history)) continue;
                if (!AllSpeakersAvailable(entry, isSpeakerAvailable)) continue;
                if (!conditions.EvaluateAll(entry.Conditions, context)) continue;
                result.Add(entry);
            }
            return result;
        }

        public static T PickWeighted<T>(IReadOnlyList<T> candidates, Func<T, int> weightOf, Func<float> random01) where T : class
        {
            if (candidates == null || candidates.Count == 0) return null;

            var total = 0;
            foreach (var c in candidates) total += Math.Max(0, weightOf(c));
            // 전부 0이면 균등 추첨(사용자 결정, 2026-10-13). random01이 1을 돌려줘도(UnityEngine.Random.value) 범위 안에 둔다.
            if (total <= 0) return candidates[Math.Min(candidates.Count - 1, (int)(random01() * candidates.Count))];

            var roll = random01() * total;
            foreach (var c in candidates)
            {
                roll -= Math.Max(0, weightOf(c));
                if (roll < 0f) return c;
            }
            // random01이 정확히 1이면 여기로 온다 - 가중치 0인 행이 뽑히지 않게 마지막 양수 행을 돌려준다.
            for (var i = candidates.Count - 1; i >= 0; i--)
            {
                if (weightOf(candidates[i]) > 0) return candidates[i];
            }
            return candidates[candidates.Count - 1];
        }

        private static bool AllSpeakersAvailable(DialogueEntry entry, Func<string, bool> isSpeakerAvailable)
        {
            if (entry.Speakers == null) return true;
            foreach (var speaker in entry.Speakers)
            {
                if (!isSpeakerAvailable(speaker)) return false;
            }
            return true;
        }
    }
}
