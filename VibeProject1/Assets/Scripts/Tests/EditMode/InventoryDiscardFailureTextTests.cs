using NUnit.Framework;

namespace Game.Core.Tests
{
    /// <summary>
    /// 버리기 실패 문구를 방식별로 나눈다(Docs/Refactor/2026-10-26_공통.md D-6) - 선택 버리기는 다시 고르게 하고, 드롭 버리기는 아이템이 이미 사라졌음을 알린다.
    /// </summary>
    public class InventoryDiscardFailureTextTests
    {
        [Test]
        public void FailureText_ByMode()
        {
            Assert.AreEqual("버리지 못했습니다. 다시 선택하세요.", InventoryDiscardController.FailureText(wasSelecting: true));
            Assert.AreEqual("아이템이 이미 사라져 버리지 못했습니다.", InventoryDiscardController.FailureText(wasSelecting: false));
        }
    }
}
