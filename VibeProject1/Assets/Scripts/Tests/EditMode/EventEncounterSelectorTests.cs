using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class EventEncounterSelectorTests
    {
        private static EventEntry Ev(string id, EventOccurrence occurrence, EventImportance importance = EventImportance.Normal, int weight = 100)
            => new() { Id = id, YarnNode = id, Occurrence = occurrence, Importance = importance, Weight = weight };

        private static EventEntry Pick(FixedEventQueue queue, List<EventEntry> events, EncounterHistory history, out int rolls, float roll)
        {
            var count = 0;
            var result = EventEncounterSelector.PickForCheck(queue, events, history, EncounterConditionRegistry.CreateDefault(), default, () => { count++; return roll; });
            rolls = count;
            return result;
        }

        [Test]
        public void Chance_IsTwoPerFiveMinutes()
        {
            Assert.AreEqual(2f * 5f / 300f, EventEncounterSelector.RandomEventChance, 1e-5f);
        }

        [Test]
        public void FixedSatisfied_RunsWithoutRandomRoll()
        {
            var events = new List<EventEntry> { Ev("R", EventOccurrence.Random), Ev("F", EventOccurrence.Fixed) };
            var picked = Pick(new FixedEventQueue(), events, new EncounterHistory(), out var rolls, 0f);
            Assert.AreEqual("F", picked.Id);
            Assert.AreEqual(0, rolls, "확정 차례가 있으면 랜덤 판정 없음");
        }

        [Test]
        public void NoFixed_RollAboveChance_Nothing()
        {
            var events = new List<EventEntry> { Ev("R", EventOccurrence.Random) };
            Assert.IsNull(Pick(new FixedEventQueue(), events, new EncounterHistory(), out _, 0.05f));
        }

        [Test]
        public void NoFixed_RollBelowChance_PicksRandom()
        {
            var events = new List<EventEntry> { Ev("R", EventOccurrence.Random) };
            Assert.AreEqual("R", Pick(new FixedEventQueue(), events, new EncounterHistory(), out _, 0.01f).Id);
        }

        [Test]
        public void ExhaustedFixed_NotQueued_FallsBackToRandom()
        {
            var events = new List<EventEntry> { Ev("F", EventOccurrence.Fixed), Ev("R", EventOccurrence.Random) };
            events[0].Repeat = EventRepeat.Once;
            var history = new EncounterHistory();
            history.RecordCompleted("F");
            Assert.AreEqual("R", Pick(new FixedEventQueue(), events, history, out _, 0.01f).Id);
        }

        [Test]
        public void Importance_OrdersQueue()
        {
            var events = new List<EventEntry> { Ev("N", EventOccurrence.Fixed, EventImportance.Normal), Ev("S", EventOccurrence.Fixed, EventImportance.Scenario) };
            var queue = new FixedEventQueue();
            Assert.AreEqual("S", Pick(queue, events, new EncounterHistory(), out _, 0f).Id);
            Assert.AreEqual(1, queue.Count, "N은 대기열에 남는다");
        }
    }
}
