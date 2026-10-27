using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    /// <summary>적재 사전 판정·의뢰 화물 출입(Docs/설계/107번 §8).</summary>
    public class QuestCargoServiceTests
    {
        private static readonly GoldTestItem Box = new("box", 1, 1);
        private static readonly GoldTestItem Wide = new("wide", 2, 1);

        private static GoldTestInventory Inventory(int width, int height)
        {
            var inventory = new GoldTestInventory(GoldTestInventory.Section("A", width, height));
            inventory.AddDefinition(Box);
            inventory.AddDefinition(Wide);
            return inventory;
        }

        private static QuestCargoService Service(GoldTestInventory inventory) => new(inventory, new CargoTestSettlement(inventory));

        [Test]
        public void CanFitAll_AllFit()
        {
            var inventory = Inventory(2, 1);
            Assert.IsTrue(CargoFitSimulator.CanFitAll(inventory.Sections, inventory.Items, new IInventoryItemDefinition[] { Box, Box }, true));
        }

        [Test]
        public void CanFitAll_OneDoesNotFit_False()
        {
            var inventory = Inventory(2, 1);
            Assert.IsFalse(CargoFitSimulator.CanFitAll(inventory.Sections, inventory.Items, new IInventoryItemDefinition[] { Box, Box, Box }, true));
        }

        [Test]
        public void CanFitAll_NeedsRelocation_True()
        {
            // 2x2에 1x1이 (1,0)에 있으면 2x1은 아래 행에 들어간다 - 재배치가 필요한 경우까지 계획기와 같은 결과여야 한다.
            var inventory = Inventory(2, 2);
            inventory.Place(Box, 1, 0, "A");
            inventory.Place(Box, 0, 1, "A");
            Assert.IsTrue(CargoFitSimulator.CanFitAll(inventory.Sections, inventory.Items, new IInventoryItemDefinition[] { Wide }, true));
        }

        [Test]
        public void CanFitAll_DoesNotMutateInventory()
        {
            var inventory = Inventory(2, 1);
            CargoFitSimulator.CanFitAll(inventory.Sections, inventory.Items, new IInventoryItemDefinition[] { Box }, true);
            Assert.AreEqual(0, inventory.Items.Count);
        }

        [Test]
        public void TryGiveAll_InsufficientSpace_GivesNothing()
        {
            var inventory = Inventory(1, 1);
            Assert.IsFalse(Service(inventory).TryGiveAll(new List<(string, int)> { ("box", 2) }));
            Assert.AreEqual(0, inventory.Items.Count);
            Assert.AreEqual(0, inventory.StagedItems.Count);

            Assert.IsTrue(Service(inventory).TryGiveAll(new List<(string, int)> { ("box", 1) }));
            Assert.AreEqual(1, inventory.Items.Count);
        }

        [Test]
        public void GiveOrStage_OverflowGoesToStaging()
        {
            var inventory = Inventory(1, 1);
            Assert.AreEqual(2, Service(inventory).GiveOrStage("box", 3));
            Assert.AreEqual(1, inventory.Items.Count);
            Assert.AreEqual(2, inventory.StagedItems.Count);
        }

        [Test]
        public void Remove_StagingFirst_CountIncludesStaging()
        {
            var inventory = Inventory(1, 1);
            inventory.Place(Box, 0, 0, "A");
            inventory.Stage(Box);
            var service = Service(inventory);
            Assert.AreEqual(2, service.Count("box"));
            Assert.AreEqual(1, service.Remove("box", 1));
            Assert.AreEqual(0, inventory.StagedItems.Count);
            Assert.AreEqual(1, service.Count("box"));
        }
    }
}
