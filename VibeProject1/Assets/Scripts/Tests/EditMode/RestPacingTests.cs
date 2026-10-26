using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>휴식 진행 하루당 시간 = max(최소, min(기준, 목표 ÷ 일수))(Docs/설계/103번 §4.2).</summary>
    public class RestPacingTests
    {
        private static readonly RestPacingSettings Settings = new RestPacingSettings();

        [TestCase(1, 3f)]
        [TestCase(6, 3f)]
        [TestCase(7, 20f / 7f)]
        [TestCase(20, 1f)]
        [TestCase(30, 1f)]
        [TestCase(0, 3f)]
        [TestCase(-5, 3f)]
        public void SecondsPerDay(int days, float expected)
        {
            Assert.AreEqual(expected, RestPacing.SecondsPerDay(days, Settings), 0.0001f);
        }

        [Test]
        public void DefaultSettings_MatchSpec()
        {
            Assert.AreEqual(3f, Settings.BaseSecondsPerDay);
            Assert.AreEqual(20f, Settings.TargetTotalSeconds);
            Assert.AreEqual(1f, Settings.MinSecondsPerDay);
        }
    }
}
