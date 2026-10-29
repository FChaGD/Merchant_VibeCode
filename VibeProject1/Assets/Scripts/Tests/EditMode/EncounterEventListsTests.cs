using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class EncounterEventListsTests
    {
        private static EventEntry Ev(string id, EventPlace place) => new() { Id = id, YarnNode = id, Place = place };

        [Test]
        public void At_SplitsByPlace_KeepsTableOrder()
        {
            var lists = new EncounterEventLists(new List<EventEntry>
            {
                Ev("t1", EventPlace.Trip), Ev("r1", EventPlace.Rest), Ev("t2", EventPlace.Trip), Ev("r2", EventPlace.Rest),
            });

            CollectionAssert.AreEqual(new[] { "t1", "t2" }, Ids(lists.At(EventPlace.Trip)));
            CollectionAssert.AreEqual(new[] { "r1", "r2" }, Ids(lists.At(EventPlace.Rest)));
        }

        [Test]
        public void Empty_ReturnsEmpty()
        {
            var lists = new EncounterEventLists(new List<EventEntry>());
            Assert.AreEqual(0, lists.At(EventPlace.Trip).Count);
            Assert.AreEqual(0, lists.At(EventPlace.Rest).Count);
        }

        [Test]
        public void NullList_ReturnsEmpty()
        {
            var lists = new EncounterEventLists(null);
            Assert.AreEqual(0, lists.At(EventPlace.Rest).Count);
        }

        private static List<string> Ids(IReadOnlyList<EventEntry> events)
        {
            var ids = new List<string>();
            foreach (var e in events) ids.Add(e.Id);
            return ids;
        }
    }
}
