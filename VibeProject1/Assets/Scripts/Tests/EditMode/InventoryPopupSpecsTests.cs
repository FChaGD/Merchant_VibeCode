using NUnit.Framework;

namespace Game.Core.Tests
{
    /// <summary>
    /// 인벤토리 팝업 스펙에서 파생하는 표시 순서·상시 호출 키·숙소 탭(Docs/Refactor/2026-10-26_공통.md D-2). 팝업을 늘릴 때 스펙 한 곳만 고치게 한다.
    /// </summary>
    public class InventoryPopupSpecsTests
    {
        [Test]
        public void All_OrderAndShortcuts()
        {
            var all = InventoryPopupSpecs.All;
            Assert.AreEqual(4, all.Count);
            Assert.AreSame(InventoryPopupSpecs.TradeGoods, all[0]);
            Assert.AreSame(InventoryPopupSpecs.Equipment, all[1]);
            Assert.AreSame(InventoryPopupSpecs.Consumable, all[2]);
            Assert.AreSame(InventoryPopupSpecs.PersonalItem, all[3]);
            Assert.AreEqual(GameInputActionIds.OpenTradeGoods, InventoryPopupSpecs.TradeGoods.ShortcutActionId);
            Assert.AreEqual(GameInputActionIds.OpenEquipment, InventoryPopupSpecs.Equipment.ShortcutActionId);
            Assert.AreEqual(GameInputActionIds.OpenConsumable, InventoryPopupSpecs.Consumable.ShortcutActionId);
            Assert.AreEqual(GameInputActionIds.OpenPersonalItem, InventoryPopupSpecs.PersonalItem.ShortcutActionId);
        }

        [Test]
        public void InnTabs_ExcludesTradeGoods()
        {
            var tabs = InventoryPopupSpecs.InnTabs;
            Assert.AreEqual(3, tabs.Count);
            Assert.AreSame(InventoryPopupSpecs.Equipment, tabs[0]);
            Assert.AreSame(InventoryPopupSpecs.Consumable, tabs[1]);
            Assert.AreSame(InventoryPopupSpecs.PersonalItem, tabs[2]);
        }
    }
}
