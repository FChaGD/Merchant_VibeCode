using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    public class TravelDayRulesTests
    {
        private static readonly TripTravelSettings Settings = TripTravelSettings.Default();

        [TestCase(0f, 0)]
        [TestCase(29.9f, 0)]
        [TestCase(30f, 1)]
        [TestCase(89.99999f, 3)]
        [TestCase(100f, 3)]
        [TestCase(-5f, 0)]
        public void ToDays_FloorsBySecondsPerDay(float seconds, int expected)
        {
            Assert.AreEqual(expected, TravelDayRules.ToDays(Settings, seconds));
        }

        [Test]
        public void ToRoutDays_AppliesMultiplierThenFloors()
        {
            Assert.AreEqual(5, TravelDayRules.ToRoutDays(Settings, 100f)); // 150초
            Assert.AreEqual(0, TravelDayRules.ToRoutDays(Settings, 19f));  // 28.5초
        }

        [Test]
        public void PlanningExample_ArrivalThenRout_IsEightDays()
        {
            // 기획 84 §4.2 예시: 1구간 90초 도착 + 2구간 100초 궤주.
            Assert.AreEqual(8, TravelDayRules.ToDays(Settings, 90f) + TravelDayRules.ToRoutDays(Settings, 100f));
        }

        [Test]
        public void NonPositiveSettings_FallBackToDefaults()
        {
            // 기존 씬에 저장된 설정에는 새 필드가 없어 0으로 들어올 수 있다.
            var broken = TripTravelSettings.Default();
            broken.SecondsPerDay = 0f;
            broken.RoutDayMultiplier = -1f;
            Assert.AreEqual(3, TravelDayRules.ToDays(broken, 100f));
            Assert.AreEqual(5, TravelDayRules.ToRoutDays(broken, 100f));
            Assert.AreEqual(3, TravelDayRules.ToDays(null, 100f));
        }
    }
}
