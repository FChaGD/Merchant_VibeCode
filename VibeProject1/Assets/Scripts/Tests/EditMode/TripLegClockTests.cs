using NUnit.Framework;

namespace Game.Core.Tests
{
    public class TripLegClockTests
    {
        [Test]
        public void Extend_AddsToAdded_KeepsElapsed()
        {
            var clock = new TripLegClock(30f);
            clock.Begin(100f);
            clock.Tick(50f);
            clock.Extend(20f);
            Assert.AreEqual(120f, clock.DurationSeconds, 1e-4f);
            Assert.AreEqual(20f, clock.AddedSeconds, 1e-4f);
            Assert.AreEqual(50f, clock.ElapsedSeconds, 1e-4f);
        }

        [Test]
        public void Shorten_NeverBelowElapsed()
        {
            var clock = new TripLegClock(30f);
            clock.Begin(100f);
            clock.Tick(90f);
            var actual = clock.Shorten(30f);
            Assert.AreEqual(10f, actual, 1e-4f, "남은 10초만 줄어든다");
            Assert.AreEqual(90f, clock.DurationSeconds, 1e-4f);
            Assert.AreEqual(1f, clock.Progress, 1e-4f);
            Assert.AreEqual(-10f, clock.AddedSeconds, 1e-4f);
        }

        [Test]
        public void Rebase_KeepsProgressRatio_AndAddedSeconds()
        {
            var clock = new TripLegClock(30f);
            clock.Begin(100f);
            clock.Extend(20f);          // 총 120
            clock.Tick(60f);            // 진행 0.5
            clock.Rebase(180f);         // 총 200
            Assert.AreEqual(200f, clock.DurationSeconds, 1e-4f);
            Assert.AreEqual(0.5f, clock.Progress, 1e-4f);
            Assert.AreEqual(100f, clock.ElapsedSeconds, 1e-4f);
            Assert.AreEqual(20f, clock.AddedSeconds, 1e-4f);
        }

        [Test]
        public void Begin_ResetsAdded()
        {
            var clock = new TripLegClock(30f);
            clock.Begin(100f);
            clock.Extend(20f);
            clock.Begin(50f);
            Assert.AreEqual(0f, clock.AddedSeconds);
            Assert.AreEqual(50f, clock.DurationSeconds, 1e-4f);
            Assert.AreEqual(0f, clock.Progress);
        }

        [Test]
        public void ShortenAndRebase_TotalNeverBelowMinimum()
        {
            var clock = new TripLegClock(30f);
            clock.Begin(100f);
            clock.Shorten(100f);        // 경과 0 → 총 0으로 내려가려 함
            Assert.Greater(clock.DurationSeconds, 0f);
            clock.Rebase(5f);
            Assert.Greater(clock.DurationSeconds, 0f);
        }

        [Test]
        public void Tick_ReturnsMoved_ClampedAtDuration()
        {
            var clock = new TripLegClock(30f);
            clock.Begin(100f);
            Assert.AreEqual(95f, clock.Tick(95f), 1e-4f);
            Assert.AreEqual(5f, clock.Tick(10f), 1e-4f, "도착 프레임 초과분은 이동이 아니다");
            Assert.AreEqual(0f, clock.Tick(1f), 1e-4f);
            Assert.AreEqual(0f, clock.Tick(-1f), 1e-4f);
        }

        [Test]
        public void Tick_AfterShortenToElapsed_ReturnsZero()
        {
            var clock = new TripLegClock(30f);
            clock.Begin(100f);
            clock.Tick(40f);
            clock.Shorten(100f);        // 남은 시간 0
            Assert.AreEqual(0f, clock.Tick(1f), 1e-4f);
        }
    }
}
