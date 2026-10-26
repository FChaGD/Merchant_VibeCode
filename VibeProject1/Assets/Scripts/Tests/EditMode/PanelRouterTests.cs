using NUnit.Framework;

namespace Game.Core.Tests
{
    /// <summary>
    /// 패널 종류별 채널(depth·시설 화면·모달) 라우팅과 두 축 신호(Docs/Refactor/2026-10-26_공통.md B-1·B-2). 시설 화면 위에 모달이 열려도
    /// 시설 화면이 닫히지 않아야 하는 것이 시설 화면 채널을 따로 둔 이유다.
    /// </summary>
    public class PanelRouterTests
    {
        private class FakePanel : IUIPanel
        {
            public string PanelId { get; }
            public int OpenCount { get; private set; }
            public int CloseCount { get; private set; }

            public FakePanel(string panelId) => PanelId = panelId;
            public void Open() => OpenCount++;
            public void Close() => CloseCount++;
        }

        [Test]
        public void OpenScreen_FromDepth_KeepsDepthOpen_RaisesScreenSignal()
        {
            var router = new PanelRouter();
            var depth = new FakePanel("A");
            var screen = new FakePanel("S");
            router.RegisterDepthPanel(depth);
            router.RegisterScreenPanel(screen);
            var screenSignals = 0;
            router.ScreenOpenChanged += open => { if (open) screenSignals++; };

            router.Open("A");
            router.Open("S");

            Assert.AreEqual(0, depth.CloseCount);
            Assert.IsTrue(router.IsScreenOpen);
            Assert.IsFalse(router.IsAtRootDepth);
            Assert.AreEqual(1, screenSignals);
        }

        [Test]
        public void ModalOverScreen_KeepsScreenOpen()
        {
            var router = new PanelRouter();
            var screen = new FakePanel("S");
            var modal = new FakePanel("M");
            router.RegisterScreenPanel(screen);
            router.RegisterPopupPanel(modal);

            router.Open("S");
            router.Open("M");
            Assert.AreEqual(0, screen.CloseCount, "모달이 열려도 시설 화면은 닫히지 않는다");

            router.Close("M");
            Assert.AreEqual(1, screen.OpenCount, "모달을 닫아도 시설 화면을 다시 열지 않는다(상태 유지)");
            Assert.IsTrue(router.IsScreenOpen);
            Assert.IsFalse(router.IsModalPopupOpen);
        }

        [Test]
        public void SecondScreen_HidesFirst_CloseReturns()
        {
            var router = new PanelRouter();
            var first = new FakePanel("S1");
            var second = new FakePanel("S2");
            router.RegisterScreenPanel(first);
            router.RegisterScreenPanel(second);

            router.Open("S1");
            router.Open("S2");
            Assert.AreEqual(1, first.CloseCount);

            router.Close("S2");
            Assert.AreEqual(2, first.OpenCount);
        }

        [Test]
        public void Reset_ClearsAllChannels()
        {
            var router = new PanelRouter();
            var depth = new FakePanel("A");
            var first = new FakePanel("S1");
            var second = new FakePanel("S2");
            var modal = new FakePanel("M");
            router.RegisterDepthPanel(depth);
            router.RegisterScreenPanel(first);
            router.RegisterScreenPanel(second);
            router.RegisterPopupPanel(modal);
            router.Open("A");
            router.Open("S2");
            router.Open("M");

            router.Reset();

            Assert.IsTrue(router.IsAtRootDepth);
            Assert.IsFalse(router.IsScreenOpen);
            Assert.IsFalse(router.IsModalPopupOpen);

            // 이전 기록(S2 활성)이 남아 있으면 S1을 열 때 S2를 숨기고 복귀 대상으로 기록한다 - 새 기록만으로 동작해야 한다.
            router.Open("S1");
            Assert.AreEqual(0, second.CloseCount);
            router.Close("S1");
            Assert.AreEqual(1, second.OpenCount, "지워진 기록의 S2로 복귀하지 않는다");
        }

        [Test]
        public void UnknownId_IsIgnored()
        {
            var router = new PanelRouter();
            var screen = new FakePanel("S");
            router.RegisterScreenPanel(screen);

            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            router.Open("Nope");
            router.Close("Nope");

            Assert.AreEqual(0, screen.OpenCount);
            Assert.IsFalse(router.IsScreenOpen);
        }
    }
}
