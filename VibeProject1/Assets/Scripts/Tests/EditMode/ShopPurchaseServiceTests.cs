using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Game.Core;

namespace Game.Core.Tests
{
    public class ShopPurchaseServiceTests
    {
        private class FakeStock : ITownStockConsumer
        {
            public readonly List<(int city, TownStockCategory category, string id)> Consumed = new();
            public bool TryConsume(int cityId, TownStockCategory category, string itemId)
            {
                Consumed.Add((cityId, category, itemId));
                return true;
            }
        }

        private GameObject gameObject;
        private InMemoryPlayerCurrencyWallet wallet;
        private FakeStock stock;
        private GoldLedger gold;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject(nameof(ShopPurchaseServiceTests));
            wallet = gameObject.AddComponent<InMemoryPlayerCurrencyWallet>();
            wallet.ResolveDependencies(null); // 시작 금액 10,000으로 시작
            stock = new FakeStock();
            gold = new GoldLedger(wallet, null, "gold-box", 500);
        }

        [TearDown]
        public void TearDown()
        {
            gold.Dispose();
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void TryPurchase_Success_DeductsAndPlacesAtTopLeft()
        {
            var inventory = new GoldTestInventory(4, 2);
            var service = new ShopPurchaseService(gold, inventory, inventory, allowRotation: true, stock);
            var entry = new ShopStockEntry(new GoldTestItem("a", 2, 1), 300, 5);
            var startingAmount = wallet.CurrentAmount;

            Assert.IsTrue(service.TryPurchase(entry, 4, null, out _));
            Assert.AreEqual(startingAmount - 300, wallet.CurrentAmount);
            Assert.IsTrue(inventory.TryGetItemAt(new GridPosition(0, 0), out var placed));
            Assert.AreEqual("a", placed.Definition.Id);
        }

        [Test]
        public void Evaluate_InsufficientFunds()
        {
            var inventory = new GoldTestInventory(4, 2);
            var service = new ShopPurchaseService(gold, inventory, inventory, allowRotation: true, stock);
            var entry = new ShopStockEntry(new GoldTestItem("a", 1, 1), wallet.CurrentAmount + 1, 5);

            Assert.AreEqual(ShopPurchaseCheck.InsufficientFunds, service.Evaluate(entry));
            Assert.IsFalse(service.TryPurchase(entry, 4, null, out _));
            Assert.AreEqual(10000, wallet.CurrentAmount);
        }

        [Test]
        public void Evaluate_NoSpace()
        {
            var inventory = new GoldTestInventory(1, 1);
            inventory.TryPlaceItem(new GoldTestItem("filler", 1, 1), new GridPosition(0, 0), out _);
            var service = new ShopPurchaseService(gold, inventory, inventory, allowRotation: true, stock);
            var entry = new ShopStockEntry(new GoldTestItem("a", 1, 1), 100, 5);

            Assert.AreEqual(ShopPurchaseCheck.NoSpace, service.Evaluate(entry));
            Assert.IsFalse(service.TryPurchase(entry, 4, null, out _));
            Assert.AreEqual(10000, wallet.CurrentAmount);
        }

        [Test]
        public void Evaluate_BothFail_ReportsInsufficientFundsFirst()
        {
            var inventory = new GoldTestInventory(1, 1);
            inventory.TryPlaceItem(new GoldTestItem("filler", 1, 1), new GridPosition(0, 0), out _);
            var service = new ShopPurchaseService(gold, inventory, inventory, allowRotation: true, stock);
            var entry = new ShopStockEntry(new GoldTestItem("a", 1, 1), wallet.CurrentAmount + 1, 5);

            Assert.AreEqual(ShopPurchaseCheck.InsufficientFunds, service.Evaluate(entry));
        }

        [Test]
        public void TryPurchase_FitsOnlyRotated_PlacesRotated()
        {
            var inventory = new GoldTestInventory(1, 2); // 세로 2칸 - 2×1 아이템은 회전해야 들어간다
            var service = new ShopPurchaseService(gold, inventory, inventory, allowRotation: true, stock);
            var entry = new ShopStockEntry(new GoldTestItem("long", 2, 1), 100, 5);

            Assert.IsTrue(service.TryPurchase(entry, 4, null, out _));
            Assert.IsTrue(inventory.TryGetItemAt(new GridPosition(0, 1), out var placed));
            Assert.AreEqual(1, placed.QuarterTurns);
        }

        [Test]
        public void Evaluate_RotationNotAllowed_NoSpace()
        {
            var inventory = new GoldTestInventory(1, 2);
            var service = new ShopPurchaseService(gold, inventory, inventory, allowRotation: false, stock);
            var entry = new ShopStockEntry(new GoldTestItem("long", 2, 1), 100, 5);

            Assert.AreEqual(ShopPurchaseCheck.NoSpace, service.Evaluate(entry));
        }

        [Test]
        public void TryPurchase_PlacementFails_RefundsWallet()
        {
            var inventory = new GoldTestInventory(2, 2) { FailNextPlacement = true };
            var service = new ShopPurchaseService(gold, inventory, inventory, allowRotation: true, stock);
            var entry = new ShopStockEntry(new GoldTestItem("a", 1, 1), 100, 5);

            Assert.IsFalse(service.TryPurchase(entry, 4, null, out _));
            Assert.AreEqual(10000, wallet.CurrentAmount);
        }

        [Test]
        public void TryFindSlot_IgnoresStagedItemsAndPrefersTopRowLeft()
        {
            var placed = new List<InventoryItemInstance>
            {
                new("x", new GoldTestItem("x", 1, 1), new GridPosition(0, 0), 0, false, InventoryGrid.DefaultSectionId),
            };
            var sections = new[] { new InventorySection(InventoryGrid.DefaultSectionId, string.Empty, InventoryShape.Rectangle(3, 2)) };

            Assert.IsTrue(InventoryAutoSorter.TryFindSlot(sections, placed, new GoldTestItem("a", 1, 1), allowRotation: true, out _, out var position, out var turns));
            Assert.AreEqual(new GridPosition(1, 0), position);
            Assert.AreEqual(0, turns);
        }

        [Test]
        public void TryPurchase_PrefersGivenSection_AndReportsPlacedSection()
        {
            var inventory = new GoldTestInventory(new InventorySection("a", string.Empty, InventoryShape.Rectangle(1, 1)), new InventorySection("b", string.Empty, InventoryShape.Rectangle(1, 1)));
            var service = new ShopPurchaseService(gold, inventory, inventory, allowRotation: true, stock);
            var entry = new ShopStockEntry(new GoldTestItem("item", 1, 1), 100, 5);

            Assert.IsTrue(service.TryPurchase(entry, 4, "b", out var firstSection));
            Assert.AreEqual("b", firstSection);
            Assert.IsTrue(service.TryPurchase(entry, 4, "b", out var secondSection));
            Assert.AreEqual("a", secondSection, "우선 섹션이 차면 다음 섹션에 넣는다(기획 63번 §3.5).");
            Assert.AreEqual(ShopPurchaseCheck.NoSpace, service.Evaluate(entry));
        }

        [Test]
        public void Evaluate_SoldOut_TakesPriorityOverFunds()
        {
            wallet.TrySpend(wallet.CurrentAmount);
            var inventory = new GoldTestInventory(4, 4);
            var service = new ShopPurchaseService(gold, inventory, inventory, allowRotation: false, stock);

            Assert.AreEqual(ShopPurchaseCheck.SoldOut, service.Evaluate(new ShopStockEntry(new GoldTestItem("a", 1, 1), 100, 0)));
        }

        [Test]
        public void TryPurchase_Success_ConsumesTradeGoodsStockOfCity()
        {
            var inventory = new GoldTestInventory(4, 4);
            var service = new ShopPurchaseService(gold, inventory, inventory, allowRotation: false, stock);

            Assert.IsTrue(service.TryPurchase(new ShopStockEntry(new GoldTestItem("a", 1, 1), 100, 1), 7, null, out _));

            CollectionAssert.AreEqual(new[] { (7, TownStockCategory.TradeGoods, "a") }, stock.Consumed);
        }

        [Test]
        public void TryPurchase_PlacementFails_DoesNotConsumeStock()
        {
            var repo = new GoldTestInventory(4, 4) { FailNextPlacement = true };
            var service = new ShopPurchaseService(gold, repo, repo, allowRotation: false, stock);

            Assert.IsFalse(service.TryPurchase(new ShopStockEntry(new GoldTestItem("a", 1, 1), 100, 1), 7, null, out _));
            Assert.AreEqual(0, stock.Consumed.Count);
        }

        private static readonly GoldTestItem GoldBox = new("gold-box", 1, 1);

        // 인벤토리를 아는 장부로 지출하는 서비스 - 적재 골드 인출이 구매 판정·확정에 반영되는지 본다(설계 83번 §5.2).
        private ShopPurchaseService ServiceWithCargo(GoldTestWallet cargoWallet, GoldTestInventory inventory, out GoldLedger cargoGold)
        {
            inventory.AddDefinition(GoldBox);
            cargoGold = new GoldLedger(cargoWallet, inventory, "gold-box", 500);
            return new ShopPurchaseService(cargoGold, inventory, inventory, allowRotation: true, stock);
        }

        [Test]
        public void Evaluate_AffordableOnlyWithLoadedGold_Available_AndWithdraws()
        {
            var cargoWallet = new GoldTestWallet(0);
            var inventory = new GoldTestInventory(3, 1);
            inventory.Place(GoldBox, 0, 0);
            var service = ServiceWithCargo(cargoWallet, inventory, out _);
            var entry = new ShopStockEntry(new GoldTestItem("a", 1, 1), 300, 5);

            Assert.AreEqual(ShopPurchaseCheck.Available, service.Evaluate(entry));
            Assert.IsTrue(service.TryPurchase(entry, 4, null, out _));
            Assert.AreEqual(200, cargoWallet.CurrentAmount);      // 상자 1개 인출 500 - 300
            Assert.AreEqual(1, inventory.Items.Count);            // 상자는 빠지고 구매품만
        }

        [Test]
        public void TryPurchase_FullOfGoldBoxes_UsesWithdrawnSlot()
        {
            var cargoWallet = new GoldTestWallet(0);
            var inventory = new GoldTestInventory(1, 1);
            inventory.Place(GoldBox, 0, 0);
            var service = ServiceWithCargo(cargoWallet, inventory, out _);
            var entry = new ShopStockEntry(new GoldTestItem("a", 1, 1), 500, 5);

            Assert.AreEqual(ShopPurchaseCheck.Available, service.Evaluate(entry));
            Assert.IsTrue(service.TryPurchase(entry, 4, null, out _));
            Assert.IsTrue(inventory.TryGetItemAt(new GridPosition(0, 0), out var placed));
            Assert.AreEqual("a", placed.Definition.Id);
            Assert.AreEqual(0, cargoWallet.CurrentAmount);
        }

        [Test]
        public void TryPurchase_RelocatesBlockingItems()
        {
            // A 2×1이 p·q로 꽉 참, B 2×1은 r이 반만 차지, C 1×1은 s가 차지 → 2×1을 바로 놓을 자리도, p·q를 옮길 두 칸도 없다.
            var cargoWallet = new GoldTestWallet(1000);
            var inventory = new GoldTestInventory(GoldTestInventory.Section("A", 2, 1), GoldTestInventory.Section("B", 2, 1), GoldTestInventory.Section("C", 1, 1));
            var p = inventory.Place(new GoldTestItem("p", 1, 1), 0, 0, "A");
            var q = inventory.Place(new GoldTestItem("q", 1, 1), 1, 0, "A");
            inventory.Place(new GoldTestItem("r", 1, 1), 0, 0, "B");
            var s = inventory.Place(new GoldTestItem("s", 1, 1), 0, 0, "C");
            var service = ServiceWithCargo(cargoWallet, inventory, out _);
            var entry = new ShopStockEntry(new GoldTestItem("wide", 2, 1), 100, 5);

            Assert.AreEqual(ShopPurchaseCheck.NoSpace, service.Evaluate(entry));

            // C를 비우면 빈 칸이 B(1,0)·C(0,0) 두 곳 - 바로 놓을 2×1 자리는 여전히 없고, 보고 있는 마차 A에서 p·q를 옮기면 들어간다.
            inventory.RemoveItem(s.InstanceId);
            Assert.AreEqual(ShopPurchaseCheck.Available, service.Evaluate(entry));
            Assert.IsTrue(service.TryPurchase(entry, 4, "A", out var placedSection));
            Assert.AreEqual("A", placedSection);
            Assert.IsTrue(inventory.TryGetItemAt("A", new GridPosition(0, 0), out var bought));
            Assert.AreEqual("wide", bought.Definition.Id);
            var movedSections = inventory.Items.Where(item => item.InstanceId == p.InstanceId || item.InstanceId == q.InstanceId).Select(item => item.SectionId);
            CollectionAssert.AreEquivalent(new[] { "B", "C" }, movedSections);
            Assert.AreEqual(900, cargoWallet.CurrentAmount);
        }

        [Test]
        public void Evaluate_OwnedGoldInsufficient_EvenWithBoxes()
        {
            var cargoWallet = new GoldTestWallet(100);
            var inventory = new GoldTestInventory(2, 1);
            inventory.Place(GoldBox, 0, 0);
            var service = ServiceWithCargo(cargoWallet, inventory, out _);

            Assert.AreEqual(ShopPurchaseCheck.InsufficientFunds, service.Evaluate(new ShopStockEntry(new GoldTestItem("a", 1, 1), 601, 5)));
        }
    }
}
