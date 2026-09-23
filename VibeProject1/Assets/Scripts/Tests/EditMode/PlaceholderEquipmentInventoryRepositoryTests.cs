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
    }
}
