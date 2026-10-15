using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    public class CaravanAssetDurabilityLedgerTests
    {
        private FakeOwnedCaravanAssets owned;
        private CaravanAssetDurabilityLedger ledger;

        [SetUp]
        public void SetUp()
        {
            owned = new FakeOwnedCaravanAssets();
            owned.Add("Wagon01#1", FormationUnitKind.Wagon);
            ledger = new CaravanAssetDurabilityLedger(owned);
        }

        [TearDown]
        public void TearDown() => ledger.Dispose();

        [Test]
        public void ExistingOwned_StartsAtMax()
        {
            Assert.IsTrue(ledger.TryGet("Wagon01#1", out var current, out var max));
            Assert.AreEqual(200, max);
            Assert.AreEqual(200f, current);
        }

        [Test]
        public void OwnedAddedAfterCreation_Registered()
        {
            owned.Add("Facility01#1", FormationUnitKind.Facility, max: 120);
            Assert.IsTrue(ledger.TryGet("Facility01#1", out var current, out var max));
            Assert.AreEqual(120, max);
            Assert.AreEqual(120f, current);
        }

        [Test]
        public void Removed_RecordDeleted()
        {
            owned.Remove("Wagon01#1");
            Assert.IsFalse(ledger.TryGet("Wagon01#1", out _, out _));
        }

        [Test]
        public void TrySet_ClampsToRange()
        {
            Assert.IsTrue(ledger.TrySet("Wagon01#1", -5f));
            ledger.TryGet("Wagon01#1", out var low, out _);
            Assert.AreEqual(0f, low);
            Assert.IsTrue(ledger.TrySet("Wagon01#1", 500f));
            ledger.TryGet("Wagon01#1", out var high, out _);
            Assert.AreEqual(200f, high);
        }

        [Test]
        public void MaxShrunk_GetClamps()
        {
            ledger.TrySet("Wagon01#1", 180f);
            owned.SetMax("Wagon01#1", 150);
            ledger.TryGet("Wagon01#1", out var current, out var max);
            Assert.AreEqual(150, max);
            Assert.AreEqual(150f, current);
        }

        [Test]
        public void TryRestore_SetsMax()
        {
            ledger.TrySet("Wagon01#1", 30f);
            Assert.IsTrue(ledger.TryRestore("Wagon01#1"));
            ledger.TryGet("Wagon01#1", out var current, out _);
            Assert.AreEqual(200f, current);
        }

        [Test]
        public void UnknownId_False()
        {
            Assert.IsFalse(ledger.TryGet("Missing", out _, out _));
            Assert.IsFalse(ledger.TrySet("Missing", 10f));
            Assert.IsFalse(ledger.TryRestore(null));
        }

        [Test]
        public void Changed_OnValueAndOwnedChange_NotOnSameValue()
        {
            var count = 0;
            ledger.Changed += () => count++;
            ledger.TrySet("Wagon01#1", 100f);
            ledger.TrySet("Wagon01#1", 100f);
            owned.Add("Wagon02#1", FormationUnitKind.Wagon);
            owned.Remove("Wagon02#1");
            Assert.AreEqual(3, count);
        }

        [Test]
        public void Dispose_StopsSync()
        {
            ledger.Dispose();
            owned.Add("Wagon02#1", FormationUnitKind.Wagon);
            Assert.IsFalse(ledger.TryGet("Wagon02#1", out _, out _));
        }
    }
}
