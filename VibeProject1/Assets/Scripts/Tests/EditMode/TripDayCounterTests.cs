using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    public class TripDayCounterTests
    {
        private static readonly TripTravelSettings Settings = TripTravelSettings.Default();

        [Test]
        public void AddMoved_AdvancesOnlyWhenCrossingBoundary()
        {
            var counter = new TripDayCounter();
            Assert.AreEqual(0, counter.AddMoved(Settings, 29.9f));
            Assert.AreEqual(1, counter.AddMoved(Settings, 0.1f));
            Assert.AreEqual(0f, counter.RemainderSeconds, 1e-3f);
            Assert.AreEqual(30f, counter.MovedSeconds, 1e-3f);
        }

        [Test]
        public void AddMoved_LargeDelta_ReturnsSeveralDaysAndKeepsRemainder()
        {
            var counter = new TripDayCounter();
            Assert.AreEqual(3, counter.AddMoved(Settings, 95f));
            Assert.AreEqual(5f, counter.RemainderSeconds, 1e-3f);
            Assert.AreEqual(25f, counter.SecondsUntilNextDay(Settings), 1e-3f);
        }

        [Test]
        public void AddMoved_JustBelowBoundary_CountsDayWithoutNegativeRemainder()
        {
            var counter = new TripDayCounter();
            Assert.AreEqual(3, counter.AddMoved(Settings, 89.99999f));
            Assert.GreaterOrEqual(counter.RemainderSeconds, 0f);
        }

        [Test]
        public void PlanningExample_MoveThenRout_IsEightDays()
        {
            // 기획 92 §3 예시: 구간 1:30 도착 + 두 번째 구간 1:40에 궤주.
            var counter = new TripDayCounter();
            var days = counter.AddMoved(Settings, 90f);
            counter.BeginLeg(); // 중간 도시 도착
            days += counter.AddMoved(Settings, 100f);
            Assert.AreEqual(6, days);
            Assert.AreEqual(10f, counter.RemainderSeconds, 1e-3f);
            Assert.AreEqual(2, counter.PreviewRout(Settings), "미리보기는 상태를 바꾸지 않는다");
            Assert.AreEqual(2, counter.SettleRout(Settings));
            Assert.AreEqual(0f, counter.LegMovedSeconds);
            Assert.AreEqual(0f, counter.RemainderSeconds);
        }

        [Test]
        public void Reset_StartsFromZero()
        {
            var counter = new TripDayCounter();
            counter.AddMoved(Settings, 29f);
            counter.Reset();
            Assert.AreEqual(0, counter.AddMoved(Settings, 29f), "상행 끝 나머지는 버린다");
            Assert.AreEqual(29f, counter.MovedSeconds, 1e-3f);
        }

        [Test]
        public void Rout_MultiplierAtOrBelowOne_AddsNothing()
        {
            var settings = TripTravelSettings.Default();
            settings.RoutDayMultiplier = 0.5f;
            var counter = new TripDayCounter();
            counter.AddMoved(settings, 20f);
            Assert.AreEqual(0, counter.SettleRout(settings));
        }
    }
}
