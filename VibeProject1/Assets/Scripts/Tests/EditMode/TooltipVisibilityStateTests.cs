using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>툴팁 표시 판정(Docs/설계/100번 §4.1) - (호버 또는 길게 누름) 그리고 문구 있음.</summary>
    public class TooltipVisibilityStateTests
    {
        [TestCase(false, false, "사유", false)]
        [TestCase(true, false, "사유", true)]
        [TestCase(false, true, "사유", true)]
        [TestCase(true, true, "사유", true)]
        [TestCase(true, false, "", false)]
        [TestCase(false, true, null, false)]
        [TestCase(true, true, "", false)]
        public void IsVisible_ByInputs(bool hovering, bool longPressing, string text, bool expected)
        {
            var state = new TooltipVisibilityState();
            state.SetText(text);
            state.SetHovering(hovering);
            state.SetLongPressing(longPressing);
            Assert.AreEqual(expected, state.IsVisible);
        }

        [Test]
        public void ShowingThenTextCleared_HidesImmediately()
        {
            var state = new TooltipVisibilityState();
            state.SetText("사유");
            Assert.IsTrue(state.SetHovering(true));

            Assert.IsTrue(state.SetText(string.Empty));
            Assert.IsFalse(state.IsVisible);
        }

        [Test]
        public void ShowingThenTextChanged_NeedsApply()
        {
            var state = new TooltipVisibilityState();
            state.SetText("사유1");
            state.SetHovering(true);

            Assert.IsTrue(state.SetText("사유1\n사유2"));
            Assert.AreEqual("사유1\n사유2", state.Text);
            Assert.IsFalse(state.SetText("사유1\n사유2"));
        }

        [Test]
        public void HiddenTextChange_NoApply()
        {
            var state = new TooltipVisibilityState();
            state.SetText("사유1");
            Assert.IsFalse(state.SetText("사유2"));
        }

        [Test]
        public void HoverExitWhileLongPressing_StaysVisible()
        {
            var state = new TooltipVisibilityState();
            state.SetText("사유");
            state.SetHovering(true);
            state.SetLongPressing(true);

            Assert.IsFalse(state.SetHovering(false));
            Assert.IsTrue(state.IsVisible);
            Assert.IsTrue(state.SetLongPressing(false));
            Assert.IsFalse(state.IsVisible);
        }
    }
}
