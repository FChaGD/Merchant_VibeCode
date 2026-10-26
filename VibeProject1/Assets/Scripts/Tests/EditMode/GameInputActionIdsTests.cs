using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>
    /// 액션 Id 상수의 텍스트 입력 차단 맵 판정(Docs/설계/99번 §4.1). 팝업 → 단축키 대응은 팝업 스펙으로 옮겼다(InventoryPopupSpecsTests).
    /// </summary>
    public class GameInputActionIdsTests
    {
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
