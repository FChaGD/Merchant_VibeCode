using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Game.Core;

namespace Game.Core.Tests
{
    public class ShopPurchaseServiceTests
    {
        private class FakeItemDefinition : IInventoryItemDefinition
        {
            public string Id { get; }
            public string DisplayName => Id;
            public string Description => string.Empty;
            public Sprite Icon => null;
            public int FootprintWidth { get; }
            public int FootprintHeight { get; }

            public FakeItemDefinition(string id, int width, int height)
            {
                Id = id;
                FootprintWidth = width;
                FootprintHeight = height;
            }
        }

        // 그리드만 가진 최소 저장소 - 구매 서비스는 IInventoryRepository 계약만 쓴다.
        private class GridRepository : IInventoryRepository
        {
            private readonly InventoryGrid grid;
            public bool FailNextPlacement { get; set; }

            public GridRepository(int width, int height) => grid = new InventoryGrid(width, height);

            public GridRepository(InventoryGrid grid) => this.grid = grid;

            public IReadOnlyList<InventorySection> Sections => grid.Sections;
            public IReadOnlyCollection<InventoryItemInstance> Items => grid.Items;
            public event System.Action OnChanged;
            public bool TryGetItemAt(string sectionId, GridPosition position, out InventoryItemInstance item) => grid.TryGetAt(sectionId, position, out item);
            public bool TryGetItemAt(GridPosition position, out InventoryItemInstance item) => grid.TryGetAt(null, position, out item);

            public bool TryPlaceItem(IInventoryItemDefinition definition, GridPosition position, out InventoryItemInstance placed, int quarterTurns = 0, string sectionId = null)
            {
                if (FailNextPlacement)
                {
                    placed = default;
                    return false;
                }

                if (!grid.TryPlace(definition, position, out placed, quarterTurns, sectionId)) return false;
                OnChanged?.Invoke();
                return true;
            }

            public bool RemoveItem(string instanceId) => grid.Remove(instanceId);
        }

        private GameObject gameObject;
        private InMemoryPlayerCurrencyWallet wallet;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject(nameof(ShopPurchaseServiceTests));
            wallet = gameObject.AddComponent<InMemoryPlayerCurrencyWallet>();
            wallet.ResolveDependencies(null); // 기본 소지 재화 상한만큼 가득 찬 상태로 시작
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void TryPurchase_Success_DeductsAndPlacesAtTopLeft()
        {
            var inventory = new GridRepository(4, 2);
            var service = new ShopPurchaseService(wallet, inventory, allowRotation: true);
            var entry = new ShopStockEntry(new FakeItemDefinition("a", 2, 1), 300);
            var startingAmount = wallet.CurrentAmount;

            Assert.IsTrue(service.TryPurchase(entry, null, out _));
            Assert.AreEqual(startingAmount - 300, wallet.CurrentAmount);
            Assert.IsTrue(inventory.TryGetItemAt(new GridPosition(0, 0), out var placed));
            Assert.AreEqual("a", placed.Definition.Id);
        }

        [Test]
        public void Evaluate_InsufficientFunds()
        {
            var service = new ShopPurchaseService(wallet, new GridRepository(4, 2), allowRotation: true);
            var entry = new ShopStockEntry(new FakeItemDefinition("a", 1, 1), wallet.CurrentAmount + 1);

            Assert.AreEqual(ShopPurchaseCheck.InsufficientFunds, service.Evaluate(entry));
            Assert.IsFalse(service.TryPurchase(entry, null, out _));
            Assert.AreEqual(wallet.Capacity, wallet.CurrentAmount);
        }

        [Test]
        public void Evaluate_NoSpace()
        {
            var inventory = new GridRepository(1, 1);
            inventory.TryPlaceItem(new FakeItemDefinition("filler", 1, 1), new GridPosition(0, 0), out _);
            var service = new ShopPurchaseService(wallet, inventory, allowRotation: true);
            var entry = new ShopStockEntry(new FakeItemDefinition("a", 1, 1), 100);

            Assert.AreEqual(ShopPurchaseCheck.NoSpace, service.Evaluate(entry));
            Assert.IsFalse(service.TryPurchase(entry, null, out _));
            Assert.AreEqual(wallet.Capacity, wallet.CurrentAmount);
        }

