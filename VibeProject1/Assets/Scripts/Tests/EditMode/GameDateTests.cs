using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    public class GameDateTests
    {
        [TestCase(0, 0, 1, 1)]
        [TestCase(29, 0, 1, 30)]
        [TestCase(30, 0, 2, 1)]
        [TestCase(179, 0, 6, 30)]
        [TestCase(180, 1, 1, 1)]
        [TestCase(395, 2, 2, 6)]
        public void TotalDays_ToYearMonthDay(int totalDays, int years, int month, int day)
        {
            var date = new GameDate(totalDays);
            Assert.AreEqual(years, date.ElapsedYears);
            Assert.AreEqual(month, date.Month);
            Assert.AreEqual(day, date.Day);
        }

        [Test]
        public void DisplayString_IsMonthAndDayOnly()
        {
            Assert.AreEqual("1월 1일", new GameDate(0).ToDisplayString());
            Assert.AreEqual("6월 30일", new GameDate(179).ToDisplayString());
            Assert.AreEqual("1월 1일", new GameDate(180).ToDisplayString(), "해가 넘어도 연도는 표시하지 않는다");
        }

        [Test]
        public void NegativeTotalDays_ClampsToStart()
        {
            Assert.AreEqual(0, new GameDate(-5).TotalDays);
        }
    }

    public class GameCalendarTests
    {
        [Test]
        public void StartsAtFirstDay()
        {
            Assert.AreEqual(0, new GameCalendar().Today.TotalDays);
        }

        [Test]
        public void AdvanceDays_AddsAndRaisesChangedOnce()
        {
            var calendar = new GameCalendar();
            var raised = 0;
            calendar.Changed += () => raised++;
            calendar.AdvanceDays(3);
            Assert.AreEqual(3, calendar.Today.TotalDays);
            Assert.AreEqual(1, raised);
        }

        [TestCase(0)]
        [TestCase(-2)]
        public void AdvanceDays_NonPositive_IsIgnored(int days)
        {
            var calendar = new GameCalendar(10);
            var raised = 0;
            calendar.Changed += () => raised++;
            calendar.AdvanceDays(days);
            Assert.AreEqual(10, calendar.Today.TotalDays);
            Assert.AreEqual(0, raised);
        }
    }
}
