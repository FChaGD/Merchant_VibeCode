using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    public class InventoryGridTests
    {
        private class FakeItemDefinition : IInventoryItemDefinition
        {
            public string Id { get; }
            public string DisplayName => Id;
            public UnityEngine.Sprite Icon => null;
            public int FootprintWidth { get; }
            public int FootprintHeight { get; }

            public FakeItemDefinition(string id, int width, int height)
            {
                Id = id;
                FootprintWidth = width;
                FootprintHeight = height;
            }
        }

        private static readonly FakeItemDefinition OneByOne = new("1x1", 1, 1);
        private static readonly FakeItemDefinition TwoByOne = new("2x1", 2, 1);

        [Test]
        public void TryPlace_WithinBounds_Succeeds()
        {
            var grid = new InventoryGrid(4, 4);

            var placed = grid.TryPlace(OneByOne, new GridPosition(0, 0), out var item);

            Assert.IsTrue(placed);
            Assert.AreEqual(OneByOne, item.Definition);
        }

        [Test]
        public void TryPlace_OutOfBounds_Fails()
        {
            var grid = new InventoryGrid(2, 2);

            var placed = grid.TryPlace(TwoByOne, new GridPosition(1, 0), out _);

            Assert.IsFalse(placed);
        }

        [Test]
        public void TryPlace_OverlappingExistingItem_Fails()
        {
            var grid = new InventoryGrid(4, 4);
            grid.TryPlace(TwoByOne, new GridPosition(0, 0), out _);

            var placed = grid.TryPlace(OneByOne, new GridPosition(1, 0), out _);

            Assert.IsFalse(placed);
        }

        [Test]
        public void TryPlace_AdjacentNonOverlapping_Succeeds()
        {
            var grid = new InventoryGrid(4, 4);
            grid.TryPlace(TwoByOne, new GridPosition(0, 0), out _);

            var placed = grid.TryPlace(OneByOne, new GridPosition(2, 0), out _);

            Assert.IsTrue(placed);
        }

        [Test]
        public void Remove_FreesOccupiedCells()
        {
            var grid = new InventoryGrid(4, 4);
            grid.TryPlace(TwoByOne, new GridPosition(0, 0), out var placed);

            var removed = grid.Remove(placed.InstanceId);
            var replaced = grid.TryPlace(OneByOne, new GridPosition(0, 0), out _);

            Assert.IsTrue(removed);
            Assert.IsTrue(replaced);
        }

        [Test]
        public void Remove_UnknownInstanceId_ReturnsFalse()
        {
            var grid = new InventoryGrid(4, 4);

            Assert.IsFalse(grid.Remove("does-not-exist"));
        }

        [Test]
        public void TryGetAt_ReturnsItemOccupyingCell()
        {
            var grid = new InventoryGrid(4, 4);
            grid.TryPlace(TwoByOne, new GridPosition(0, 0), out var placed);

            var found = grid.TryGetAt(new GridPosition(1, 0), out var item);

            Assert.IsTrue(found);
            Assert.AreEqual(placed.InstanceId, item.InstanceId);
        }

        [Test]
        public void TryGetAt_EmptyCell_ReturnsFalse()
        {
            var grid = new InventoryGrid(4, 4);

            Assert.IsFalse(grid.TryGetAt(new GridPosition(0, 0), out _));
        }

        [Test]
        public void Resize_Shrink_ExcludesItemFromOverlapCheck_WithoutLosingIt()
        {
            var grid = new InventoryGrid(4, 4);
            grid.TryPlace(OneByOne, new GridPosition(3, 3), out var placed);

            grid.Resize(2, 2); // (3,3)이 범위 밖으로 밀려남

            Assert.AreEqual(1, grid.Items.Count); // 아이템 자체는 잃지 않는다
            Assert.IsFalse(grid.TryGetAt(new GridPosition(3, 3), out _)); // 하지만 범위 밖이라 조회는 안 됨
        }

        [Test]
        public void Resize_GrowBack_RevivesItemOccupancy()
        {
            var grid = new InventoryGrid(4, 4);
            grid.TryPlace(OneByOne, new GridPosition(3, 3), out var placed);
            grid.Resize(2, 2);

            grid.Resize(4, 4);

            var found = grid.TryGetAt(new GridPosition(3, 3), out var item);
            Assert.IsTrue(found);
            Assert.AreEqual(placed.InstanceId, item.InstanceId);
        }
    }
}
