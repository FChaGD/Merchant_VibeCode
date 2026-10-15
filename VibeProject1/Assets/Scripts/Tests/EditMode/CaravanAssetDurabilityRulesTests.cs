using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    public class CaravanAssetDurabilityRulesTests
    {
        [TestCase(0.4f, 1)]
        [TestCase(149.2f, 150)]
        [TestCase(150f, 150)]
        [TestCase(0f, 0)]
        public void Display_RoundsUp(float value, int expected) => Assert.AreEqual(expected, CaravanAssetDurabilityRules.Display(value));

        [Test]
        public void RepairCost_ProportionalWithCoefficient() => Assert.AreEqual(50, CaravanAssetDurabilityRules.RepairCost(1000, 180f, 200, 0.5f));

        [Test]
        public void RepairCost_RoundsUp() => Assert.AreEqual(51, CaravanAssetDurabilityRules.RepairCost(1001, 180f, 200, 0.5f));

        [Test]
        public void RepairCost_Full_Zero() => Assert.AreEqual(0, CaravanAssetDurabilityRules.RepairCost(1000, 200f, 200, 0.5f));

        // 표시가 가득(올림 200)이면 손상으로 보지 않는다 - "200/200"인데 수리비가 붙지 않게.
        [Test]
        public void RepairCost_DisplayFull_Zero()
        {
            Assert.IsFalse(CaravanAssetDurabilityRules.IsDamaged(199.6f, 200));
            Assert.AreEqual(0, CaravanAssetDurabilityRules.RepairCost(1000, 199.6f, 200, 0.5f));
        }

        [Test]
        public void RepairCost_NearlyBroken() => Assert.AreEqual(499, CaravanAssetDurabilityRules.RepairCost(1000, 0.4f, 200, 0.5f));

        [Test]
        public void CoefficientOf_IsUniformHalf() => Assert.AreEqual(0.5f, CaravanAssetDurabilityRules.CoefficientOf(null));
    }
}
