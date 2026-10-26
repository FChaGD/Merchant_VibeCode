using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>숙소·상인조합·마구간 매각 Placeholder(Docs/설계/103번 §4.1).</summary>
    public class PlaceholderInnGuildSaleTests
    {
        private sealed class FakeOwned : IOwnedCaravanAssetRoster
        {
            public readonly Dictionary<string, OwnedCaravanAsset> Assets = new();
            public event Action OnOwnedChanged { add { } remove { } }
            public IReadOnlyList<string> GetOwnedIds(FormationUnitKind kind) => new List<string>();
            public bool TryGetOwned(string instanceId, out OwnedCaravanAsset asset) => Assets.TryGetValue(instanceId, out asset);
            public int CountOwnedOfKind(FormationUnitKind kind) { var n = 0; foreach (var a in Assets.Values) if (a.Profile.Kind == kind) n++; return n; }
            public bool TryAddOwned(string kindId, out string instanceId) { instanceId = null; return false; }
            public void Add(string id, FormationUnitKind kind, int price) => Assets[id] = new OwnedCaravanAsset(id, new CaravanAssetProfile(id, id, kind, kind.ToString(), price), 1);
        }

        private sealed class FakeCargo : IInventoryReader
        {
            public readonly List<InventoryItemInstance> List = new();
            public IReadOnlyList<InventorySection> Sections => Array.Empty<InventorySection>();
            public IReadOnlyCollection<InventoryItemInstance> Items => List;
            public bool TryGetItemAt(string sectionId, GridPosition position, out InventoryItemInstance item) { item = default; return false; }
            public event Action OnChanged { add { } remove { } }
        }

        private readonly List<GameObject> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created) UnityEngine.Object.DestroyImmediate(go);
            created.Clear();
        }

        private T Create<T>() where T : MonoBehaviour
        {
            var go = new GameObject(typeof(T).Name);
            created.Add(go);
            return go.AddComponent<T>();
        }

        [Test]
        public void InnRest_DefaultCostIsTen()
        {
            Assert.AreEqual(10, Create<PlaceholderInnRestService>().CostPerDay);
        }

        [Test]
        public void GuildBoard_ThreeQuests_AcceptOnce()
        {
            var board = Create<PlaceholderGuildQuestBoard>();
            board.ResolveDependencies(null);
            var changed = 0;
            board.Changed += () => changed++;

            Assert.AreEqual(3, board.Quests.Count);
            Assert.AreEqual("퀘스트이름1", board.Quests[0].Title);
            Assert.AreEqual("값 없음", board.Quests[0].Requester);
            Assert.IsTrue(board.TryAccept(board.Quests[1].Id));
            Assert.IsFalse(board.TryAccept(board.Quests[1].Id));
            Assert.IsFalse(board.TryAccept("missing"));
            Assert.IsTrue(board.IsAccepted(board.Quests[1].Id));
            Assert.IsFalse(board.IsAccepted(board.Quests[0].Id));
            Assert.AreEqual(1, changed);
        }

        [Test]
        public void Sale_EvaluatesCargoLastWagonAndPrice()
        {
            var owned = new FakeOwned();
            owned.Add("W1", FormationUnitKind.Wagon, 1000);
            owned.Add("W2", FormationUnitKind.Wagon, 801);
            owned.Add("F1", FormationUnitKind.Facility, 300);
            var cargo = new FakeCargo();
            cargo.List.Add(new InventoryItemInstance("i1", null, new GridPosition(0, 0), 0, false, "W1"));
            var sale = Create<PlaceholderCaravanAssetSaleService>();
            sale.Initialize(owned, cargo);

            Assert.AreEqual(CaravanAssetSaleCheck.HasCargo, sale.Evaluate("W1"));
            Assert.AreEqual(CaravanAssetSaleCheck.Available, sale.Evaluate("W2"));
            Assert.AreEqual(CaravanAssetSaleCheck.Available, sale.Evaluate("F1"));
            Assert.AreEqual(CaravanAssetSaleCheck.NotOwned, sale.Evaluate("X"));
            Assert.AreEqual(400, sale.SalePriceOf("W2"));

            owned.Assets.Remove("W1");
            Assert.AreEqual(CaravanAssetSaleCheck.LastWagon, sale.Evaluate("W2"));
            Assert.IsFalse(sale.TrySell("W2"));
        }
    }
}
