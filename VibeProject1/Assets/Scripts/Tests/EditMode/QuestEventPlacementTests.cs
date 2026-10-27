using System;
using System.Collections.Generic;
using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>확정 사건 발생 위치(Docs/설계/107번 §9.3).</summary>
    public class QuestEventPlacementTests
    {
        private static Func<float> Rolls(params float[] values)
        {
            var queue = new Queue<float>(values);
            return () => queue.Count > 0 ? queue.Dequeue() : 0f;
        }

        [Test]
        public void Progress_MapsAcrossLegs()
        {
            var spot = QuestEventPlacement.Resolve(QuestEventTiming.Progress, new[] { 10f, 30f }, 0.5f, 0f, Rolls());
            Assert.AreEqual(1, spot.LegIndex);
            Assert.AreEqual(10f / 30f, spot.LegProgress, 1e-4f);
        }

        [Test]
        public void Progress_Zero_FirstLegStart()
        {
            var spot = QuestEventPlacement.Resolve(QuestEventTiming.Progress, new[] { 10f, 30f }, 0f, 0f, Rolls());
            Assert.AreEqual(0, spot.LegIndex);
            Assert.AreEqual(0f, spot.LegProgress);
        }

        [Test]
        public void Progress_One_LastLegEnd()
        {
            var spot = QuestEventPlacement.Resolve(QuestEventTiming.Progress, new[] { 10f, 30f }, 1f, 0f, Rolls());
            Assert.AreEqual(1, spot.LegIndex);
            Assert.AreEqual(1f, spot.LegProgress);
        }

        [Test]
        public void Progress_BoundaryGoesToNextLegStart()
        {
            var spot = QuestEventPlacement.Resolve(QuestEventTiming.Progress, new[] { 10f, 10f }, 0.5f, 0f, Rolls());
            Assert.AreEqual(1, spot.LegIndex);
            Assert.AreEqual(0f, spot.LegProgress);
        }

        [Test]
        public void Random_PicksLegAndRange()
        {
            var spot = QuestEventPlacement.Resolve(QuestEventTiming.Random, new[] { 10f, 10f, 10f }, 0.2f, 0.6f, Rolls(0.7f, 0.5f));
            Assert.AreEqual(2, spot.LegIndex);
            Assert.AreEqual(0.4f, spot.LegProgress, 1e-4f);

            spot = QuestEventPlacement.Resolve(QuestEventTiming.Random, new[] { 10f, 10f, 10f }, 0.2f, 0.6f, Rolls(0.9999f, 0f));
            Assert.AreEqual(2, spot.LegIndex);
            spot = QuestEventPlacement.Resolve(QuestEventTiming.Random, new[] { 10f, 10f }, 0f, 1f, Rolls(1f, 1f));
            Assert.AreEqual(1, spot.LegIndex, "난수 1이 와도 마지막 구간을 넘지 않는다");
        }

        [Test]
        public void Random_SwapsAndClamps()
        {
            var spot = QuestEventPlacement.Resolve(QuestEventTiming.Random, new[] { 10f }, 1.5f, 0.5f, Rolls(0f, 0f));
            Assert.AreEqual(0.5f, spot.LegProgress, 1e-4f);
            spot = QuestEventPlacement.Resolve(QuestEventTiming.Random, new[] { 10f }, 1.5f, 0.5f, Rolls(0f, 1f));
            Assert.AreEqual(1f, spot.LegProgress, 1e-4f);
        }

        [Test]
        public void NoLegs_ReturnsZeroLeg()
        {
            Assert.AreEqual(0, QuestEventPlacement.Resolve(QuestEventTiming.Progress, Array.Empty<float>(), 0.3f, 0f, Rolls()).LegIndex);
            Assert.AreEqual(0, QuestEventPlacement.Resolve(QuestEventTiming.Random, null, 0.3f, 0.5f, Rolls(0.5f, 0.5f)).LegIndex);
        }
    }
}
