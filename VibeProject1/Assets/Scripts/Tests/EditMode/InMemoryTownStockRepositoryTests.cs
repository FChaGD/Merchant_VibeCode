using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Game.Core;

namespace Game.Core.Tests
{
    public class InMemoryTownStockRepositoryTests
    {
        // 마을 1은 마구간이 없는 촌락, 마을 4는 모두 있는 대도시라고 가정한다.
        private class FakeFacilities : ITownFacilityAvailabilityReader
        {
            public bool IsFacilityAvailable(int cityId, string facilityId) => cityId == 4 || facilityId != TownFacilityIds.Stable;
        }

        private class Resolver : IDependencyResolver
        {
            private readonly Dictionary<System.Type, object> instances = new();
            public void Add<T>(T instance) => instances[typeof(T)] = instance;
            public T Resolve<T>() where T : class => (T)instances[typeof(T)];
            public bool TryResolve<T>(out T instance) where T : class
            {
                instance = instances.TryGetValue(typeof(T), out var found) ? (T)found : null;
                return instance != null;
            }
        }

        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in created) Object.DestroyImmediate(obj);
            created.Clear();
        }

        private InMemoryTownStockRepository Create(params TownStockEntry[] rows)
        {
            var table = ScriptableObject.CreateInstance<TownStockTableAsset>();
            typeof(TownStockTableAsset).GetField("entries", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(table, new List<TownStockEntry>(rows));
            created.Add(table);

            var go = new GameObject(nameof(InMemoryTownStockRepositoryTests));
            created.Add(go);
            var repository = go.AddComponent<InMemoryTownStockRepository>();
            typeof(InMemoryTownStockRepository).GetField("stockTable", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(repository, table);

            var resolver = new Resolver();
            resolver.Add<ITownFacilityAvailabilityReader>(new FakeFacilities());
            repository.ResolveDependencies(resolver);
            return repository;
        }

        private static TownStockEntry Row(int city, TownStockCategory category, string id, int quantity)
            => new() { CityId = city, Category = category, ItemId = id, Quantity = quantity };

        [Test]
        public void StableRowsInTownWithoutStable_AreIgnoredWithWarning()
        {
            var repository = Create(Row(1, TownStockCategory.Wagon, "Wagon01", 2), Row(1, TownStockCategory.TradeGoods, "a", 3));

            LogAssert.Expect(LogType.Warning, new Regex("마을 1에는 'Stable' 시설이 없어"));
            Assert.AreEqual(0, repository.GetLines(1, TownStockCategory.Wagon).Count);
            Assert.AreEqual(3, repository.GetLines(1, TownStockCategory.TradeGoods)[0].Remaining);
        }

        [Test]
        public void TownWithoutRows_DoesNotWarn()
        {
            var repository = Create(Row(4, TownStockCategory.Wagon, "Wagon01", 2));

            Assert.AreEqual(0, repository.GetLines(1, TownStockCategory.Wagon).Count);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TryConsume_RaisesChangedOnlyOnSuccess()
        {
            var repository = Create(Row(4, TownStockCategory.Wagon, "Wagon01", 1));
            var raised = 0;
            repository.OnStockChanged += () => raised++;

            Assert.IsTrue(repository.TryConsume(4, TownStockCategory.Wagon, "Wagon01"));
            Assert.IsFalse(repository.TryConsume(4, TownStockCategory.Wagon, "Wagon01"));

            Assert.AreEqual(1, raised);
        }
    }
}
