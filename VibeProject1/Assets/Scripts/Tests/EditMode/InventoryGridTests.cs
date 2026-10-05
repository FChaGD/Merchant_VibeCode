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
            public string Description => string.Empty;
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

            var found = grid.TryGetAt(null, new GridPosition(1, 0), out var item);

            Assert.IsTrue(found);
            Assert.AreEqual(placed.InstanceId, item.InstanceId);
        }

        [Test]
        public void TryGetAt_EmptyCell_ReturnsFalse()
        {
            var grid = new InventoryGrid(4, 4);

            Assert.IsFalse(grid.TryGetAt(null, new GridPosition(0, 0), out _));
        }

        // ==================== 모양·섹션 (Docs/설계/64번 §11) ====================

        private static InventoryShape Shape(string mask)
        {
            Assert.IsTrue(InventoryShape.TryParse(mask, out var shape, out var error), error);
            return shape;
        }

        [Test]
        public void ShapeParse_ValidMask_ReadsSizeAndBlockedCells()
        {
            var shape = Shape("11110/11111/11111/01110");

            Assert.AreEqual(5, shape.Width);
            Assert.AreEqual(4, shape.Height);
            Assert.AreEqual(17, shape.UsableCount);
            Assert.IsFalse(shape.IsUsable(4, 0));
            Assert.IsFalse(shape.IsUsable(0, 3));
            Assert.IsTrue(shape.IsUsable(4, 1));
            Assert.AreEqual("11110/11111/11111/01110", shape.ToString());
        }

        [TestCase("")]
        [TestCase("111/11")]
        [TestCase("1a1")]
        [TestCase("000/000")]
        public void ShapeParse_InvalidMask_Fails(string mask)
        {
            Assert.IsFalse(InventoryShape.TryParse(mask, out _, out var error));
            Assert.IsFalse(string.IsNullOrEmpty(error));
        }

        [Test]
        public void TryPlace_OnBlockedCell_Fails()
        {
            var grid = new InventoryGrid();
            grid.AddSection(new InventorySection("w", string.Empty, Shape("10/11")));

            Assert.IsFalse(grid.TryPlace(TwoByOne, new GridPosition(0, 0), out _, 0, "w"), "(1,0)이 막힌 칸이라 2×1이 들어가지 않는다.");
            Assert.IsTrue(grid.TryPlace(TwoByOne, new GridPosition(0, 1), out _, 0, "w"));
            Assert.IsFalse(grid.TryGetAt("w", new GridPosition(1, 0), out _));
        }

        [Test]
        public void ApplyPlacements_MovesItemAcrossSections()
        {
            var grid = new InventoryGrid();
            grid.AddSection(new InventorySection("a", string.Empty, InventoryShape.Rectangle(2, 2)));
            grid.AddSection(new InventorySection("b", string.Empty, InventoryShape.Rectangle(2, 2)));
            grid.TryPlace(OneByOne, new GridPosition(0, 0), out var item, 0, "a");

            Assert.IsTrue(grid.TryApplyPlacements(new[] { new ItemPlacement(item.InstanceId, new GridPosition(1, 1), 0, "b") }));

            Assert.IsFalse(grid.TryGetAt("a", new GridPosition(0, 0), out _));
            Assert.IsTrue(grid.TryGetAt("b", new GridPosition(1, 1), out var moved));
            Assert.AreEqual("b", moved.SectionId);
            Assert.AreEqual(item.InstanceId, moved.InstanceId);
        }

        [Test]
        public void ApplyPlacements_CrossSectionSwapFailure_LeavesBothSectionsUnchanged()
        {
            var grid = new InventoryGrid();
            grid.AddSection(new InventorySection("a", string.Empty, InventoryShape.Rectangle(2, 1)));
            grid.AddSection(new InventorySection("b", string.Empty, InventoryShape.Rectangle(1, 1)));
            grid.TryPlace(TwoByOne, new GridPosition(0, 0), out var wide, 0, "a");
            grid.TryPlace(OneByOne, new GridPosition(0, 0), out var small, 0, "b");

            // 2×1은 1×1 섹션에 들어가지 않는다 - 교환 전체가 취소돼야 한다.
            var swap = new[]
            {
                new ItemPlacement(wide.InstanceId, new GridPosition(0, 0), 0, "b"),
                new ItemPlacement(small.InstanceId, new GridPosition(0, 0), 0, "a"),
            };
            Assert.IsFalse(grid.TryApplyPlacements(swap));

            Assert.IsTrue(grid.TryGetAt("a", new GridPosition(1, 0), out var stillWide));
            Assert.AreEqual(wide.InstanceId, stillWide.InstanceId);
            Assert.IsTrue(grid.TryGetAt("b", new GridPosition(0, 0), out var stillSmall));
            Assert.AreEqual(small.InstanceId, stillSmall.InstanceId);
        }

        [Test]
        public void AddSection_DuplicateId_IsRejected()
        {
            var grid = new InventoryGrid();
            Assert.IsTrue(grid.AddSection(new InventorySection("a", string.Empty, InventoryShape.Rectangle(1, 1))));
            Assert.IsFalse(grid.AddSection(new InventorySection("a", string.Empty, InventoryShape.Rectangle(2, 2))));
            Assert.AreEqual(1, grid.Sections.Count);
        }

        [Test]
        public void StageNew_AddsStagedInstance()
        {
            var grid = new InventoryGrid(2, 2);

            var staged = grid.StageNew(OneByOne);

            Assert.IsTrue(staged.IsStaged);
            Assert.AreEqual(OneByOne, staged.Definition);
            Assert.AreEqual(1, grid.StagedItems.Count);
            Assert.AreEqual(staged.InstanceId, grid.StagedItems[0].InstanceId);
            Assert.AreEqual(0, grid.Items.Count);
        }

        [Test]
        public void RemoveSection_EmptySection_Removed()
        {
            var grid = new InventoryGrid();
            grid.AddSection(new InventorySection("a", string.Empty, InventoryShape.Rectangle(1, 1)));
            grid.AddSection(new InventorySection("b", string.Empty, InventoryShape.Rectangle(1, 1)));

            Assert.IsTrue(grid.RemoveSection("a"));

            Assert.IsFalse(grid.HasSection("a"));
            Assert.AreEqual(1, grid.Sections.Count);
            Assert.AreEqual("b", grid.Sections[0].Id);
        }

        [Test]
        public void RemoveSection_WithItems_ReturnsFalse()
        {
            var grid = new InventoryGrid();
            grid.AddSection(new InventorySection("a", string.Empty, InventoryShape.Rectangle(1, 1)));
            grid.TryPlace(OneByOne, new GridPosition(0, 0), out _, 0, "a");

            // 아이템 처리(임시 보관·환급 여부)는 호출자의 정책이다 - 그리드가 몰래 지우지 않는다.
            Assert.IsFalse(grid.RemoveSection("a"));
            Assert.IsTrue(grid.HasSection("a"));
            Assert.IsTrue(grid.TryGetAt("a", new GridPosition(0, 0), out _));
        }
    }
}
