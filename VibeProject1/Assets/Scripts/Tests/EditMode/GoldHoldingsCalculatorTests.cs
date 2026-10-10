using System.Collections.Generic;
using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    public class GoldHoldingsCalculatorTests
    {
        private static readonly GoldTestItem Gold = new("gold-box", 1, 1);
        private static readonly GoldTestItem Other = new("other", 1, 1);

        [Test]
        public void CountFreeCells_ExcludesStagedAndBlockedCells()
        {
            Assert.IsTrue(InventoryShape.TryParse("110/111", out var shape, out _));
            var inventory = new GoldTestInventory(new InventorySection("A", "A", shape));
            inventory.Place(Other, 0, 0, "A");
            inventory.Stage(Other);

            // 사용 가능 5칸 - 놓인 1칸 = 4. 임시 보관은 칸을 차지하지 않는다.
            Assert.AreEqual(4, GoldHoldingsCalculator.CountFreeCells(inventory.Sections, inventory.Items));
        }

        [Test]
        public void CountItems_CountsPlacedAndStaged()
        {
            var inventory = new GoldTestInventory(2, 2);
            inventory.Place(Gold, 0, 0);
            inventory.Place(Other, 1, 0);
            inventory.Stage(Gold);

            Assert.AreEqual(2, GoldHoldingsCalculator.CountItems(inventory.Items, inventory.StagedItems, "gold-box"));
        }

        [Test]
        public void SelectWithdrawal_StagedFirst_ThenHighestOccupancy_TieBackWagon_BottomRightFirst()
        {
            var inventory = new GoldTestInventory(GoldTestInventory.Section("A", 2, 1), GoldTestInventory.Section("B", 2, 1), GoldTestInventory.Section("C", 2, 1));
            var a = inventory.Place(Gold, 0, 0, "A");          // A 점유율 0.5
            var b0 = inventory.Place(Gold, 0, 0, "B");         // B 점유율 1.0
            var b1 = inventory.Place(Gold, 1, 0, "B");
            var c = inventory.Place(Gold, 0, 0, "C");          // C 점유율 0.5 - A와 동률이면 뒤쪽(C) 먼저
            var staged = inventory.Stage(Gold);

            var selected = GoldHoldingsCalculator.SelectWithdrawal(inventory.Sections, inventory.Items, inventory.StagedItems, "gold-box", 5);

            CollectionAssert.AreEqual(new[] { staged.InstanceId, b1.InstanceId, b0.InstanceId, c.InstanceId, a.InstanceId }, selected);
        }

        [Test]
        public void SelectWithdrawal_OccupancyComputedOnce()
        {
            // A 2×1에 상자 2개(1.0), B 4×1에 상자 3개(0.75). 한 번만 계산하면 A를 다 비운 뒤 B로 넘어간다.
            var inventory = new GoldTestInventory(GoldTestInventory.Section("A", 2, 1), GoldTestInventory.Section("B", 4, 1));
            var a0 = inventory.Place(Gold, 0, 0, "A");
            var a1 = inventory.Place(Gold, 1, 0, "A");
            inventory.Place(Gold, 0, 0, "B");
            inventory.Place(Gold, 1, 0, "B");
            var b2 = inventory.Place(Gold, 2, 0, "B");

            var selected = GoldHoldingsCalculator.SelectWithdrawal(inventory.Sections, inventory.Items, inventory.StagedItems, "gold-box", 3);

            CollectionAssert.AreEqual(new[] { a1.InstanceId, a0.InstanceId, b2.InstanceId }, selected);
        }

        [Test]
        public void SelectWithdrawal_LowerRowBeforeUpperRow()
        {
            var inventory = new GoldTestInventory(2, 2);
            var top = inventory.Place(Gold, 1, 0);
            var bottom = inventory.Place(Gold, 0, 1);

            var selected = GoldHoldingsCalculator.SelectWithdrawal(inventory.Sections, inventory.Items, inventory.StagedItems, "gold-box", 1);

            CollectionAssert.AreEqual(new[] { bottom.InstanceId }, selected);
            Assert.AreNotEqual(top.InstanceId, selected[0]);
        }

        [Test]
        public void SelectWithdrawal_NotEnoughBoxes_ReturnsAllAvailable()
        {
            var inventory = new GoldTestInventory(2, 1);
            inventory.Place(Gold, 0, 0);

            Assert.AreEqual(1, GoldHoldingsCalculator.SelectWithdrawal(inventory.Sections, inventory.Items, inventory.StagedItems, "gold-box", 3).Count);
            Assert.AreEqual(0, GoldHoldingsCalculator.SelectWithdrawal(inventory.Sections, inventory.Items, inventory.StagedItems, "gold-box", 0).Count);
        }

        [TestCase(1700, 2, 2)]
        [TestCase(1700, 10, 3)]
        [TestCase(400, 10, 0)]
        [TestCase(1700, 0, 0)]
        public void MaxConvertible(int personal, int freeCells, int expected)
        {
            Assert.AreEqual(expected, GoldHoldingsCalculator.MaxConvertible(personal, 500, freeCells));
        }

        [TestCase(1700, 2, 2)]   // ⌈1200/500⌉=3, 최대 2개로 제한
        [TestCase(1700, 3, 3)]
        [TestCase(600, 1, 1)]    // ⌈100/500⌉=1
        [TestCase(500, 1, 0)]    // 초과 없음
        [TestCase(300, 0, 0)]
        public void ExcessBoxes(int personal, int maxConvertible, int expected)
        {
            Assert.AreEqual(expected, GoldHoldingsCalculator.ExcessBoxes(personal, 500, 500, maxConvertible));
        }
    }
}
