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
        public void RoutExtraRatio_IsMultiplierMinusOne()
        {
            Assert.AreEqual(0.5f, TravelDayRules.RoutExtraRatio(Settings), 1e-5f);
            var low = TripTravelSettings.Default();
            low.RoutDayMultiplier = 0.8f;
            Assert.AreEqual(0f, TravelDayRules.RoutExtraRatio(low), "1 미만이면 0");
        }

        [Test]
        public void NonPositiveSettings_FallBackToDefaults()
        {
            // 기존 씬에 저장된 설정에는 새 필드가 없어 0으로 들어올 수 있다.
            var broken = TripTravelSettings.Default();
            broken.SecondsPerDay = 0f;
            broken.RoutDayMultiplier = -1f;
            Assert.AreEqual(3, TravelDayRules.ToDays(broken, 100f));
            Assert.AreEqual(0.5f, TravelDayRules.RoutExtraRatio(broken), 1e-5f);
            Assert.AreEqual(30f, TravelDayRules.SecondsPerDay(broken));
            Assert.AreEqual(3, TravelDayRules.ToDays(null, 100f));
        }
    }
}
