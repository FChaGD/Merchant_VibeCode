using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class EncounterCandidatePickerTests
    {
        private static readonly EncounterConditionRegistry Registry = EncounterConditionRegistry.CreateDefault();

        private static EncounterConditionContext Ctx(EncounterHistory h)
            => new(new GameDate(0), true, 1, 2, 1, 1, 0.5f, h);

        private static EventEntry Ev(string id, EventOccurrence o = EventOccurrence.Random, EventRepeat r = EventRepeat.PerTrip, string cond = null, int w = 100)
            => new() { Id = id, YarnNode = id, Occurrence = o, Repeat = r, Weight = w, Conditions = EncounterConditionParser.Parse(cond) };

        private static DialogueEntry Dl(string id, DialogueOccurrence o, params string[] speakers)
            => new() { Id = id, YarnNode = id, Occurrence = o, Speakers = new List<string>(speakers) };

        [Test]
        public void FilterEvents_ByOccurrenceConditionsAndRepeat()
        {
            var h = new EncounterHistory();
            h.RecordCompleted("onceDone");
            h.ResetTrip();
            h.RecordCompleted("tripDone");
            var list = new List<EventEntry>
            {
                Ev("ok"),
                Ev("fixed", EventOccurrence.Fixed),
                Ev("onceDone", r: EventRepeat.Once),
                Ev("tripDone"),
                Ev("condFalse", cond: "flag:nope"),
            };
            var result = EncounterCandidatePicker.FilterEvents(list, EventOccurrence.Random, h, Registry, Ctx(h));
            CollectionAssert.AreEqual(new[] { "ok" }, result.ConvertAll(e => e.Id));
        }

        [Test]
        public void OnceEvent_IsExhaustedEvenOnLaterTrip()
        {
            var h = new EncounterHistory();
            h.RecordCompleted("E");
            h.ResetTrip();
            Assert.IsTrue(EncounterCandidatePicker.IsEventExhausted(Ev("E", r: EventRepeat.Once), h));
            Assert.IsFalse(EncounterCandidatePicker.IsEventExhausted(Ev("E", r: EventRepeat.PerTrip), h));
        }

        [Test]
        public void Dialogues_RandomAndManualLimitedPerTrip_TriggerAndRepeatUnlimited()
        {
            var h = new EncounterHistory();
            foreach (var id in new[] { "r", "m", "t", "rep" }) h.RecordCompleted(id);
            Assert.IsTrue(EncounterCandidatePicker.IsDialogueExhausted(Dl("r", DialogueOccurrence.Random), h));
            Assert.IsTrue(EncounterCandidatePicker.IsDialogueExhausted(Dl("m", DialogueOccurrence.Manual), h));
            Assert.IsFalse(EncounterCandidatePicker.IsDialogueExhausted(Dl("t", DialogueOccurrence.Trigger), h));
            Assert.IsFalse(EncounterCandidatePicker.IsDialogueExhausted(Dl("rep", DialogueOccurrence.ManualRepeat), h));
        }

        [Test]
        public void FilterDialogues_RequiresAllSpeakersAvailable()
        {
            var h = new EncounterHistory();
            var list = new List<DialogueEntry>
            {
                Dl("both", DialogueOccurrence.Random, "a", "b"),
                Dl("missing", DialogueOccurrence.Random, "a", "c"),
                Dl("person", DialogueOccurrence.Random, "outsider"),
            };
            var available = new HashSet<string> { "a", "b", "outsider" };
            var result = EncounterCandidatePicker.FilterDialogues(list, d => d.Occurrence == DialogueOccurrence.Random, h, available.Contains, Registry, Ctx(h));
            CollectionAssert.AreEqual(new[] { "both", "person" }, result.ConvertAll(d => d.Id));
        }

        [Test]
        public void PickWeighted_UsesCumulativeWeights()
        {
            var list = new List<EventEntry> { Ev("a", w: 100), Ev("b", w: 300) };
            Assert.AreEqual("a", EncounterCandidatePicker.PickWeighted(list, e => e.Weight, () => 0.1f).Id);   // 40 < 100
            Assert.AreEqual("b", EncounterCandidatePicker.PickWeighted(list, e => e.Weight, () => 0.5f).Id);   // 200 ≥ 100
            Assert.AreEqual("b", EncounterCandidatePicker.PickWeighted(list, e => e.Weight, () => 0.999f).Id);
        }

        [Test]
        public void PickWeighted_EmptyReturnsNull_ZeroWeightsPickUniformly()
        {
            Assert.IsNull(EncounterCandidatePicker.PickWeighted(new List<EventEntry>(), e => e.Weight, () => 0f));
            // 가중치가 전부 0이면 균등 추첨(사용자 결정, 2026-10-13).
            var zero = new List<EventEntry> { Ev("a", w: 0), Ev("b", w: 0) };
            Assert.AreEqual("a", EncounterCandidatePicker.PickWeighted(zero, e => e.Weight, () => 0.1f).Id);
            Assert.AreEqual("b", EncounterCandidatePicker.PickWeighted(zero, e => e.Weight, () => 0.9f).Id);
            Assert.AreEqual("b", EncounterCandidatePicker.PickWeighted(zero, e => e.Weight, () => 1f).Id);   // UnityEngine.Random.value는 1을 포함한다
        }

        [Test]
        public void PickWeighted_RandomOne_NeverPicksZeroWeight()
        {
            var list = new List<EventEntry> { Ev("a", w: 5), Ev("b", w: 0) };
            Assert.AreEqual("a", EncounterCandidatePicker.PickWeighted(list, e => e.Weight, () => 1f).Id);
        }
    }
}
