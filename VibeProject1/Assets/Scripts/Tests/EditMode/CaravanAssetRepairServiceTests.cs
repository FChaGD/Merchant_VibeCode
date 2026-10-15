using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    public class CaravanAssetRepairServiceTests
    {
        // 복원만 실패시키는 쓰기 계약 - 환급 경로 확인용.
        private sealed class FailingRestoreWriter : ICaravanAssetDurabilityWriter
        {
            private readonly ICaravanAssetDurabilityWriter inner;
            public FailingRestoreWriter(ICaravanAssetDurabilityWriter inner) => this.inner = inner;
            public bool TrySet(string instanceId, float value) => inner.TrySet(instanceId, value);
            public bool TryRestore(string instanceId) => false;
        }

        private FakeOwnedCaravanAssets owned;
        private CaravanAssetDurabilityLedger ledger;
        private FakeGoldSpender gold;

        [SetUp]
        public void SetUp()
        {
            owned = new FakeOwnedCaravanAssets();
            owned.Add("Wagon01#1", FormationUnitKind.Wagon, price: 1000);
            owned.Add("Facility01#1", FormationUnitKind.Facility, price: 400);
            ledger = new CaravanAssetDurabilityLedger(owned);
            gold = new FakeGoldSpender(1000);
        }

        [TearDown]
        public void TearDown() => ledger.Dispose();

        private CaravanAssetRepairService Service(ICaravanAssetDurabilityWriter writer = null) => new(gold, owned, ledger, writer ?? ledger);

        [Test]
        public void Evaluate_Full_NotDamaged()
        {
            Assert.AreEqual(0, Service().CostOf("Wagon01#1"));
            Assert.AreEqual(CaravanAssetRepairCheck.NotDamaged, Service().Evaluate("Wagon01#1"));
        }

        [Test]
        public void Evaluate_DamagedAffordable_Available()
        {
            ledger.TrySet("Wagon01#1", 100f); // 1000 × 0.5 × 0.5 = 250
            Assert.AreEqual(250, Service().CostOf("Wagon01#1"));
            Assert.AreEqual(CaravanAssetRepairCheck.Available, Service().Evaluate("Wagon01#1"));
        }

        [Test]
        public void Evaluate_Unaffordable_InsufficientFunds()
        {
            ledger.TrySet("Wagon01#1", 100f);
            gold = new FakeGoldSpender(249);
            Assert.AreEqual(CaravanAssetRepairCheck.InsufficientFunds, Service().Evaluate("Wagon01#1"));
        }

        [Test]
        public void TryRepair_SpendsAndRestores()
        {
            ledger.TrySet("Wagon01#1", 100f);
            Assert.IsTrue(Service().TryRepair("Wagon01#1"));
            Assert.AreEqual(750, gold.Gold);
            ledger.TryGet("Wagon01#1", out var current, out _);
            Assert.AreEqual(200f, current);
        }

        [Test]
        public void TryRepair_Insufficient_NoChange()
        {
            ledger.TrySet("Wagon01#1", 100f);
            gold = new FakeGoldSpender(100);
            Assert.IsFalse(Service().TryRepair("Wagon01#1"));
            Assert.AreEqual(100, gold.Gold);
            ledger.TryGet("Wagon01#1", out var current, out _);
            Assert.AreEqual(100f, current);
        }

        [Test]
        public void TryRepair_RestoreFails_Refunds()
        {
            ledger.TrySet("Wagon01#1", 100f);
            Assert.IsFalse(Service(new FailingRestoreWriter(ledger)).TryRepair("Wagon01#1"));
            Assert.AreEqual(1000, gold.Gold);
        }

        [Test]
        public void MaxShrunk_NoRepairCost()
        {
            ledger.TrySet("Wagon01#1", 180f);
            owned.SetMax("Wagon01#1", 150);
            Assert.AreEqual(0, Service().CostOf("Wagon01#1"));
        }

        [Test]
        public void TotalCost_SumOfIndividual()
        {
            ledger.TrySet("Wagon01#1", 100f);    // 250
            ledger.TrySet("Facility01#1", 150f); // 400 × 0.25 × 0.5 = 50
            var service = Service();
            Assert.AreEqual(service.CostOf("Wagon01#1") + service.CostOf("Facility01#1"), service.TotalCost());
            Assert.AreEqual(300, service.TotalCost());
        }

        [Test]
        public void TryRepairAll_Insufficient_RepairsNothing()
        {
            ledger.TrySet("Wagon01#1", 100f);
            ledger.TrySet("Facility01#1", 150f);
            gold = new FakeGoldSpender(299);
            var service = Service();
            Assert.AreEqual(CaravanAssetRepairCheck.InsufficientFunds, service.EvaluateAll());
            Assert.IsFalse(service.TryRepairAll());
            Assert.AreEqual(299, gold.Gold);
            ledger.TryGet("Facility01#1", out var facility, out _);
            Assert.AreEqual(150f, facility);
        }

        [Test]
        public void TryRepairAll_RestoresAll()
        {
            ledger.TrySet("Wagon01#1", 100f);
            ledger.TrySet("Facility01#1", 150f);
            Assert.IsTrue(Service().TryRepairAll());
            Assert.AreEqual(700, gold.Gold);
            ledger.TryGet("Wagon01#1", out var wagon, out _);
            ledger.TryGet("Facility01#1", out var facility, out _);
            Assert.AreEqual(200f, wagon);
            Assert.AreEqual(200f, facility);
        }

        [Test]
        public void EvaluateAll_NothingDamaged_NotDamaged() => Assert.AreEqual(CaravanAssetRepairCheck.NotDamaged, Service().EvaluateAll());
    }
}
