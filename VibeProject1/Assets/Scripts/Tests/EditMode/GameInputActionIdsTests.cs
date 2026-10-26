using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>
    /// 액션 Id 상수의 팝업 대응·텍스트 입력 차단 맵 판정(Docs/설계/99번 §4.1).
    /// </summary>
    public class GameInputActionIdsTests
    {
        [Test]
        public void ForInventoryPopup_MapsFourPopups()
        {
            Assert.AreEqual(GameInputActionIds.OpenTradeGoods, GameInputActionIds.ForInventoryPopup(InventoryPopupIds.TradeGoods));
            Assert.AreEqual(GameInputActionIds.OpenEquipment, GameInputActionIds.ForInventoryPopup(InventoryPopupIds.Equipment));
            Assert.AreEqual(GameInputActionIds.OpenConsumable, GameInputActionIds.ForInventoryPopup(InventoryPopupIds.Consumable));
            Assert.AreEqual(GameInputActionIds.OpenPersonalItem, GameInputActionIds.ForInventoryPopup(InventoryPopupIds.PersonalItem));
            Assert.IsNull(GameInputActionIds.ForInventoryPopup("Unknown"));
        }

        [Test]
        public void IsBlockedDuringTextInput_ByMap()
        {
            Assert.IsTrue(GameInputActionIds.IsBlockedDuringTextInput(GameInputActionIds.OpenTradeGoods));
            Assert.IsTrue(GameInputActionIds.IsBlockedDuringTextInput(GameInputActionIds.RotateHeldItem));
            Assert.IsFalse(GameInputActionIds.IsBlockedDuringTextInput(GameInputActionIds.PointerPress));
            Assert.IsFalse(GameInputActionIds.IsBlockedDuringTextInput(null));
        }
    }
}
