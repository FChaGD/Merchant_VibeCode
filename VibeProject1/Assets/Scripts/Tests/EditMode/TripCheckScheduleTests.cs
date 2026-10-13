using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class TripCheckScheduleTests
    {
        private static List<(float time, TripCheckKind kind)> Run(float total, float step)
        {
            var s = new TripCheckSchedule();
            var fired = new List<TripCheckKind>();
            var log = new List<(float, TripCheckKind)>();
            for (var t = step; t <= total + 1e-4f; t += step)
            {
                s.Advance(step, fired);
                foreach (var k in fired) log.Add((t, k));
            }
            return log;
        }

        [Test]
        public void FirstSignals_AtOffsets()
        {
            var log = Run(5f, 0.25f);
            Assert.AreEqual((1.25f, TripCheckKind.Dialogue), log[0]);
            Assert.AreEqual((2.5f, TripCheckKind.Event), log[1]);
            Assert.AreEqual((5f, TripCheckKind.Battle), log[2]);
        }

        [Test]
        public void Counts_Over60Seconds()
        {
            var log = Run(60f, 0.25f);
            Assert.AreEqual(12, log.FindAll(x => x.kind == TripCheckKind.Battle).Count);
            Assert.AreEqual(3, log.FindAll(x => x.kind == TripCheckKind.Dialogue).Count);   // 1.25, 21.25, 41.25
            Assert.AreEqual(12, log.FindAll(x => x.kind == TripCheckKind.Event).Count);     // 2.5 ... 57.5
        }

        [Test]
        public void BigDelta_FiresEachBoundaryInTimeOrder()
        {
            var s = new TripCheckSchedule();
            var fired = new List<TripCheckKind>();
            s.Advance(6f, fired);
            CollectionAssert.AreEqual(new[] { TripCheckKind.Dialogue, TripCheckKind.Event, TripCheckKind.Battle }, fired);
        }

        [Test]
        public void Reset_StartsOverFromZero()
        {
            var s = new TripCheckSchedule();
            var fired = new List<TripCheckKind>();
            s.Advance(4f, fired);
            s.Reset();
            s.Advance(4.9f, fired);
            CollectionAssert.DoesNotContain(fired, TripCheckKind.Battle);
            s.Advance(0.1f, fired);
            CollectionAssert.AreEqual(new[] { TripCheckKind.Battle }, fired);
        }

        [Test]
        public void ZeroOrNegativeDelta_FiresNothing()
        {
            var s = new TripCheckSchedule();
            var fired = new List<TripCheckKind> { TripCheckKind.Battle };
            s.Advance(0f, fired);
            CollectionAssert.IsEmpty(fired);
            Assert.AreEqual(1.25f, s.SecondsUntil(TripCheckKind.Dialogue), 1e-5f);
        }
    }
}
