using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>마구간 매각 실제 처리(Docs/설계/104번 §3·§6). 대열 정리는 공용 파괴 처리 테스트가 다루므로 여기선 대열 저장소 없이 돌린다.</summary>
    public class CaravanAssetSaleServiceTests
    {
        private sealed class FakeOwned : IOwnedCaravanAssetRoster, IOwnedCaravanAssetRemover
        {
            public readonly Dictionary<string, OwnedCaravanAsset> Assets = new();
            public bool RefuseRemove;
            public event Action OnOwnedChanged { add { } remove { } }
            public IReadOnlyList<string> GetOwnedIds(FormationUnitKind kind) => new List<string>();
            public bool TryGetOwned(string instanceId, out OwnedCaravanAsset asset) => Assets.TryGetValue(instanceId, out asset);
            public int CountOwnedOfKind(FormationUnitKind kind) { var n = 0; foreach (var a in Assets.Values) if (a.Profile.Kind == kind) n++; return n; }
            public bool TryAddOwned(string kindId, out string instanceId) { instanceId = null; return false; }
            public bool TryRemoveOwned(string id) => !RefuseRemove && Assets.Remove(id);
            public void Add(string id, string kindId, FormationUnitKind kind, int price) => Assets[id] = new OwnedCaravanAsset(id, new CaravanAssetProfile(kindId, kindId, kind, kind.ToString(), price), 1);
        }

        private sealed class FakeCargo : IInventoryReader
        {
            public readonly List<InventoryItemInstance> List = new();
            public IReadOnlyList<InventorySection> Sections => Array.Empty<InventorySection>();
            public IReadOnlyCollection<InventoryItemInstance> Items => List;
            public bool TryGetItemAt(string sectionId, GridPosition position, out InventoryItemInstance item) { item = default; return false; }
            public event Action OnChanged { add { } remove { } }
        }

        private sealed class FakeDurability : ICaravanAssetDurabilityReader
        {
            public readonly Dictionary<string, (float current, int max)> Values = new();
            public bool TryGet(string instanceId, out float current, out int max)
            {
                var found = Values.TryGetValue(instanceId, out var v);
                current = v.current;
                max = v.max;
                return found;
            }
            public event Action Changed { add { } remove { } }
        }

        private sealed class FakeLocation : ITripCurrentLocationReader
        {
            public int CurrentCityId { get; set; } = 4;
            public event Action Changed { add { } remove { } }
        }

        private sealed class FakeStock : ITownStockReturner
        {
            public readonly List<(int city, TownStockCategory category, string id)> Returned = new();
            public bool TryReturn(int cityId, TownStockCategory category, string itemId) { Returned.Add((cityId, category, itemId)); return true; }
        }

        private sealed class FakeIncome : IGoldIncome
        {
            public int Total;
            public int Earn(int amount) { if (amount <= 0) return 0; Total += amount; return amount; }
        }

        private readonly List<GameObject> created = new();
        private FakeOwned owned;
        private FakeCargo cargo;
        private FakeDurability durability;
        private FakeStock stock;
        private FakeIncome income;

        [SetUp]
        public void SetUp()
        {
            owned = new FakeOwned();
            owned.Add("W1", "Wagon01", FormationUnitKind.Wagon, 1000);
            owned.Add("W2", "Wagon02", FormationUnitKind.Wagon, 801);
            owned.Add("F1", "Facility01", FormationUnitKind.Facility, 300);
            cargo = new FakeCargo();
            durability = new FakeDurability();
            stock = new FakeStock();
            income = new FakeIncome();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created) UnityEngine.Object.DestroyImmediate(go);
            created.Clear();
        }

        private CaravanAssetSaleService Create(ITripCurrentLocationReader location, IGoldIncome goldIncome)
        {
            var go = new GameObject(nameof(CaravanAssetSaleService));
            created.Add(go);
            var sale = go.AddComponent<CaravanAssetSaleService>();
            sale.Initialize(owned, cargo, durability, null, null, null, owned, location, stock, goldIncome);
            return sale;
        }

        private CaravanAssetSaleService Create() => Create(new FakeLocation(), income);

        [Test]
        public void Evaluate_CargoLastWagonNotOwned()
        {
            cargo.List.Add(new InventoryItemInstance("i1", null, new GridPosition(0, 0), 0, false, "W1"));
            var sale = Create();

            Assert.AreEqual(CaravanAssetSaleCheck.HasCargo, sale.Evaluate("W1"));
            Assert.AreEqual(CaravanAssetSaleCheck.Available, sale.Evaluate("W2"));
            Assert.AreEqual(CaravanAssetSaleCheck.Available, sale.Evaluate("F1"));
            Assert.AreEqual(CaravanAssetSaleCheck.NotOwned, sale.Evaluate("X"));

            owned.Assets.Remove("W1");
            Assert.AreEqual(CaravanAssetSaleCheck.LastWagon, sale.Evaluate("W2"));
        }

        [Test]
        public void SalePrice_ScalesByDurability()
        {
            durability.Values["W1"] = (6f, 10);
            var sale = Create();

            Assert.AreEqual(300, sale.SalePriceOf("W1"));
            Assert.AreEqual(400, sale.SalePriceOf("W2"), "기록 없음 = 감가 없음");
            Assert.AreEqual(0, sale.SalePriceOf("X"));
        }

        [Test]
        public void TrySell_RemovesReturnsStockAndPaysPriceComputedBeforeRemoval()
        {
            durability.Values["W1"] = (6f, 10);
            var sale = Create(new FakeLocation { CurrentCityId = 7 }, income);

            Assert.IsTrue(sale.TrySell("W1"));
            Assert.IsFalse(owned.Assets.ContainsKey("W1"));
            Assert.AreEqual(300, income.Total);
            Assert.AreEqual(1, stock.Returned.Count);
            Assert.AreEqual((7, TownStockCategory.Wagon, "Wagon01"), stock.Returned[0]);
        }

        [Test]
        public void TrySell_Facility_ReturnsToFacilityStock()
        {
            Assert.IsTrue(Create().TrySell("F1"));
            Assert.AreEqual((4, TownStockCategory.Facility, "Facility01"), stock.Returned[0]);
            Assert.AreEqual(150, income.Total);
        }

        [Test]
        public void TrySell_Blocked_ChangesNothing()
        {
            cargo.List.Add(new InventoryItemInstance("i1", null, new GridPosition(0, 0), 0, false, "W1"));
            var sale = Create();

            Assert.IsFalse(sale.TrySell("W1"));
            Assert.IsFalse(sale.TrySell("X"));
            owned.Assets.Remove("W1");
            Assert.IsFalse(sale.TrySell("W2"), "마지막 마차");
            Assert.AreEqual(0, income.Total);
            Assert.AreEqual(0, stock.Returned.Count);
            Assert.IsTrue(owned.Assets.ContainsKey("W2"));
        }

        [Test]
        public void TrySell_NoIncome_Refuses()
        {
            var sale = Create(new FakeLocation(), null);

            Assert.IsFalse(sale.TrySell("W2"));
            Assert.IsTrue(owned.Assets.ContainsKey("W2"));
            Assert.AreEqual(0, stock.Returned.Count);
        }

        [Test]
        public void TrySell_RemoveFails_NoPaymentOrStock()
        {
            owned.RefuseRemove = true;
            var sale = Create();

            Assert.IsFalse(sale.TrySell("W2"));
            Assert.AreEqual(0, income.Total);
            Assert.AreEqual(0, stock.Returned.Count);
        }

        [Test]
        public void TrySell_NoLocation_SkipsStockOnly()
        {
            var sale = Create(null, income);

            Assert.IsTrue(sale.TrySell("W2"));
            Assert.AreEqual(400, income.Total);
            Assert.AreEqual(0, stock.Returned.Count);
        }
    }
}
