using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Game.Core;

namespace Game.Core.Tests
{
    // 고정 크기 3종(장비/소모품/개인물품) 저장소는 상수와 등록 인터페이스만 다르고 동작이 동일하다 -
    // 대표로 장비 저장소만 테스트한다(Docs/설계/32번 §4.1).
    public class PlaceholderEquipmentInventoryRepositoryTests
    {
        private class FakeItemDefinition : IInventoryItemDefinition
        {
            public string Id => "fake";
            public string DisplayName => Id;
            public Sprite Icon => null;
            public int FootprintWidth => 1;
            public int FootprintHeight => 1;
        }

        private GameObject gameObject;
        private PlaceholderEquipmentInventoryRepository repository;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject(nameof(PlaceholderEquipmentInventoryRepositoryTests));
            repository = gameObject.AddComponent<PlaceholderEquipmentInventoryRepository>();
            repository.ResolveDependencies(null); // 다른 의존성을 조회하지 않으므로 null로 충분하다.
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void ResolveDependencies_SetsFixedGridSize()
        {
            Assert.AreEqual(6, repository.GridWidth);
            Assert.AreEqual(4, repository.GridHeight);
        }

        [Test]
        public void TryPlaceItem_Succeeds_AndRaisesOnChanged()
        {
            var fired = false;
            repository.OnChanged += () => fired = true;

            var placed = repository.TryPlaceItem(new FakeItemDefinition(), new GridPosition(0, 0), out _);

            Assert.IsTrue(placed);
            Assert.IsTrue(fired);
            Assert.AreEqual(1, repository.Items.Count);
        }

        [Test]
        public void RemoveItem_RemovesPlacedItem_AndRaisesOnChanged()
        {
            repository.TryPlaceItem(new FakeItemDefinition(), new GridPosition(0, 0), out var placed);
            var fired = false;
            repository.OnChanged += () => fired = true;

            var removed = repository.RemoveItem(placed.InstanceId);

            Assert.IsTrue(removed);
            Assert.IsTrue(fired);
            Assert.AreEqual(0, repository.Items.Count);
        }

        [Test]
        public void RemoveItem_UnknownInstanceId_ReturnsFalse_AndDoesNotRaiseOnChanged()
        {
            var fired = false;
            repository.OnChanged += () => fired = true;

            var removed = repository.RemoveItem("does-not-exist");

            Assert.IsFalse(removed);
            Assert.IsFalse(fired);
        }

        // ==================== 전투 장비 팝업(설계 42번 §5, §7) ====================

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        }

        private ItemDefinitionTableAsset CreatePlaceholderTable()
        {
            var table = ScriptableObject.CreateInstance<ItemDefinitionTableAsset>();
            SetPrivateField(table, "entries", Enumerable.Range(1, 4)
                .Select(i => new ItemDefinitionEntry { Id = $"placeholder-equipment-{i}", FootprintWidth = 1, FootprintHeight = 1, Icon = null })
                .ToList());
            return table;
        }

        [Test]
        public void ResolveDependencies_WithoutPlaceholderRows_StartsEmpty()
        {
            Assert.AreEqual(0, repository.Items.Count);
        }

        [Test]
        public void ResolveDependencies_WithPlaceholderRows_PlacesFourScatteredItems()
        {
            var table = CreatePlaceholderTable();
            SetPrivateField(repository, "itemTable", table);

            repository.ResolveDependencies(null);

            Assert.AreEqual(4, repository.Items.Count);
            Assert.IsTrue(repository.TryGetItemAt(new GridPosition(5, 3), out var corner));
            Assert.AreEqual("placeholder-equipment-3", corner.Definition.Id);
            Object.DestroyImmediate(table);
        }

        [Test]
        public void OneByOneOverlap_IsAlwaysSwap()
        {
            repository.TryPlaceItem(new FakeItemDefinition(), new GridPosition(0, 0), out var dragged);
            repository.TryPlaceItem(new FakeItemDefinition(), new GridPosition(5, 3), out var other);

            var result = InventoryDropResolver.ResolveGridDrop(repository, repository, dragged, new GridPosition(5, 3), 0);
            Assert.AreEqual(InventoryDropKind.Swap, result.Kind);

            var fired = false;
            repository.OnChanged += () => fired = true;
            Assert.IsTrue(repository.TryApplyPlacements(result.Placements));
            Assert.IsTrue(fired);
            Assert.IsTrue(repository.TryGetItemAt(new GridPosition(0, 0), out var movedOther));
            Assert.AreEqual(other.InstanceId, movedOther.InstanceId);
        }

        [Test]
        public void AutoSort_OneByOne_FillsTopLeftWithoutRotation()
        {
            repository.TryPlaceItem(new FakeItemDefinition(), new GridPosition(5, 3), out _);
            repository.TryPlaceItem(new FakeItemDefinition(), new GridPosition(2, 2), out _);

            Assert.IsTrue(InventoryAutoSorter.TryBuildSortedLayout(repository.GridWidth, repository.GridHeight, repository.Items, repository.StagedItems, out var placements));

            CollectionAssert.AreEquivalent(new[] { new GridPosition(0, 0), new GridPosition(1, 0) }, placements.Select(p => p.Position.Value).ToArray());
            Assert.IsTrue(placements.All(p => p.QuarterTurns == 0));
        }
    }
}
