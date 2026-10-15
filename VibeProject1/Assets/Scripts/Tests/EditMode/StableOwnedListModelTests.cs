using System;
using System.Collections.Generic;
using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    public class StableOwnedListModelTests
    {
        private sealed class FakeCatalog : ICaravanAssetCatalogReader
        {
            public IReadOnlyList<CaravanAssetProfile> All => Array.Empty<CaravanAssetProfile>();
            public bool TryGet(string id, out CaravanAssetProfile profile) { profile = null; return false; }
            public string GetKindLabel(FormationUnitKind kind) => kind == FormationUnitKind.Wagon ? "마차" : "시설";
        }

        private FakeOwnedCaravanAssets owned;
        private CaravanAssetDurabilityLedger ledger;

        [SetUp]
        public void SetUp()
        {
            owned = new FakeOwnedCaravanAssets();
            owned.Add("Wagon01#1", FormationUnitKind.Wagon);
            owned.Add("Wagon02#1", FormationUnitKind.Wagon);
            owned.Add("Facility01#1", FormationUnitKind.Facility);
            ledger = new CaravanAssetDurabilityLedger(owned);
        }

        [TearDown]
        public void TearDown() => ledger.Dispose();

        [Test]
        public void Build_HeadersThenAssets_InOwnedOrder()
        {
            var entries = StableOwnedListModel.Build(owned, new FakeCatalog(), ledger);

            Assert.AreEqual(5, entries.Count);
            Assert.IsTrue(entries[0].IsHeader);
            Assert.AreEqual("마차 2", entries[0].Name);
            Assert.AreEqual("Wagon01#1", entries[1].InstanceId);
            Assert.AreEqual("1번 Wagon01#1이름", entries[1].Name);
            Assert.AreEqual("200/200", entries[1].DurabilityText);
            Assert.AreEqual("시설 1", entries[3].Name);
        }

        [Test]
        public void Build_Damaged_RoundedUpAndFlagged()
        {
            ledger.TrySet("Wagon02#1", 149.2f);
            var entry = StableOwnedListModel.Build(owned, new FakeCatalog(), ledger)[2];
            Assert.AreEqual("150/200", entry.DurabilityText);
            Assert.IsTrue(entry.Damaged);
        }

        [Test]
        public void Build_DisplayFull_NotDamaged()
        {
            ledger.TrySet("Wagon02#1", 199.6f);
            Assert.IsFalse(StableOwnedListModel.Build(owned, new FakeCatalog(), ledger)[2].Damaged);
        }

        [Test]
        public void Build_NoDurability_MissingText()
        {
            var entry = StableOwnedListModel.Build(owned, new FakeCatalog(), null)[1];
            Assert.AreEqual("값 없음", entry.DurabilityText);
            Assert.IsFalse(entry.Damaged);
        }
    }
}
