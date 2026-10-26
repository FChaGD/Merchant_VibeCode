using System.Collections.Generic;
using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>버리기 경고 문구(Docs/설계/102번 §4.5, 기획 95).</summary>
    public class InventoryDiscardWarningTests
    {
        private sealed class Item : IInventoryItemDefinition
        {
            public Item(string id, string name) { Id = id; DisplayName = name; }
            public string Id { get; }
            public string DisplayName { get; }
            public string Description => string.Empty;
            public UnityEngine.Sprite Icon => null;
            public int FootprintWidth => 1;
            public int FootprintHeight => 1;
        }

        private sealed class GoldInfo : IGoldItemInfo
        {
            public bool IsGoldItem(string itemId) => itemId == "gold-box";
            public int GoldValuePerItem => 500;
        }

        private static readonly Item Salt = new("salt", "소금");
        private static readonly Item Silk = new("silk", "비단");
        private static readonly Item GoldBox = new("gold-box", "골드 상자");

        private static List<InventoryItemInstance> Items(params Item[] definitions)
        {
            var list = new List<InventoryItemInstance>();
            for (var i = 0; i < definitions.Length; i++) list.Add(new InventoryItemInstance("i" + i, definitions[i], new GridPosition(i, 0)));
            return list;
        }

        [Test]
        public void Compose_GroupsByNameInFirstOrder_OmitsTimesOne()
        {
            var text = InventoryDiscardWarning.Compose(Items(Silk, Salt, Silk, Silk), null);

            Assert.AreEqual("다음 아이템을 버립니다. 되돌릴 수 없습니다.\n비단 ×3, 소금", text);
        }

        [Test]
        public void Compose_GoldBoxes_AddsGoldLine()
        {
            var text = InventoryDiscardWarning.Compose(Items(GoldBox, Salt, GoldBox), new GoldInfo());

            Assert.AreEqual("다음 아이템을 버립니다. 되돌릴 수 없습니다.\n골드 상자 ×2, 소금\n골드 상자 2개(골드 1,000)가 사라집니다.", text);
        }

        [Test]
        public void Compose_NoGoldInfo_NoGoldLine()
        {
            var text = InventoryDiscardWarning.Compose(Items(GoldBox), null);

            Assert.AreEqual("다음 아이템을 버립니다. 되돌릴 수 없습니다.\n골드 상자", text);
        }

        [Test]
        public void Compose_NoGoldBoxes_NoGoldLine()
        {
            var text = InventoryDiscardWarning.Compose(Items(Salt), new GoldInfo());

            Assert.AreEqual("다음 아이템을 버립니다. 되돌릴 수 없습니다.\n소금", text);
        }
    }
}
