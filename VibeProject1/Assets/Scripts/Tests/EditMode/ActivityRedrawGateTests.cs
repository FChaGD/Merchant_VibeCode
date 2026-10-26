using NUnit.Framework;

namespace Game.Core.Tests
{
    /// <summary>
    /// 정비창 활동 표시를 다시 그릴지 판정(Docs/Refactor/2026-10-26_공통.md O-2). 활동이 계속 0건이면 건너뛰되, 0건이 된 첫 프레임은 숨김 처리를 위해 그린다.
    /// </summary>
    public class ActivityRedrawGateTests
    {
        [Test]
        public void FirstCallWithZero_DoesNotRedraw()
        {
            var gate = new ActivityRedrawGate();
            Assert.IsFalse(gate.ShouldRedraw(0));
        }

        [Test]
        public void ActiveActivities_Redraw()
        {
            var gate = new ActivityRedrawGate();
            Assert.IsTrue(gate.ShouldRedraw(2));
            Assert.IsTrue(gate.ShouldRedraw(2));
        }

        [Test]
        public void ActivityRedrawGate_LastToZero_Redraws()
        {
            var gate = new ActivityRedrawGate();
            gate.ShouldRedraw(1);
            Assert.IsTrue(gate.ShouldRedraw(0), "마지막 활동이 끝난 프레임은 숨김을 위해 그린다");
            Assert.IsFalse(gate.ShouldRedraw(0));
        }
    }
}
