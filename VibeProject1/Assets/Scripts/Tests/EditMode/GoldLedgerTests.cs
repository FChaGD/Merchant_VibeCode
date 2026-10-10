using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    public class GoldLedgerTests
    {
        private static readonly GoldTestItem Gold = new("gold-box", 1, 1);
        private static readonly GoldTestItem Other = new("other", 1, 1);

        private static GoldTestInventory Inventory(params InventorySection[] sections)
        {
            var inventory = new GoldTestInventory(sections);
            inventory.AddDefinition(Gold);
            return inventory;
        }

        private static GoldLedger Ledger(GoldTestWallet wallet, GoldTestInventory inventory) => new(wallet, inventory, "gold-box", 500);

        [Test]
        public void Holdings_ComputeOwnedCapacityExcess()
        {
            var wallet = new GoldTestWallet(700);
            var inventory = Inventory(GoldTestInventory.Section("A", 2, 2));
            inventory.Place(Gold, 0, 0, "A");
            inventory.Place(Other, 1, 0, "A");
            inventory.Stage(Gold);
            var ledger = Ledger(wallet, inventory);

            Assert.AreEqual(700, ledger.PersonalGold);
            Assert.AreEqual(500, ledger.PersonalLimit);
            Assert.AreEqual(1000, ledger.LoadedGold);      // 그리드 1 + 임시 보관 1
            Assert.AreEqual(2, ledger.FreeCells);
            Assert.AreEqual(1000, ledger.FreeSpaceGold);
            Assert.AreEqual(1700, ledger.OwnedGold);
            Assert.AreEqual(2500, ledger.CarryCapacity);   // 500 + 1000 + 1000
            Assert.AreEqual(200, ledger.ExcessGold);
        }

        [Test]
        public void TrySpend_PersonalSufficient_NoWithdrawal()
        {
            var wallet = new GoldTestWallet(1000);
            var inventory = Inventory(GoldTestInventory.Section("A", 2, 1));
            inventory.Place(Gold, 0, 0, "A");
            var ledger = Ledger(wallet, inventory);

            Assert.IsTrue(ledger.TrySpend(300));
            Assert.AreEqual(700, wallet.CurrentAmount);
            Assert.AreEqual(1, inventory.Items.Count);
        }

        [Test]
        public void TrySpend_Shortfall_WithdrawsMinimalBoxes_ChangeStaysPersonal()
        {
            var wallet = new GoldTestWallet(200);
            var inventory = Inventory(GoldTestInventory.Section("A", 2, 1));
            inventory.Place(Gold, 0, 0, "A");
            inventory.Place(Gold, 1, 0, "A");
            var ledger = Ledger(wallet, inventory);

            Assert.IsTrue(ledger.TrySpend(300));    // 부족 100 → 상자 1개
            Assert.AreEqual(400, wallet.CurrentAmount);
            Assert.AreEqual(1, inventory.Items.Count);
        }

        [Test]
        public void TrySpend_Insufficient_ChangesNothing()
        {
            var wallet = new GoldTestWallet(100);
            var inventory = Inventory(GoldTestInventory.Section("A", 2, 1));
            inventory.Place(Gold, 0, 0, "A");
            var ledger = Ledger(wallet, inventory);

            Assert.IsFalse(ledger.CanAfford(601));
            Assert.IsFalse(ledger.TrySpend(601));
            Assert.AreEqual(100, wallet.CurrentAmount);
            Assert.AreEqual(1, inventory.Items.Count);
        }

        [Test]
        public void TrySpend_ZeroOrNegative_SucceedsWithoutChange()
        {
            var wallet = new GoldTestWallet(100);
            var ledger = Ledger(wallet, Inventory(GoldTestInventory.Section("A", 1, 1)));

            Assert.IsTrue(ledger.TrySpend(0));
            Assert.IsTrue(ledger.TrySpend(-10));
            Assert.AreEqual(100, wallet.CurrentAmount);
        }

        [Test]
        public void PreviewWithdrawal_MatchesActualWithdrawal()
        {
            var wallet = new GoldTestWallet(0);
            var inventory = Inventory(GoldTestInventory.Section("A", 2, 1), GoldTestInventory.Section("B", 2, 1));
            inventory.Place(Gold, 0, 0, "A");
            var b = inventory.Place(Gold, 0, 0, "B");
            inventory.Place(Other, 1, 0, "B");     // B 점유율 1.0 > A 0.5
            var ledger = Ledger(wallet, inventory);

            var preview = ledger.PreviewWithdrawal(500);
            CollectionAssert.AreEqual(new[] { b.InstanceId }, preview);

            Assert.IsTrue(ledger.TrySpend(500));
            Assert.IsFalse(inventory.TryGetItemAt("B", new GridPosition(0, 0), out _));
            Assert.IsTrue(inventory.TryGetItemAt("A", new GridPosition(0, 0), out _));
            Assert.AreEqual(0, ledger.PreviewWithdrawal(0).Count);
        }

        [Test]
        public void Refund_AddsToPersonal()
        {
            var wallet = new GoldTestWallet(0);
            var ledger = Ledger(wallet, Inventory(GoldTestInventory.Section("A", 1, 1)));

            ledger.Refund(300);

            Assert.AreEqual(300, wallet.CurrentAmount);
        }

        [Test]
        public void ConvertCounts_UsePersonalAndFreeCells()
        {
            var wallet = new GoldTestWallet(1700);
            var ledger = Ledger(wallet, Inventory(GoldTestInventory.Section("A", 2, 1)));

            Assert.AreEqual(500, ledger.GoldBoxValue);
            Assert.AreEqual(2, ledger.MaxConvertibleBoxes);  // min(3, 빈 칸 2)
            Assert.AreEqual(2, ledger.ExcessBoxes);          // min(⌈1200/500⌉=3, 2)
        }

        [Test]
        public void Convert_PlacesFrontWagonTopLeftFirst_DeductsPersonal()
        {
            var wallet = new GoldTestWallet(1500);
            var inventory = Inventory(GoldTestInventory.Section("A", 1, 1), GoldTestInventory.Section("B", 2, 1));
            inventory.Place(Other, 0, 0, "A");
            var ledger = Ledger(wallet, inventory);

            Assert.AreEqual(2, ledger.Convert(2));
            Assert.AreEqual(500, wallet.CurrentAmount);
            Assert.IsTrue(inventory.TryGetItemAt("B", new GridPosition(0, 0), out var first));
            Assert.AreEqual("gold-box", first.Definition.Id);
            Assert.IsTrue(inventory.TryGetItemAt("B", new GridPosition(1, 0), out var second));
            Assert.AreEqual("gold-box", second.Definition.Id);
        }

        [Test]
        public void Convert_ClampsToMax()
        {
            var wallet = new GoldTestWallet(5000);
            var inventory = Inventory(GoldTestInventory.Section("A", 2, 1));
            var ledger = Ledger(wallet, inventory);

            Assert.AreEqual(2, ledger.Convert(10));
            Assert.AreEqual(4000, wallet.CurrentAmount);
            Assert.AreEqual(0, ledger.Convert(1));
        }

        [Test]
        public void Convert_PlacementFails_RefundsThatBox()
        {
            var wallet = new GoldTestWallet(1000);
            var inventory = Inventory(GoldTestInventory.Section("A", 2, 1));
            inventory.FailNextPlacement = true;
            var ledger = Ledger(wallet, inventory);

            Assert.AreEqual(0, ledger.Convert(1));
            Assert.AreEqual(1000, wallet.CurrentAmount);
        }

        [Test]
        public void DiscardExcess_TrimsPersonalToLimit()
        {
            var wallet = new GoldTestWallet(1200);
            var inventory = Inventory(GoldTestInventory.Section("A", 1, 1));
            inventory.Place(Gold, 0, 0, "A");
            var ledger = Ledger(wallet, inventory);

            Assert.AreEqual(700, ledger.DiscardExcess());
            Assert.AreEqual(500, wallet.CurrentAmount);
            Assert.AreEqual(1, inventory.Items.Count);       // 적재 골드는 그대로
        }

        [Test]
        public void DiscardExcess_AtLimit_NoChange()
        {
            var wallet = new GoldTestWallet(500);
            var ledger = Ledger(wallet, Inventory(GoldTestInventory.Section("A", 1, 1)));

            Assert.AreEqual(0, ledger.ExcessGold);
            Assert.AreEqual(0, ledger.DiscardExcess());
            Assert.AreEqual(500, wallet.CurrentAmount);
        }

        [Test]
        public void Changed_FiresOnWalletAndInventory_StopsAfterDispose()
        {
            var wallet = new GoldTestWallet(100);
            var inventory = Inventory(GoldTestInventory.Section("A", 2, 1));
            var ledger = Ledger(wallet, inventory);
            var raised = 0;
            ledger.Changed += () => raised++;

            wallet.Add(10);
            inventory.Place(Other, 0, 0, "A");
            Assert.AreEqual(2, raised);

            ledger.Dispose();
            wallet.Add(10);
            Assert.AreEqual(2, raised);
        }

        [Test]
        public void WithoutInventory_ActsAsPersonalOnly()
        {
            var wallet = new GoldTestWallet(700);
            var ledger = new GoldLedger(wallet, null, "gold-box", 500);

            Assert.AreEqual(700, ledger.OwnedGold);
            Assert.AreEqual(0, ledger.LoadedGold);
            Assert.AreEqual(0, ledger.FreeCells);
            Assert.AreEqual(500, ledger.CarryCapacity);
            Assert.AreEqual(0, ledger.MaxConvertibleBoxes);
            Assert.AreEqual(0, ledger.PreviewWithdrawal(1000).Count);
            Assert.IsTrue(ledger.TrySpend(700));
            Assert.AreEqual(0, wallet.CurrentAmount);
        }

        [Test]
        public void WithoutGoldDefinition_NoFreeSpaceValueAndNoConversion()
        {
            var wallet = new GoldTestWallet(1500);
            var inventory = new GoldTestInventory(GoldTestInventory.Section("A", 2, 1)); // 카탈로그에 gold-box 없음
            var ledger = Ledger(wallet, inventory);

            Assert.AreEqual(2, ledger.FreeCells);
            Assert.AreEqual(0, ledger.FreeSpaceGold);
            Assert.AreEqual(0, ledger.MaxConvertibleBoxes);
            Assert.AreEqual(0, ledger.Convert(1));
        }
    }
}
