using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    public class TownScaleFacilityRuleTests
    {
        // 기획 57번 §4.3 확정 분포: 4 대도시, 2·6·7·10 도시, 나머지 촌락. 11번은 규모 미지정, 12번은 정의 안 된 규모.
        private static readonly Dictionary<int, string> ScaleByCityId = new()
        {
            [1] = TownScaleDefaults.Village,
            [2] = TownScaleDefaults.City,
            [4] = TownScaleDefaults.Metropolis,
            [11] = "",
            [12] = "Unknown",
        };

        private readonly List<string> warnings = new();

        private TownScaleFacilityRule CreateRule(IEnumerable<(string Id, string Label)> scales = null, IReadOnlyDictionary<string, string> minScales = null)
        {
            warnings.Clear();
            return new TownScaleFacilityRule(scales ?? TownScaleDefaults.Scales, minScales ?? TownScaleDefaults.FacilityMinScale, ScaleByCityId, warnings.Add);
        }

        private static int CountAvailable(TownScaleFacilityRule rule, int cityId)
            => TownFacilityCatalog.AllFacilityIds.Count(id => rule.IsFacilityAvailable(cityId, id));

        [Test]
        public void Defaults_CoverEveryCatalogFacility()
        {
            CollectionAssert.AreEquivalent(TownFacilityCatalog.AllFacilityIds, TownScaleDefaults.FacilityMinScale.Keys);
        }

        [Test]
        public void FacilityCount_Village3_City8_Metropolis11()
        {
            var rule = CreateRule();

            Assert.AreEqual(3, CountAvailable(rule, 1));
            Assert.AreEqual(8, CountAvailable(rule, 2));
            Assert.AreEqual(11, CountAvailable(rule, 4));
        }

        [Test]
        public void HigherScale_IncludesAllLowerScaleFacilities()
        {
            var rule = CreateRule();

            foreach (var facilityId in TownFacilityCatalog.AllFacilityIds)
            {
                if (rule.IsFacilityAvailable(1, facilityId)) Assert.IsTrue(rule.IsFacilityAvailable(2, facilityId), facilityId);
                if (rule.IsFacilityAvailable(2, facilityId)) Assert.IsTrue(rule.IsFacilityAvailable(4, facilityId), facilityId);
            }
        }

        [Test]
        public void MissingOrUnknownScale_TreatedAsSmallestWithWarning()
        {
            var rule = CreateRule();

            Assert.AreEqual(TownScaleDefaults.Village, rule.GetScale(11).Id);
            Assert.AreEqual(TownScaleDefaults.Village, rule.GetScale(12).Id);
            Assert.AreEqual(2, warnings.Count);
            // 도시 자산에 없는 도시(현재 위치 미지정 등)는 경고 없이 가장 작은 규모.
            Assert.AreEqual(TownScaleDefaults.Village, rule.GetScale(999).Id);
            Assert.AreEqual(2, warnings.Count);
        }

        [Test]
        public void FacilityWithoutMinScale_AlwaysAvailableWithWarning()
        {
            var rule = CreateRule(minScales: new Dictionary<string, string>());

            Assert.IsTrue(rule.IsFacilityAvailable(1, TownFacilityIds.CityHall));
            Assert.AreEqual(1, warnings.Count);
        }

        [Test]
        public void InsertedScale_OrdersBetweenNeighbours()
        {
            // 규모 추가(확장성, 기획 57번 §3): 촌락과 도시 사이에 소도시를 끼운다. 소도시에서 처음 열리는 시설을 하나 지정한다.
            var scales = new[] { (TownScaleDefaults.Village, "촌락"), ("Town", "소도시"), (TownScaleDefaults.City, "도시"), (TownScaleDefaults.Metropolis, "대도시") };
            var minScales = new Dictionary<string, string>(TownScaleDefaults.FacilityMinScale) { [TownFacilityIds.Tavern] = "Town" };
            var scaleByCityId = new Dictionary<int, string>(ScaleByCityId) { [20] = "Town" };
            var rule = new TownScaleFacilityRule(scales, minScales, scaleByCityId, warnings.Add);

            Assert.AreEqual(1, rule.GetScale(20).Rank);
            Assert.IsTrue(rule.IsFacilityAvailable(20, TownFacilityIds.Tavern));
            Assert.IsFalse(rule.IsFacilityAvailable(20, TownFacilityIds.Stable));
            Assert.IsFalse(rule.IsFacilityAvailable(1, TownFacilityIds.Tavern));
            Assert.IsTrue(rule.IsFacilityAvailable(2, TownFacilityIds.Tavern));
        }
    }
}
