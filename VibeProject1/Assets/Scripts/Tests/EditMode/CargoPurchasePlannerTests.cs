using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    public class CargoPurchasePlannerTests
    {
        private static readonly GoldTestItem One = new("one", 1, 1);
        private static readonly GoldTestItem Wide = new("wide", 2, 1);
        private static readonly GoldTestItem Square = new("square", 2, 2);

        private static bool Plan(GoldTestInventory inventory, string preferred, IInventoryItemDefinition definition, bool allowRotation, out CargoPurchasePlan plan)
            => CargoPurchasePlanner.TryPlan(inventory.Sections, preferred, inventory.Items.ToList(), definition, allowRotation, out plan);

        [Test]
        public void DirectSlot_NoMoves()
        {
            var inventory = new GoldTestInventory(GoldTestInventory.Section("A", 2, 2));
            inventory.Place(One, 0, 0, "A");

            Assert.IsTrue(Plan(inventory, null, One, true, out var plan));
            Assert.AreEqual("A", plan.SectionId);
            Assert.AreEqual(new GridPosition(1, 0), plan.Position);
            Assert.AreEqual(0, plan.Moves.Count);
        }

        [Test]
        public void Relocate_WithinSameWagon_ChoosesFewestMoves()
        {
            // A 3×2: x(0,0), y(1,1). 2×2를 (0,0)에 두면 2개 이동, (1,0)에 두면 y 1개만 이동 → (1,0) 선택, y는 같은 마차 (0,1)로.
            var inventory = new GoldTestInventory(GoldTestInventory.Section("A", 3, 2));
            inventory.Place(One, 0, 0, "A");
            var y = inventory.Place(One, 1, 1, "A");

            Assert.IsTrue(Plan(inventory, null, Square, true, out var plan));
            Assert.AreEqual("A", plan.SectionId);
            Assert.AreEqual(new GridPosition(1, 0), plan.Position);
            Assert.AreEqual(1, plan.Moves.Count);
            Assert.AreEqual(y.InstanceId, plan.Moves[0].InstanceId);
            Assert.AreEqual("A", plan.Moves[0].SectionId);
            Assert.AreEqual(new GridPosition(0, 1), plan.Moves[0].Position);
            Assert.IsTrue(inventory.CanApplyPlacements(plan.Moves));
        }

        [Test]
        public void Relocate_ToOtherWagon()
        {
            // A 2×1이 p·q로 꽉 참, B 2×1 비어 있음. 바로 놓기는 B(0,0)이 되므로 선호 마차 A + B를 막아 재배치를 강제한다.
            var inventory = new GoldTestInventory(GoldTestInventory.Section("A", 2, 1), GoldTestInventory.Section("B", 2, 1), GoldTestInventory.Section("C", 2, 1));
            var p = inventory.Place(One, 0, 0, "A");
            var q = inventory.Place(One, 1, 0, "A");
            inventory.Place(One, 0, 0, "B");
            inventory.Place(One, 1, 0, "C");

            // 바로 놓을 2×1 자리가 없다. A에 놓으려면 p·q를 B(1,0)·C(0,0)로 옮겨야 한다. 같은 크기·같은 정의라 둘 중 누가
            // 어느 마차로 가는지는 인스턴스 Id 순서에 달려 있으므로 목적지 집합만 확인한다.
            Assert.IsTrue(Plan(inventory, "A", Wide, false, out var plan));
            Assert.AreEqual("A", plan.SectionId);
            Assert.AreEqual(new GridPosition(0, 0), plan.Position);
            CollectionAssert.AreEquivalent(new[] { p.InstanceId, q.InstanceId }, plan.Moves.Select(move => move.InstanceId));
            CollectionAssert.AreEquivalent(new[] { "B", "C" }, plan.Moves.Select(move => move.SectionId));
            Assert.IsTrue(inventory.CanApplyPlacements(plan.Moves));
        }

        [Test]
        public void PreferredSection_DecidesWagon()
        {
            var inventory = new GoldTestInventory(GoldTestInventory.Section("A", 2, 1), GoldTestInventory.Section("B", 2, 1));
            inventory.Place(One, 0, 0, "A");
            inventory.Place(One, 0, 0, "B");

            Assert.IsTrue(Plan(inventory, "B", Wide, false, out var preferB));
            Assert.AreEqual("B", preferB.SectionId);
            Assert.AreEqual("A", preferB.Moves[0].SectionId);

            Assert.IsTrue(Plan(inventory, null, Wide, false, out var preferNone));
            Assert.AreEqual("A", preferNone.SectionId);
            Assert.AreEqual("B", preferNone.Moves[0].SectionId);
        }

        [Test]
        public void Rotation_UsedWhenAllowed()
        {
            // A 1×2(세로)에 p(0,0), B 1×1 비어 있음. 2×1은 회전해야 A에 들어가고 p는 B로 간다.
            var inventory = new GoldTestInventory(GoldTestInventory.Section("A", 1, 2), GoldTestInventory.Section("B", 1, 1));
            inventory.Place(One, 0, 0, "A");

            Assert.IsTrue(Plan(inventory, null, Wide, true, out var plan));
            Assert.AreEqual("A", plan.SectionId);
            Assert.AreEqual(1, plan.QuarterTurns);
            Assert.AreEqual("B", plan.Moves[0].SectionId);

            Assert.IsFalse(Plan(inventory, null, Wide, false, out _));
        }

        [Test]
        public void NoRoomAnywhere_ReturnsFalse()
        {
            var inventory = new GoldTestInventory(GoldTestInventory.Section("A", 1, 1));
            inventory.Place(One, 0, 0, "A");

            Assert.IsFalse(Plan(inventory, null, One, true, out _));
        }

        [Test]
        public void ExcludedItems_TreatedAsEmpty()
        {
            // 인출될 상자를 호출자가 목록에서 뺀 경우 그 자리를 빈칸으로 본다(설계 83번 §5.2).
            var inventory = new GoldTestInventory(GoldTestInventory.Section("A", 1, 1));
            var box = inventory.Place(One, 0, 0, "A");
            var withoutBox = inventory.Items.Where(item => item.InstanceId != box.InstanceId).ToList();

            Assert.IsTrue(CargoPurchasePlanner.TryPlan(inventory.Sections, null, withoutBox, One, true, out var plan));
            Assert.AreEqual(0, plan.Moves.Count);
        }
    }
}
