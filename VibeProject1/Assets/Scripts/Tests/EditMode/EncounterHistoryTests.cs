using NUnit.Framework;

namespace Game.Core.Tests
{
    public class EncounterHistoryTests
    {
        [Test]
        public void Flags_SetAndClear()
        {
            var h = new EncounterHistory();
            h.SetFlag("helped_cart", true);
            Assert.IsTrue(h.HasFlag("helped_cart"));
            h.SetFlag("helped_cart", false);
            Assert.IsFalse(h.HasFlag("helped_cart"));
            CollectionAssert.IsEmpty(h.ActiveFlags);
        }

        [Test]
        public void TryGetFlag_DistinguishesExplicitFalseFromUnset()
        {
            // Yarn은 저장소에 값이 없으면 선언 기본값을 쓴다 - false로 바꾼 값은 "없음"이 아니라 false로 돌려줘야 한다.
            var h = new EncounterHistory();
            Assert.IsFalse(h.TryGetFlag("x", out _));
            h.SetFlag("x", true);
            Assert.IsTrue(h.TryGetFlag("x", out var on));
            Assert.IsTrue(on);
            h.SetFlag("x", false);
            Assert.IsTrue(h.TryGetFlag("x", out var off));
            Assert.IsFalse(off);
            h.ResetAll();
            Assert.IsFalse(h.TryGetFlag("x", out _));
        }

        [Test]
        public void RecordCompleted_MarksBothScopes()
        {
            var h = new EncounterHistory();
            h.RecordCompleted("E1");
            Assert.IsTrue(h.HasOccurred("E1"));
            Assert.IsTrue(h.OccurredThisTrip("E1"));
        }

        [Test]
        public void ResetTrip_ClearsOnlyTripScope()
        {
            var h = new EncounterHistory();
            h.SetFlag("x", true);
            h.RecordCompleted("E1");
            h.ResetTrip();
            Assert.IsTrue(h.HasOccurred("E1"));
            Assert.IsFalse(h.OccurredThisTrip("E1"));
            Assert.IsTrue(h.HasFlag("x"));
        }

        [Test]
        public void ResetAll_ClearsEverything()
        {
            var h = new EncounterHistory();
            h.SetFlag("x", true);
            h.RecordCompleted("E1");
            h.SetScriptValue("$count", 2f);
            h.ResetAll();
            Assert.IsFalse(h.HasFlag("x"));
            Assert.IsFalse(h.HasOccurred("E1"));
            Assert.IsFalse(h.TryGetScriptValue("$count", out _));
        }

        [Test]
        public void ScriptValues_RoundTrip()
        {
            var h = new EncounterHistory();
            h.SetScriptValue("$count", 3f);
            Assert.IsTrue(h.TryGetScriptValue("$count", out var v));
            Assert.AreEqual(3f, v);
        }

        [Test]
        public void NullOrEmptyIds_AreIgnored()
        {
            var h = new EncounterHistory();
            h.SetFlag(null, true);
            h.RecordCompleted("");
            Assert.IsEmpty(h.ActiveFlags);
            Assert.IsEmpty(h.OccurredIds);
        }
    }
}
