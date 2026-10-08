using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Game.Core;

namespace Game.Core.Tests
{
    public class TownShopStockAndCatalogTests
    {
        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in created) Object.DestroyImmediate(obj);
            created.Clear();
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(target, value);
        }

        private ItemDefinitionTableAsset CreateItemTable(params ItemDefinitionEntry[] entries)
        {
            var table = ScriptableObject.CreateInstance<ItemDefinitionTableAsset>();
            SetPrivateField(table, "entries", new List<ItemDefinitionEntry>(entries));
            created.Add(table);
            return table;
        }

        private class FakeStockReader : ITownStockReader
        {
            public event System.Action OnStockChanged;
            public IReadOnlyList<TownStockLine> GetLines(int cityId, TownStockCategory category)
                => cityId == 4 && category == TownStockCategory.TradeGoods
                    ? new[] { new TownStockLine("general-b", 3), new TownStockLine("general-a", 0) }
                    : new TownStockLine[0];
            public void Raise() => OnStockChanged?.Invoke();
        }

        private TownShopStockProvider CreateProvider(FakeStockReader reader)
        {
            var itemTable = CreateItemTable(
                new ItemDefinitionEntry { Id = "general-a", FootprintWidth = 1, FootprintHeight = 1, Price = 100 },
                new ItemDefinitionEntry { Id = "general-b", FootprintWidth = 2, FootprintHeight = 1, Price = 200 });
            var gameObject = new GameObject(nameof(TownShopStockAndCatalogTests));
            created.Add(gameObject);
            var provider = gameObject.AddComponent<TownShopStockProvider>();
            SetPrivateField(provider, "tradeGoodsItemTable", itemTable);
            provider.Bind(reader);
            return provider;
        }

        [Test]
        public void GetStock_TradeGoodsMarket_FollowsStockOrderWithRemaining()
        {
            var stock = CreateProvider(new FakeStockReader()).GetStock(4, TownFacilityIds.TradeGoodsMarket);

            Assert.AreEqual(2, stock.Count);
            Assert.AreEqual("general-b", stock[0].Definition.Id);
            Assert.AreEqual(200, stock[0].Price);
            Assert.AreEqual(3, stock[0].Remaining);
            Assert.AreEqual(0, stock[1].Remaining, "품절 행도 남는다");
        }

        [Test]
        public void GetStock_OtherFacilityOrCity_IsEmpty()
        {
            var provider = CreateProvider(new FakeStockReader());

            Assert.AreEqual(0, provider.GetStock(4, TownFacilityIds.Blacksmith).Count);
            Assert.AreEqual(0, provider.GetStock(1, TownFacilityIds.TradeGoodsMarket).Count);
        }

        [Test]
        public void OnStockChanged_IsForwarded()
        {
            var reader = new FakeStockReader();
            var provider = CreateProvider(reader);
            var raised = 0;
            provider.OnStockChanged += () => raised++;

            reader.Raise();

            Assert.AreEqual(1, raised);
        }

        [Test]
        public void CompositeItemCatalog_FindsItemsFromAllCatalogs()
        {
            var tradeGoods = new TableItemCatalog(CreateItemTable(new ItemDefinitionEntry { Id = "a", FootprintWidth = 1, FootprintHeight = 1 }), null);
            var misc = new TableItemCatalog(CreateItemTable(new ItemDefinitionEntry { Id = "gold-box", FootprintWidth = 1, FootprintHeight = 1 }), null);
            var composite = new CompositeItemCatalog(tradeGoods, misc);

            Assert.IsTrue(composite.TryGetDefinition("a", out _));
            Assert.IsTrue(composite.TryGetDefinition("gold-box", out _));
            Assert.IsFalse(composite.TryGetDefinition("missing", out _));
            Assert.AreEqual(2, composite.CatalogItems.Count);
        }
    }
}
