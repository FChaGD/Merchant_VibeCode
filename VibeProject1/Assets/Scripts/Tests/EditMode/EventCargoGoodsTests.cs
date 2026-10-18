using System;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class EventCargoGoodsTests
    {
        private static readonly GoldTestItem Wheat = new("wheat", 1, 1);
        private static readonly GoldTestItem Salt = new("salt", 1, 1);
        private static readonly GoldTestItem Gold = new("gold-box", 1, 1);

        private static GoldTestInventory Inventory(int width, int height)
        {
            var inventory = new GoldTestInventory(GoldTestInventory.Section("A", width, height));
            inventory.AddDefinition(Wheat);
            inventory.AddDefinition(Salt);
            inventory.AddDefinition(Gold);
            return inventory;
        }

        private static EventCargoGoods Goods(GoldTestInventory inventory, Func<int, int> randomIndex = null)
            => new(inventory, new CargoTestSettlement(inventory), id => id == "gold-box", randomIndex ?? (n => 0));

        [Test]
        public void Add_FitsGrid_ThenStagesOverflow()
        {
            var inventory = Inventory(1, 1);
            Assert.IsTrue(Goods(inventory).TryAdd("wheat", 3, out var name, out var staged));
            Assert.AreEqual("wheat", name);
            Assert.AreEqual(2, staged);
            Assert.AreEqual(1, inventory.Items.Count);
            Assert.AreEqual(2, inventory.StagedItems.Count);
        }

        [Test]
        public void Add_UnknownId_Fails()
        {
            Assert.IsFalse(Goods(Inventory(1, 1)).TryAdd("ghost", 1, out _, out _));
        }

        [Test]
        public void Remove_StagedFirst_ThenPlaced_UpToOwned()
        {
            var inventory = Inventory(2, 1);
            inventory.Place(Wheat, 0, 0, "A");
            inventory.Stage(Wheat);
            Assert.IsTrue(Goods(inventory).TryRemove("wheat", 1, out _, out var removed));
            Assert.AreEqual(1, removed);
            Assert.AreEqual(0, inventory.StagedItems.Count, "임시 보관부터 잃는다");
            Assert.AreEqual(1, inventory.Items.Count);

            Assert.IsTrue(Goods(inventory).TryRemove("wheat", 5, out _, out removed));
            Assert.AreEqual(1, removed, "가진 만큼만");
            Assert.AreEqual(0, inventory.Items.Count);
        }

        [Test]
        public void Remove_PlacedOrder_TopRowLeftFirst()
        {
            var inventory = Inventory(2, 2);
            inventory.Place(Wheat, 1, 1, "A");
            var first = inventory.Place(Wheat, 1, 0, "A");
            Goods(inventory).TryRemove("wheat", 1, out _, out _);
            foreach (var item in inventory.Items) Assert.AreNotEqual(first.InstanceId, item.InstanceId, "위 행이 먼저");
        }

        [Test]
        public void Remove_NoneOwned_ReturnsZero()
        {
            Assert.IsTrue(Goods(Inventory(1, 1)).TryRemove("wheat", 1, out var name, out var removed));
            Assert.AreEqual(0, removed);
            Assert.AreEqual("wheat", name);
        }

        [Test]
        public void RemoveRandom_ExcludesGold_GroupsByName()
        {
            var inventory = Inventory(3, 1);
            inventory.Place(Gold, 0, 0, "A");
            inventory.Place(Wheat, 1, 0, "A");
            inventory.Place(Salt, 2, 0, "A");
            var losses = Goods(inventory).RemoveRandom(5);
            Assert.AreEqual(2, losses.Count);
            Assert.AreEqual(1, inventory.Items.Count, "골드 상자만 남는다");
        }

        [Test]
        public void RemoveRandom_NothingToLose_Empty()
        {
            var inventory = Inventory(1, 1);
            inventory.Place(Gold, 0, 0, "A");
            CollectionAssert.IsEmpty(Goods(inventory).RemoveRandom(2));
        }

        [Test]
        public void CountOf_PlacedPlusStaged()
        {
            var inventory = Inventory(1, 1);
            inventory.Place(Wheat, 0, 0, "A");
            inventory.Stage(Wheat);
            Assert.AreEqual(2, Goods(inventory).CountOf("wheat"));
        }
    }
}
