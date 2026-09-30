using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>
    /// 상단 물류품 팝업의 데이터·정책 계층(Docs/설계/40번 §3~4, §9): 회전, 원자적 재배치, 임시 보관,
    /// 드롭 판정, 자동 정렬.
    /// </summary>
    public class InventoryArrangementTests
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

        // InventoryDropResolver는 저장소 계약(IInventoryReader + IInventoryArrangement)에 의존한다 - 그리드를 그대로 감싼다.
        private class GridInventory : IInventoryReader, IInventoryArrangement
        {
            public readonly InventoryGrid Grid;

            public GridInventory(int width, int height) => Grid = new InventoryGrid(width, height);

            public GridInventory(InventoryGrid grid) => Grid = grid;

            public IReadOnlyList<InventorySection> Sections => Grid.Sections;
            public IReadOnlyCollection<InventoryItemInstance> Items => Grid.Items;
            public IReadOnlyList<InventoryItemInstance> StagedItems => Grid.StagedItems;
            public bool TryGetItemAt(string sectionId, GridPosition position, out InventoryItemInstance item) => Grid.TryGetAt(sectionId, position, out item);
            public bool TryGetItemAt(GridPosition position, out InventoryItemInstance item) => Grid.TryGetAt(null, position, out item);
            public bool TryApplyPlacements(IReadOnlyList<ItemPlacement> placements) => Grid.TryApplyPlacements(placements);
            public bool CanApplyPlacements(IReadOnlyList<ItemPlacement> placements) => Grid.CanApplyPlacements(placements);
            public event Action OnChanged { add { } remove { } }

            public InventoryItemInstance Place(IInventoryItemDefinition definition, int x, int y, string sectionId = null)
            {
                Assert.IsTrue(Grid.TryPlace(definition, new GridPosition(x, y), out var placed, 0, sectionId));
                return placed;
            }

            public InventoryItemInstance Find(string instanceId)
            {
                Assert.IsTrue(Grid.TryFind(instanceId, out var item));
                return item;
            }
        }

        private static InventorySection Rect(int width, int height) => new(InventoryGrid.DefaultSectionId, string.Empty, InventoryShape.Rectangle(width, height));

        private static readonly FakeItemDefinition OneByOne = new("a-1x1", 1, 1);
        private static readonly FakeItemDefinition TwoByOne = new("b-2x1", 2, 1);
        private static readonly FakeItemDefinition OneByTwo = new("c-1x2", 1, 2);
        private static readonly FakeItemDefinition TwoByTwo = new("d-2x2", 2, 2);

        // ==================== 회전 ====================

        [Test]
        public void RotateLocalCellClockwise_TwoByOneRightCell_BecomesBottomCell()
        {
            // 기획 39번 §3.4 예시: 2×1의 오른쪽 칸(1,0)을 잡고 회전하면 1×2의 아래 칸(0,1)이 된다.
            Assert.AreEqual(new GridPosition(0, 1), InventoryRotation.RotateLocalCellClockwise(new GridPosition(1, 0), heightBeforeRotation: 1));
        }

        [Test]
        public void RotateLocalCellClockwise_FourTimes_ReturnsToOriginalCell()
        {
            var cell = new GridPosition(1, 0);
            int width = 3, height = 2;
            for (var i = 0; i < 4; i++)
            {
                cell = InventoryRotation.RotateLocalCellClockwise(cell, height);
                (width, height) = (height, width);
            }

            Assert.AreEqual(new GridPosition(1, 0), cell);
        }

        [Test]
        public void RotatedPlacement_OccupiesSwappedFootprint()
        {
            var inventory = new GridInventory(4, 4);
            var item = inventory.Place(TwoByOne, 0, 0);

            Assert.IsTrue(inventory.TryApplyPlacements(new[] { new ItemPlacement(item.InstanceId, new GridPosition(0, 0), 1) }));

            Assert.IsTrue(inventory.TryGetItemAt(new GridPosition(0, 1), out _), "회전 후 1×2라 아래 칸을 점유해야 한다.");
            Assert.IsFalse(inventory.TryGetItemAt(new GridPosition(1, 0), out _), "회전 후 오른쪽 칸은 비어야 한다.");
        }

        // ==================== 원자적 재배치 / 임시 보관 ====================

        [Test]
        public void TryApplyPlacements_PartialFailure_LeavesStateUnchanged()
        {
            var inventory = new GridInventory(3, 1);
            var first = inventory.Place(OneByOne, 0, 0);
            var second = inventory.Place(OneByOne, 1, 0);

            // 두 번째 목표가 범위 밖 - 첫 번째 이동도 적용되면 안 된다.
            var applied = inventory.TryApplyPlacements(new[]
            {
                new ItemPlacement(first.InstanceId, new GridPosition(2, 0), 0),
                new ItemPlacement(second.InstanceId, new GridPosition(5, 0), 0),
            });

            Assert.IsFalse(applied);
            Assert.AreEqual(new GridPosition(0, 0), inventory.Find(first.InstanceId).Position);
            Assert.IsTrue(inventory.TryGetItemAt(new GridPosition(0, 0), out _));
            Assert.IsFalse(inventory.TryGetItemAt(new GridPosition(2, 0), out _));
        }

        [Test]
        public void CanApplyPlacements_DoesNotChangeState()
        {
            var inventory = new GridInventory(3, 1);
            var item = inventory.Place(OneByOne, 0, 0);

            Assert.IsTrue(inventory.CanApplyPlacements(new[] { new ItemPlacement(item.InstanceId, new GridPosition(2, 0), 0) }));

            Assert.AreEqual(new GridPosition(0, 0), inventory.Find(item.InstanceId).Position);
            Assert.IsTrue(inventory.TryGetItemAt(new GridPosition(0, 0), out _));
        }

        [Test]
        public void MoveToStaging_FreesCells_AndKeepsItem()
        {
            var inventory = new GridInventory(2, 2);
            var item = inventory.Place(TwoByTwo, 0, 0);

            Assert.IsTrue(inventory.TryApplyPlacements(new[] { ItemPlacement.ToStaging(item.InstanceId, 0) }));

            Assert.AreEqual(0, inventory.Items.Count);
            Assert.AreEqual(1, inventory.StagedItems.Count);
            Assert.IsTrue(inventory.StagedItems[0].IsStaged);
            Assert.IsFalse(inventory.TryGetItemAt(new GridPosition(1, 1), out _));
        }

        [Test]
        public void StagedItems_KeepOrder_WhenOneReturnsToGrid()
        {
            var inventory = new GridInventory(3, 1);
            var a = inventory.Place(OneByOne, 0, 0);
            var b = inventory.Place(OneByOne, 1, 0);
            var c = inventory.Place(OneByOne, 2, 0);
            inventory.TryApplyPlacements(new[] { ItemPlacement.ToStaging(a.InstanceId, 0) });
            inventory.TryApplyPlacements(new[] { ItemPlacement.ToStaging(b.InstanceId, 0) });
            inventory.TryApplyPlacements(new[] { ItemPlacement.ToStaging(c.InstanceId, 0) });

            inventory.TryApplyPlacements(new[] { new ItemPlacement(b.InstanceId, new GridPosition(0, 0), 0) });

            CollectionAssert.AreEqual(new[] { a.InstanceId, c.InstanceId }, inventory.StagedItems.Select(item => item.InstanceId).ToArray());
        }

        // ==================== 드롭 판정 ====================

        [Test]
        public void ResolveGridDrop_EmptyTarget_IsMove()
        {
            var inventory = new GridInventory(4, 2);
            var item = inventory.Place(TwoByOne, 0, 0);

            var result = InventoryDropResolver.ResolveGridDrop(inventory, inventory, item, null, new GridPosition(2, 1), 0);

            Assert.AreEqual(InventoryDropKind.Move, result.Kind);
        }

        [Test]
        public void ResolveGridDrop_OverlappingOwnCells_IsMove()
        {
            var inventory = new GridInventory(4, 1);
            var item = inventory.Place(TwoByOne, 0, 0);

            var result = InventoryDropResolver.ResolveGridDrop(inventory, inventory, item, null, new GridPosition(1, 0), 0);

            Assert.AreEqual(InventoryDropKind.Move, result.Kind);
        }

        [Test]
        public void ResolveGridDrop_OutOfBounds_IsInvalid()
        {
            var inventory = new GridInventory(4, 1);
            var item = inventory.Place(TwoByOne, 0, 0);

            var result = InventoryDropResolver.ResolveGridDrop(inventory, inventory, item, null, new GridPosition(3, 0), 0);

            Assert.AreEqual(InventoryDropKind.Invalid, result.Kind);
        }

        [Test]
        public void ResolveGridDrop_SameSizeOverlap_SwapsToOriginalPosition()
        {
            var inventory = new GridInventory(3, 1);
            var dragged = inventory.Place(OneByOne, 0, 0);
            var other = inventory.Place(OneByOne, 2, 0);

            var result = InventoryDropResolver.ResolveGridDrop(inventory, inventory, dragged, null, new GridPosition(2, 0), 0);
            Assert.AreEqual(InventoryDropKind.Swap, result.Kind);

            Assert.IsTrue(inventory.TryApplyPlacements(result.Placements));
            Assert.AreEqual(new GridPosition(2, 0), inventory.Find(dragged.InstanceId).Position);
            Assert.AreEqual(new GridPosition(0, 0), inventory.Find(other.InstanceId).Position);
        }

        [Test]
        public void ResolveGridDrop_DifferentSizeOverlap_SwapsWhenBothFit()
        {
            // [1x1][ . ][2x1 2x1] → 1x1을 2x1 자리(2,0)에 놓으면 2x1은 (0,0)으로 가서 (0,0)~(1,0)을 점유.
            var inventory = new GridInventory(4, 1);
            var dragged = inventory.Place(OneByOne, 0, 0);
            var other = inventory.Place(TwoByOne, 2, 0);

            var result = InventoryDropResolver.ResolveGridDrop(inventory, inventory, dragged, null, new GridPosition(2, 0), 0);

            Assert.AreEqual(InventoryDropKind.Swap, result.Kind);
        }

        [Test]
        public void ResolveGridDrop_DifferentSizeOverlap_InvalidWhenDisplacedDoesNotFit()
        {
            // [1x1][2x1 2x1] - 1x1을 (1,0)에 놓으면 2x1이 1x1의 원래 자리 (0,0)으로 가서 (0,0)~(1,0)을 차지해야
            // 하는데, (1,0)은 방금 놓은 1x1과 겹친다 → 교환 불가, 원위치.
            var inventory = new GridInventory(3, 1);
            var dragged = inventory.Place(OneByOne, 0, 0);
            inventory.Place(TwoByOne, 1, 0);

            var result = InventoryDropResolver.ResolveGridDrop(inventory, inventory, dragged, null, new GridPosition(1, 0), 0);

            Assert.AreEqual(InventoryDropKind.Invalid, result.Kind);
        }

        [Test]
        public void ResolveGridDrop_DisplacedOverlapsDraggedNewPosition_IsInvalid()
        {
            // 행 0: [2x2 2x2][1x1] / 행 1: [2x2 2x2][ . ] - 1x1을 (1,0)에 놓으면 2x2가 1x1의 원래 자리 (2,0)으로
            // 가야 하는데 그리드 폭 3이라 범위 밖 → Invalid.
            var inventory = new GridInventory(3, 2);
            inventory.Place(TwoByTwo, 0, 0);
            var dragged = inventory.Place(OneByOne, 2, 0);

            var result = InventoryDropResolver.ResolveGridDrop(inventory, inventory, dragged, null, new GridPosition(1, 0), 0);

            Assert.AreEqual(InventoryDropKind.Invalid, result.Kind);
        }

        [Test]
        public void ResolveGridDrop_TwoItemsOverlapped_IsInvalid()
        {
            var inventory = new GridInventory(3, 2);
            inventory.Place(OneByOne, 0, 0);
            inventory.Place(OneByOne, 1, 0);
            var dragged = inventory.Place(TwoByOne, 0, 1);

            var result = InventoryDropResolver.ResolveGridDrop(inventory, inventory, dragged, null, new GridPosition(0, 0), 0);

            Assert.AreEqual(InventoryDropKind.Invalid, result.Kind);
        }

        [Test]
        public void ResolveGridDrop_FromStaging_DisplacedGoesToStaging()
        {
            var inventory = new GridInventory(2, 1);
            var staged = inventory.Place(OneByOne, 0, 0);
            inventory.TryApplyPlacements(new[] { ItemPlacement.ToStaging(staged.InstanceId, 0) });
            var other = inventory.Place(TwoByOne, 0, 0);

            var result = InventoryDropResolver.ResolveGridDrop(inventory, inventory, inventory.Find(staged.InstanceId), null, new GridPosition(0, 0), 0);
            Assert.AreEqual(InventoryDropKind.Swap, result.Kind);

            Assert.IsTrue(inventory.TryApplyPlacements(result.Placements));
            Assert.IsFalse(inventory.Find(staged.InstanceId).IsStaged);
            Assert.IsTrue(inventory.Find(other.InstanceId).IsStaged);
        }

        [Test]
        public void ResolveGridDrop_RotatedDrop_UsesRotatedFootprint()
        {
            var inventory = new GridInventory(1, 2);
            var item = inventory.Place(OneByTwo, 0, 0);

            // 회전 안 하면 1×2라 들어가지만, 1회 회전하면 2×1이 되어 폭 1 그리드를 넘는다.
            Assert.AreEqual(InventoryDropKind.Move, InventoryDropResolver.ResolveGridDrop(inventory, inventory, item, null, new GridPosition(0, 0), 0).Kind);
            Assert.AreEqual(InventoryDropKind.Invalid, InventoryDropResolver.ResolveGridDrop(inventory, inventory, item, null, new GridPosition(0, 0), 1).Kind);
        }

        // ==================== 자동 정렬 ====================

        [Test]
        public void TryBuildSortedLayout_PlacesLargestFirst_TopLeft()
        {
            var inventory = new GridInventory(4, 2);
            inventory.Place(OneByOne, 0, 0);
            var big = inventory.Place(TwoByTwo, 2, 0);

            Assert.IsTrue(InventoryAutoSorter.TryBuildSortedLayout(Rect(4, 2), inventory.Items, inventory.StagedItems, out var placements));
            Assert.IsTrue(inventory.TryApplyPlacements(placements));

            Assert.AreEqual(new GridPosition(0, 0), inventory.Find(big.InstanceId).Position);
        }

        [Test]
        public void TryBuildSortedLayout_RotatesWhenOriginalOrientationDoesNotFit()
        {
            // 폭 1 × 높이 2 그리드에 임시 보관된 2×1 → 원래 방향으로는 못 넣고 시계방향 90° 회전해서 넣는다.
            var wide = new GridInventory(2, 1).Place(TwoByOne, 0, 0);

            Assert.IsTrue(InventoryAutoSorter.TryBuildSortedLayout(Rect(1, 2), Array.Empty<InventoryItemInstance>(), new[] { wide }, out var placements));

            Assert.AreEqual(1, placements.Count);
            Assert.AreEqual(new GridPosition(0, 0), placements[0].Position);
            Assert.AreEqual(1, placements[0].QuarterTurns);
        }

        [Test]
        public void TryBuildSortedLayout_SameInput_SameResult()
        {
            var inventory = new GridInventory(4, 2);
            inventory.Place(OneByOne, 3, 1);
            inventory.Place(TwoByOne, 0, 1);
            inventory.Place(OneByTwo, 2, 0);

            InventoryAutoSorter.TryBuildSortedLayout(Rect(4, 2), inventory.Items, inventory.StagedItems, out var first);
            InventoryAutoSorter.TryBuildSortedLayout(Rect(4, 2), inventory.Items.Reverse(), inventory.StagedItems, out var second);

            CollectionAssert.AreEqual(
                first.Select(p => (p.InstanceId, p.Position, p.QuarterTurns)).ToArray(),
                second.Select(p => (p.InstanceId, p.Position, p.QuarterTurns)).ToArray());
        }

        [Test]
        public void TryBuildSortedLayout_DoesNotFit_ReturnsFalse()
        {
            var inventory = new GridInventory(2, 1);
            inventory.Place(TwoByOne, 0, 0);
            var extra = new GridInventory(2, 1).Place(OneByOne, 0, 0);

            Assert.IsFalse(InventoryAutoSorter.TryBuildSortedLayout(Rect(2, 1), inventory.Items, new[] { extra }, out _));
        }

        [Test]
        public void TryBuildStagedFlush_FillsFreeCells_WithoutMovingPlacedItems()
        {
            var inventory = new GridInventory(3, 1);
            var fixedItem = inventory.Place(OneByOne, 1, 0);
            var staged = inventory.Place(OneByOne, 0, 0);
            inventory.TryApplyPlacements(new[] { ItemPlacement.ToStaging(staged.InstanceId, 0) });

            Assert.IsTrue(InventoryAutoSorter.TryBuildStagedFlush(new[] { Rect(3, 1) }, inventory.Items, inventory.StagedItems, out var placements));
            Assert.IsTrue(inventory.TryApplyPlacements(placements));

            Assert.AreEqual(new GridPosition(1, 0), inventory.Find(fixedItem.InstanceId).Position);
            Assert.AreEqual(0, inventory.StagedItems.Count);
        }

        [Test]
        public void TryBuildStagedFlush_NoRoom_ReturnsFalse()
        {
            var inventory = new GridInventory(1, 1);
            var staged = inventory.Place(OneByOne, 0, 0);
            inventory.TryApplyPlacements(new[] { ItemPlacement.ToStaging(staged.InstanceId, 0) });
            inventory.Place(OneByOne, 0, 0);

            Assert.IsFalse(InventoryAutoSorter.TryBuildStagedFlush(new[] { Rect(1, 1) }, inventory.Items, inventory.StagedItems, out _));
        }

        // ==================== 섹션(마차별 그리드, Docs/설계/64번 §11) ====================

        private static GridInventory TwoWagons(int width, int height)
        {
            var grid = new InventoryGrid();
            grid.AddSection(new InventorySection("a", "A", InventoryShape.Rectangle(width, height)));
            grid.AddSection(new InventorySection("b", "B", InventoryShape.Rectangle(width, height)));
            return new GridInventory(grid);
        }

        [Test]
        public void ResolveGridDrop_OtherSection_MovesThere_AndSwapSendsDisplacedToOriginalSection()
        {
            var inventory = TwoWagons(2, 1);
            var dragged = inventory.Place(OneByOne, 0, 0, "a");
            var other = inventory.Place(OneByOne, 1, 0, "b");

            var move = InventoryDropResolver.ResolveGridDrop(inventory, inventory, dragged, "b", new GridPosition(0, 0), 0);
            Assert.AreEqual(InventoryDropKind.Move, move.Kind);
            Assert.AreEqual("b", move.Placements[0].SectionId);

            var swap = InventoryDropResolver.ResolveGridDrop(inventory, inventory, dragged, "b", new GridPosition(1, 0), 0);
            Assert.AreEqual(InventoryDropKind.Swap, swap.Kind);
            Assert.IsTrue(inventory.TryApplyPlacements(swap.Placements));
            Assert.AreEqual("a", inventory.Find(other.InstanceId).SectionId, "밀려난 아이템은 드래그 아이템의 원래 마차로 간다.");
            Assert.AreEqual(new GridPosition(0, 0), inventory.Find(other.InstanceId).Position);
            Assert.AreEqual("b", inventory.Find(dragged.InstanceId).SectionId);
        }

        [Test]
        public void ResolveGridDrop_OnBlockedCell_IsInvalid()
        {
            Assert.IsTrue(InventoryShape.TryParse("10/11", out var shape, out _));
            var grid = new InventoryGrid();
            grid.AddSection(new InventorySection("a", string.Empty, shape));
            var inventory = new GridInventory(grid);
            var item = inventory.Place(OneByOne, 0, 1, "a");

            Assert.AreEqual(InventoryDropKind.Invalid, InventoryDropResolver.ResolveGridDrop(inventory, inventory, item, "a", new GridPosition(1, 0), 0).Kind);
            Assert.AreEqual(InventoryDropKind.Move, InventoryDropResolver.ResolveGridDrop(inventory, inventory, item, "a", new GridPosition(0, 0), 0).Kind);
        }

        [Test]
        public void TryBuildSortedLayout_Section_AvoidsBlockedCells_AndLeavesOtherSections()
        {
            Assert.IsTrue(InventoryShape.TryParse("01/11", out var shape, out _));
            var grid = new InventoryGrid();
            grid.AddSection(new InventorySection("a", string.Empty, shape));
            grid.AddSection(new InventorySection("b", string.Empty, InventoryShape.Rectangle(2, 2)));
            var inventory = new GridInventory(grid);
            var wide = inventory.Place(TwoByOne, 0, 1, "a");
            var elsewhere = inventory.Place(OneByOne, 0, 0, "b");

            Assert.IsTrue(InventoryAutoSorter.TryBuildSortedLayout(grid.Sections[0], inventory.Items, inventory.StagedItems, out var placements));

            Assert.AreEqual(1, placements.Count, "다른 섹션의 아이템은 정렬 대상이 아니다.");
            Assert.AreEqual(wide.InstanceId, placements[0].InstanceId);
            Assert.AreEqual(new GridPosition(0, 1), placements[0].Position, "막힌 칸(0,0)을 피해 아래 줄에 놓인다.");
            Assert.IsTrue(inventory.TryApplyPlacements(placements));
            Assert.AreEqual("b", inventory.Find(elsewhere.InstanceId).SectionId);
        }

        [Test]
        public void TryBuildStagedFlush_StartsFromGivenSection_AndSpillsToNext()
        {
            var inventory = TwoWagons(1, 1);
            var first = inventory.Place(OneByOne, 0, 0, "a");
            var second = inventory.Place(OneByOne, 0, 0, "b");
            inventory.TryApplyPlacements(new[] { ItemPlacement.ToStaging(first.InstanceId, 0), ItemPlacement.ToStaging(second.InstanceId, 0) });

            var order = InventoryAutoSorter.OrderFrom(inventory.Sections, "b");
            Assert.AreEqual("b", order[0].Id);
            Assert.AreEqual("a", order[1].Id);
            Assert.IsTrue(InventoryAutoSorter.TryBuildStagedFlush(order, inventory.Items, inventory.StagedItems, out var placements));
            Assert.IsTrue(inventory.TryApplyPlacements(placements));
            Assert.AreEqual(0, inventory.StagedItems.Count);
            Assert.IsTrue(inventory.TryGetItemAt("a", new GridPosition(0, 0), out _));
            Assert.IsTrue(inventory.TryGetItemAt("b", new GridPosition(0, 0), out _));
        }

        [Test]
        public void TryFindSlot_PrefersFirstSectionInOrder_ThenFallsBack()
        {
            var inventory = TwoWagons(1, 1);
            inventory.Place(OneByOne, 0, 0, "b");

            Assert.IsTrue(InventoryAutoSorter.TryFindSlot(InventoryAutoSorter.OrderFrom(inventory.Sections, "b"), inventory.Items, OneByOne, allowRotation: true, out var sectionId, out _, out _));
            Assert.AreEqual("a", sectionId, "보이는 마차(b)가 차 있으면 다음 마차로 넘어간다.");
        }

        // ==================== 품목 색 ====================

        [Test]
        public void ColorFor_SameId_IsStable()
        {
            Assert.AreEqual(InventoryItemColorPalette.IndexFor("placeholder-2x1"), InventoryItemColorPalette.IndexFor("placeholder-2x1"));
        }
    }
}
