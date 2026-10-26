using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>마구간 매각 판정·매각가(기획 94 4-19·4-20, Docs/설계/103번 §4.2). 순서: 미보유 → 화물 → 마지막 마차.</summary>
    public class CaravanAssetSaleRulesTests
    {
        [Test]
        public void NotOwned_First()
        {
            Assert.AreEqual(CaravanAssetSaleCheck.NotOwned, CaravanAssetSaleRules.Evaluate(false, true, FormationUnitKind.Wagon, 1));
        }

        [Test]
        public void Cargo_BeforeLastWagon()
        {
            Assert.AreEqual(CaravanAssetSaleCheck.HasCargo, CaravanAssetSaleRules.Evaluate(true, true, FormationUnitKind.Wagon, 1));
        }

        [Test]
        public void LastWagon_Blocked()
        {
            Assert.AreEqual(CaravanAssetSaleCheck.LastWagon, CaravanAssetSaleRules.Evaluate(true, false, FormationUnitKind.Wagon, 1));
        }

        [Test]
        public void Facility_IgnoresCount()
        {
            Assert.AreEqual(CaravanAssetSaleCheck.Available, CaravanAssetSaleRules.Evaluate(true, false, FormationUnitKind.Facility, 0));
        }

        [Test]
        public void SecondWagon_Available()
        {
            Assert.AreEqual(CaravanAssetSaleCheck.Available, CaravanAssetSaleRules.Evaluate(true, false, FormationUnitKind.Wagon, 2));
        }

        [TestCase(1000, 0.5f, 500)]
        [TestCase(999, 0.5f, 499)]
        [TestCase(1, 0.5f, 0)]
        public void SalePrice_RoundsDown(int price, float ratio, int expected)
        {
            Assert.AreEqual(expected, CaravanAssetSaleRules.SalePrice(price, ratio));
        }
    }
}
