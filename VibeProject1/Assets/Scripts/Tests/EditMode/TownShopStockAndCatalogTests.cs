using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
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

        private PlaceholderTownShopStockProvider CreateProvider()
        {
            var itemTable = CreateItemTable(
                new ItemDefinitionEntry { Id = "general-a", FootprintWidth = 1, FootprintHeight = 1, Price = 100 },
                new ItemDefinitionEntry { Id = "specialty-a", FootprintWidth = 1, FootprintHeight = 1, Price = 900 },
                new ItemDefinitionEntry { Id = "general-b", FootprintWidth = 2, FootprintHeight = 1, Price = 200 });
            var kindTable = ScriptableObject.CreateInstance<TradeGoodsKindTableAsset>();
            SetPrivateField(kindTable, "entries", new List<TradeGoodsKindEntry>
            {
                new() { Id = "general-a", Kind = TradeGoodsKind.General },
                new() { Id = "specialty-a", Kind = TradeGoodsKind.Specialty },
                new() { Id = "general-b", Kind = TradeGoodsKind.General },
            });
            created.Add(kindTable);

            var gameObject = new GameObject(nameof(TownShopStockAndCatalogTests));
            created.Add(gameObject);
            var provider = gameObject.AddComponent<PlaceholderTownShopStockProvider>();
            SetPrivateField(provider, "tradeGoodsItemTable", itemTable);
            SetPrivateField(provider, "tradeGoodsKindTable", kindTable);
            return provider;
        }

        [Test]
        public void GetStock_TradeGoodsMarket_ListsGeneralGoodsInTableOrderWithPrice()
        {
            var stock = CreateProvider().GetStock(cityId: 1, TownFacilityIds.TradeGoodsMarket);

            Assert.AreEqual(2, stock.Count);
            Assert.AreEqual("general-a", stock[0].Definition.Id);
            Assert.AreEqual(100, stock[0].Price);
            Assert.AreEqual("general-b", stock[1].Definition.Id);
            Assert.AreEqual(200, stock[1].Price);
        }

        [Test]
        public void GetStock_IgnoresCityId()
        {
            var provider = CreateProvider();

            Assert.AreEqual(provider.GetStock(1, TownFacilityIds.TradeGoodsMarket).Count, provider.GetStock(7, TownFacilityIds.TradeGoodsMarket).Count);
        }

        [Test]
        public void GetStock_UnmappedFacility_ReturnsEmpty()
        {
            var provider = CreateProvider();

            LogAssert.Expect(LogType.Warning, new Regex("판매 대상이 정해지지 않았다"));
            Assert.AreEqual(0, provider.GetStock(1, TownFacilityIds.Blacksmith).Count);
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
