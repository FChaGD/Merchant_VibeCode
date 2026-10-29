using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>숙소 휴식 일수·비용·차단 사유(기획 94 4-1·4-2·4-11, Docs/설계/103번 §4.2).</summary>
    public class InnRestPlanTests
    {
        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(15, 15)]
        [TestCase(30, 30)]
        [TestCase(31, 30)]
        public void ClampDays(int input, int expected)
        {
            Assert.AreEqual(expected, InnRestPlan.ClampDays(input));
        }

        [Test]
        public void TotalCost_DaysTimesCost()
        {
            Assert.AreEqual(70, InnRestPlan.TotalCost(7, 10));
        }

        [Test]
        public void BlockReason_UsesCanAfford()
        {
            var asked = 0;
            Assert.IsNull(InnRestPlan.BlockReason(7, 10, amount => { asked = amount; return true; }));
            Assert.AreEqual(70, asked, "총 비용으로 판정한다");
            Assert.AreEqual(InnRestPlan.InsufficientGoldText, InnRestPlan.BlockReason(7, 10, _ => false));
        }

        [Test]
        public void BlockReason_NoJudge_Blocked() => Assert.AreEqual(InnRestPlan.InsufficientGoldText, InnRestPlan.BlockReason(1, 10, null));
    }
}