        [Test]
        public void Evaluate_BothFail_ReportsInsufficientFundsFirst()
        {
            var inventory = new GridRepository(1, 1);
            inventory.TryPlaceItem(new FakeItemDefinition("filler", 1, 1), new GridPosition(0, 0), out _);
            var service = new ShopPurchaseService(wallet, inventory, allowRotation: true);
            var entry = new ShopStockEntry(new FakeItemDefinition("a", 1, 1), wallet.CurrentAmount + 1);

            Assert.AreEqual(ShopPurchaseCheck.InsufficientFunds, service.Evaluate(entry));
        }

        [Test]
        public void TryPurchase_FitsOnlyRotated_PlacesRotated()
        {
            var inventory = new GridRepository(1, 2); // 세로 2칸 - 2×1 아이템은 회전해야 들어간다
            var service = new ShopPurchaseService(wallet, inventory, allowRotation: true);
            var entry = new ShopStockEntry(new FakeItemDefinition("long", 2, 1), 100);

            Assert.IsTrue(service.TryPurchase(entry, null, out _));
            Assert.IsTrue(inventory.TryGetItemAt(new GridPosition(0, 1), out var placed));
            Assert.AreEqual(1, placed.QuarterTurns);
        }

        [Test]
        public void Evaluate_RotationNotAllowed_NoSpace()
        {
            var service = new ShopPurchaseService(wallet, new GridRepository(1, 2), allowRotation: false);
            var entry = new ShopStockEntry(new FakeItemDefinition("long", 2, 1), 100);

            Assert.AreEqual(ShopPurchaseCheck.NoSpace, service.Evaluate(entry));
        }

        [Test]
        public void TryPurchase_PlacementFails_RefundsWallet()
        {
            var inventory = new GridRepository(2, 2) { FailNextPlacement = true };
            var service = new ShopPurchaseService(wallet, inventory, allowRotation: true);
            var entry = new ShopStockEntry(new FakeItemDefinition("a", 1, 1), 100);

            Assert.IsFalse(service.TryPurchase(entry, null, out _));
            Assert.AreEqual(wallet.Capacity, wallet.CurrentAmount);
        }

        [Test]
        public void TryFindSlot_IgnoresStagedItemsAndPrefersTopRowLeft()
        {
            var placed = new List<InventoryItemInstance>
            {
                new("x", new FakeItemDefinition("x", 1, 1), new GridPosition(0, 0), 0, false, InventoryGrid.DefaultSectionId),
            };
            var sections = new[] { new InventorySection(InventoryGrid.DefaultSectionId, string.Empty, InventoryShape.Rectangle(3, 2)) };

            Assert.IsTrue(InventoryAutoSorter.TryFindSlot(sections, placed, new FakeItemDefinition("a", 1, 1), allowRotation: true, out _, out var position, out var turns));
            Assert.AreEqual(new GridPosition(1, 0), position);
            Assert.AreEqual(0, turns);
        }

        [Test]
        public void TryPurchase_PrefersGivenSection_AndReportsPlacedSection()
        {
            var grid = new InventoryGrid();
            grid.AddSection(new InventorySection("a", string.Empty, InventoryShape.Rectangle(1, 1)));
            grid.AddSection(new InventorySection("b", string.Empty, InventoryShape.Rectangle(1, 1)));
            var inventory = new GridRepository(grid);
            var service = new ShopPurchaseService(wallet, inventory, allowRotation: true);
            var entry = new ShopStockEntry(new FakeItemDefinition("item", 1, 1), 100);

            Assert.IsTrue(service.TryPurchase(entry, "b", out var firstSection));
            Assert.AreEqual("b", firstSection);
            Assert.IsTrue(service.TryPurchase(entry, "b", out var secondSection));
            Assert.AreEqual("a", secondSection, "우선 섹션이 차면 다음 섹션에 넣는다(기획 63번 §3.5).");
            Assert.AreEqual(ShopPurchaseCheck.NoSpace, service.Evaluate(entry));
        }
    }
}
