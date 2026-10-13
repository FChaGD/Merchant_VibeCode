using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class FixedEventQueueTests
    {
        private static (EventEntry, int) E(string id, EventImportance imp, int tableIndex)
            => (new EventEntry { Id = id, Occurrence = EventOccurrence.Fixed, Importance = imp }, tableIndex);

        private static List<string> Ids(FixedEventQueue q) => new List<EventEntry>(q.Snapshot()).ConvertAll(e => e.Id);

        [Test]
        public void SameImportance_IsFifoAcrossChecks()
        {
            var q = new FixedEventQueue();
            q.EnqueueSatisfied(new[] { E("late-table", EventImportance.Normal, 5) });
            q.EnqueueSatisfied(new[] { E("early-table", EventImportance.Normal, 0) });
            CollectionAssert.AreEqual(new[] { "late-table", "early-table" }, Ids(q));
        }

        [Test]
        public void SameCheck_UsesTableOrder()
        {
            var q = new FixedEventQueue();
            q.EnqueueSatisfied(new[] { E("b", EventImportance.Normal, 1), E("a", EventImportance.Normal, 3) });
            CollectionAssert.AreEqual(new[] { "b", "a" }, Ids(q));
        }

        [Test]
        public void HigherImportance_JumpsAhead()
        {
            var q = new FixedEventQueue();
            q.EnqueueSatisfied(new[] { E("normal", EventImportance.Normal, 0), E("side", EventImportance.Side, 1) });
            q.EnqueueSatisfied(new[] { E("scenario", EventImportance.Scenario, 9), E("core", EventImportance.Core, 8) });
            CollectionAssert.AreEqual(new[] { "scenario", "core", "side", "normal" }, Ids(q));
        }

        [Test]
        public void AlreadyQueued_IsNotDuplicated()
        {
            var q = new FixedEventQueue();
            q.EnqueueSatisfied(new[] { E("a", EventImportance.Normal, 0) });
            q.EnqueueSatisfied(new[] { E("a", EventImportance.Normal, 0) });
            Assert.AreEqual(1, q.Count);
        }

        [Test]
        public void TryDequeue_SkipsInvalid_AndInvalidCanReenterAtBack()
        {
            var q = new FixedEventQueue();
            q.EnqueueSatisfied(new[] { E("x", EventImportance.Normal, 0), E("y", EventImportance.Normal, 1) });
            Assert.IsTrue(q.TryDequeue(e => e.Id != "x", out var got));
            Assert.AreEqual("y", got.Id);
            Assert.AreEqual(0, q.Count);

            q.EnqueueSatisfied(new[] { E("z", EventImportance.Normal, 2) });
            q.EnqueueSatisfied(new[] { E("x", EventImportance.Normal, 0) });
            CollectionAssert.AreEqual(new[] { "z", "x" }, Ids(q));
        }

        [Test]
        public void TryDequeue_EmptyOrAllInvalid_ReturnsFalse()
        {
            var q = new FixedEventQueue();
            Assert.IsFalse(q.TryDequeue(_ => true, out _));
            q.EnqueueSatisfied(new[] { E("a", EventImportance.Normal, 0) });
            Assert.IsFalse(q.TryDequeue(_ => false, out var none));
            Assert.IsNull(none);
        }

        [Test]
        public void Clear_Empties()
        {
            var q = new FixedEventQueue();
            q.EnqueueSatisfied(new[] { E("a", EventImportance.Normal, 0) });
            q.Clear();
            Assert.AreEqual(0, q.Count);
        }
    }
}
