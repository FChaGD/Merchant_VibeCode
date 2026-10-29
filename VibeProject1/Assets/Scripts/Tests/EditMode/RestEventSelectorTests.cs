using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    /// <summary>휴식 이벤트 고르기(설계 109 §5.5).</summary>
    public class RestEventSelectorTests
    {
        private static readonly EncounterConditionRegistry Conditions = EncounterConditionRegistry.CreateDefault();

        private static EncounterConditionContext Town(int cityId = 5, EncounterHistory history = null)
            => EncounterConditionContextFactory.CreateForTown(new GameDate(69), cityId, _ => 3, history ?? new EncounterHistory());

        private static EventEntry Fixed(string id, EventImportance importance = EventImportance.Normal, EventRepeat repeat = EventRepeat.PerTrip, string conditions = null)
            => new() { Id = id, Occurrence = EventOccurrence.Fixed, Importance = importance, Repeat = repeat, Place = EventPlace.Rest,
                Conditions = conditions == null ? new List<EncounterCondition>() : EncounterConditionParser.Parse(conditions) };

        private static EventEntry Random(string id, int weight = 100)
            => new() { Id = id, Occurrence = EventOccurrence.Random, Weight = weight, Place = EventPlace.Rest };

        [Test]
        public void PickFixed_ImportanceThenTableOrder()
        {
            var events = new List<EventEntry> { Fixed("A"), Fixed("B", EventImportance.Core), Fixed("C", EventImportance.Core) };
            var h = new EncounterHistory();
            Assert.AreEqual("B", RestEventSelector.PickFixed(events, h, Conditions, Town(history: h)).Id);
        }

        [Test]
        public void PickFixed_SkipsExhausted_OnceAndSpan()
        {
            var events = new List<EventEntry> { Fixed("O", EventImportance.Scenario, EventRepeat.Once), Fixed("P", EventImportance.Core), Fixed("N") };
            var h = new EncounterHistory();
            h.RecordCompleted("O");
            h.RecordCompleted("P");
            Assert.AreEqual("N", RestEventSelector.PickFixed(events, h, Conditions, Town(history: h)).Id);

            h.ResetTrip();   // 다음 휴식 - 회차 기록만 비워져 반복 이벤트는 다시, 1회성은 계속 소진
            Assert.AreEqual("P", RestEventSelector.PickFixed(events, h, Conditions, Town(history: h)).Id);
        }

        [Test]
        public void PickFixed_ConditionFails_Null()
        {
            var events = new List<EventEntry> { Fixed("T", conditions: "town:4") };
            Assert.IsNull(RestEventSelector.PickFixed(events, new EncounterHistory(), Conditions, Town(5)));
            Assert.AreEqual("T", RestEventSelector.PickFixed(events, new EncounterHistory(), Conditions, Town(4)).Id);
        }

        [Test]
        public void PickRandom_BelowChance_PicksWeighted()
        {
            var events = new List<EventEntry> { Fixed("F"), Random("R1", 0), Random("R2", 100) };
            var rolls = new Queue<float>(new[] { 0.1f, 0.5f });
            Assert.AreEqual("R2", RestEventSelector.PickRandom(events, new EncounterHistory(), Conditions, Town(), () => rolls.Dequeue(), 0.35f).Id);
        }

        [Test]
        public void PickRandom_AtOrAboveChance_Null()
        {
            var events = new List<EventEntry> { Random("R1") };
            Assert.IsNull(RestEventSelector.PickRandom(events, new EncounterHistory(), Conditions, Town(), () => 0.35f, 0.35f));
        }

        [Test]
        public void PickRandom_NoCandidates_Null()
        {
            var events = new List<EventEntry> { Fixed("F") };
            Assert.IsNull(RestEventSelector.PickRandom(events, new EncounterHistory(), Conditions, Town(), () => 0f, 0.35f));
        }
    }
}
