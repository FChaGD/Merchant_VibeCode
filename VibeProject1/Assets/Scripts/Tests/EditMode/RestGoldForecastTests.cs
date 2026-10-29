using NUnit.Framework;

namespace Game.Core.Tests
{
    /// <summary>휴식 예상 남은 골드(설계 109 §6.3) - 개인 → 골드 상자 순 지출 기준.</summary>
    public class RestGoldForecastTests
    {
        [Test]
        public void Forecast_PersonalCovers()
        {
            var (personal, loaded, insufficient) = RestGoldForecast.Compute(100, 500, 30, 0, 500);
            Assert.AreEqual(70, personal);
            Assert.AreEqual(500, loaded);
            Assert.IsFalse(insufficient);
        }

        [Test]
        public void Forecast_WithdrawsBoxes()
        {
            var (personal, loaded, insufficient) = RestGoldForecast.Compute(20, 1000, 50, 1, 500);
            Assert.AreEqual(470, personal);
            Assert.AreEqual(500, loaded);
            Assert.IsFalse(insufficient);
        }

        [Test]
        public void Forecast_Insufficient()
        {
            var (personal, _, insufficient) = RestGoldForecast.Compute(20, 0, 50, 0, 500);
            Assert.AreEqual(-30, personal);
            Assert.IsTrue(insufficient);
        }
    }
}
