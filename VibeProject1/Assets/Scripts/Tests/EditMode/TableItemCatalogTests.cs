using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Game.Core;

namespace Game.Core.Tests
{
    public class TableItemCatalogTests
    {
        private ItemDefinitionTableAsset dataTable;
        private ItemStringTableAsset stringTable;

        [TearDown]
        public void TearDown()
        {
            if (dataTable != null) Object.DestroyImmediate(dataTable);
            if (stringTable != null) Object.DestroyImmediate(stringTable);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(target, value);
        }

        [Test]
        public void TryGetDefinition_ExistingId_ReturnsDefinitionWithLabel()
        {
            dataTable = ScriptableObject.CreateInstance<ItemDefinitionTableAsset>();
            SetPrivateField(dataTable, "entries", new List<ItemDefinitionEntry>
            {
                new() { Id = "iron-sword", FootprintWidth = 2, FootprintHeight = 1, Icon = null },
            });
            stringTable = ScriptableObject.CreateInstance<ItemStringTableAsset>();
            SetPrivateField(stringTable, "strings", new List<SlugLocalizedStringEntry>
            {
                new() { Id = "iron-sword", Ko = "철검" },
            });

            var catalog = new TableItemCatalog(dataTable, stringTable);

            var found = catalog.TryGetDefinition("iron-sword", out var definition);

            Assert.IsTrue(found);
            Assert.AreEqual("iron-sword", definition.Id);
            Assert.AreEqual("철검", definition.DisplayName);
            Assert.AreEqual(2, definition.FootprintWidth);
            Assert.AreEqual(1, definition.FootprintHeight);
        }

        [Test]
        public void TryGetDefinition_UnknownId_ReturnsFalse()
        {
            dataTable = ScriptableObject.CreateInstance<ItemDefinitionTableAsset>();
            SetPrivateField(dataTable, "entries", new List<ItemDefinitionEntry>());

            var catalog = new TableItemCatalog(dataTable, null);

            Assert.IsFalse(catalog.TryGetDefinition("does-not-exist", out _));
        }

        [Test]
        public void TryGetDefinition_MissingLabel_FallsBackToPlaceholderText()
        {
            dataTable = ScriptableObject.CreateInstance<ItemDefinitionTableAsset>();
            SetPrivateField(dataTable, "entries", new List<ItemDefinitionEntry>
            {
                new() { Id = "gold-box", FootprintWidth = 1, FootprintHeight = 1, Icon = null },
            });

            var catalog = new TableItemCatalog(dataTable, null); // String 테이블 미배선.

            catalog.TryGetDefinition("gold-box", out var definition);

            Assert.AreEqual("값 없음", definition.DisplayName);
        }

        [Test]
        public void TryGetDefinition_NullDataTable_ReturnsFalse_DoesNotThrow()
        {
            var catalog = new TableItemCatalog(null, null);

            Assert.IsFalse(catalog.TryGetDefinition("anything", out _));
            Assert.AreEqual(0, catalog.CatalogItems.Count);
        }

        [Test]
        public void CatalogItems_ReflectsAllEntries()
        {
            dataTable = ScriptableObject.CreateInstance<ItemDefinitionTableAsset>();
            SetPrivateField(dataTable, "entries", new List<ItemDefinitionEntry>
            {
                new() { Id = "a", FootprintWidth = 1, FootprintHeight = 1, Icon = null },
                new() { Id = "b", FootprintWidth = 1, FootprintHeight = 1, Icon = null },
            });

            var catalog = new TableItemCatalog(dataTable, null);

            Assert.AreEqual(2, catalog.CatalogItems.Count);
        }
    }
}
