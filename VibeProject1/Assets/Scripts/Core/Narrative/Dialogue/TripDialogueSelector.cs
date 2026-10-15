using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 상행 대화 고르기 규칙(설계 88 §3·§4.1). 20초 검사: 쌓인 일 중 '이벤트 직후'를 먼저, 그다음 먼저 생긴 순서로 상황 대화(테이블 순서 첫 행)를 찾고,
    /// 없으면 30% 판정 후 랜덤 대화 가중치 추첨. 수동 걸기: 그 상단원이 화자인 안 한 수동 대화(테이블 순서 첫 행) → 없으면 반복 대화 가중치 추첨.
    /// 횟수 제한·화자 고용·조건은 설계 87의 후보 거르기를 그대로 쓴다.
    /// </summary>
    public static class TripDialogueSelector
    {
        public const float RandomDialogueChance = 0.3f;

        public static bool TryMapMilestone(TripMilestone milestone, out DialogueTrigger trigger)
        {
            switch (milestone)
            {
                case TripMilestone.TripStarted: trigger = DialogueTrigger.Departure; return true;
                case TripMilestone.BattleEnded: trigger = DialogueTrigger.AfterBattle; return true;
                case TripMilestone.EventEnded: trigger = DialogueTrigger.AfterEvent; return true;
                case TripMilestone.MidCityArrived: trigger = DialogueTrigger.MidCityArrival; return true;
                default: trigger = DialogueTrigger.None; return false;
            }
        }

        public static DialogueEntry PickForCheck(IReadOnlyList<DialogueTrigger> pending, IReadOnlyList<DialogueEntry> dialogues, IEncounterHistoryReader history,
            Func<string, bool> isSpeakerAvailable, EncounterConditionRegistry conditions, EncounterConditionContext context, Func<float> random01)
        {
            var triggered = PickTriggered(pending, dialogues, history, isSpeakerAvailable, conditions, context);
            if (triggered != null) return triggered;

            if (random01() >= RandomDialogueChance) return null;
            var randoms = EncounterCandidatePicker.FilterDialogues(dialogues, d => d.Occurrence == DialogueOccurrence.Random, history, isSpeakerAvailable, conditions, context);
            return EncounterCandidatePicker.PickWeighted(randoms, d => d.Weight, random01);
        }

        /// <summary>상황 대화만 찾는다(랜덤 대체 없음) - 20초 검사와 전투 후 5초 대기(진행자)가 함께 쓴다.</summary>
        public static DialogueEntry PickTriggered(IReadOnlyList<DialogueTrigger> pending, IReadOnlyList<DialogueEntry> dialogues, IEncounterHistoryReader history,
            Func<string, bool> isSpeakerAvailable, EncounterConditionRegistry conditions, EncounterConditionContext context)
        {
            if (Contains(pending, DialogueTrigger.AfterEvent))
            {
                var afterEvent = FirstForTrigger(DialogueTrigger.AfterEvent, dialogues, history, isSpeakerAvailable, conditions, context);
                if (afterEvent != null) return afterEvent;
            }
            foreach (var trigger in pending)
            {
                if (trigger == DialogueTrigger.AfterEvent) continue;
                var entry = FirstForTrigger(trigger, dialogues, history, isSpeakerAvailable, conditions, context);
                if (entry != null) return entry;
            }
            return null;
        }

        public static DialogueEntry PickManual(string characterId, IReadOnlyList<DialogueEntry> dialogues, IEncounterHistoryReader history,
            Func<string, bool> isSpeakerAvailable, EncounterConditionRegistry conditions, EncounterConditionContext context, Func<float> random01)
        {
            var manual = EncounterCandidatePicker.FilterDialogues(dialogues, d => d.Occurrence == DialogueOccurrence.Manual && Speaks(d, characterId), history, isSpeakerAvailable, conditions, context);
            if (manual.Count > 0) return manual[0];
            var repeat = EncounterCandidatePicker.FilterDialogues(dialogues, d => d.Occurrence == DialogueOccurrence.ManualRepeat && Speaks(d, characterId), history, isSpeakerAvailable, conditions, context);
            return EncounterCandidatePicker.PickWeighted(repeat, d => d.Weight, random01);
        }

        /// <summary>수동 걸기 목록의 "할 말 없음" 판정 - 목록을 열 때 상단원별로 부른다(설계 88 §4.1).</summary>
        public static bool HasManualTalk(string characterId, IReadOnlyList<DialogueEntry> dialogues, IEncounterHistoryReader history,
            Func<string, bool> isSpeakerAvailable, EncounterConditionRegistry conditions, EncounterConditionContext context)
            => PickManual(characterId, dialogues, history, isSpeakerAvailable, conditions, context, () => 0f) != null;

        private static DialogueEntry FirstForTrigger(DialogueTrigger trigger, IReadOnlyList<DialogueEntry> dialogues, IEncounterHistoryReader history,
            Func<string, bool> isSpeakerAvailable, EncounterConditionRegistry conditions, EncounterConditionContext context)
        {
            var candidates = EncounterCandidatePicker.FilterDialogues(dialogues, d => d.Occurrence == DialogueOccurrence.Trigger && d.Trigger == trigger, history, isSpeakerAvailable, conditions, context);
            return candidates.Count > 0 ? candidates[0] : null;
        }

        private static bool Speaks(DialogueEntry entry, string characterId)
        {
            if (entry.Speakers == null) return false;
            foreach (var s in entry.Speakers) if (s == characterId) return true;
            return false;
        }

        private static bool Contains(IReadOnlyList<DialogueTrigger> list, DialogueTrigger value)
        {
            foreach (var t in list) if (t == value) return true;
            return false;
        }
    }
}
