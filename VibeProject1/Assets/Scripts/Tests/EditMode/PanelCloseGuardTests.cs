using NUnit.Framework;
using UnityEngine;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>
    /// PanelChannel의 닫기 차단(Docs/설계/50번 §6.3)을 검증한다. PanelChannel은 internal이라, 같은 채널을 그대로 쓰는
    /// 공개 구현체 BattleTestPanelHost를 통해 확인한다.
    /// </summary>
    public class PanelCloseGuardTests
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

        private class GuardedPanel : FakePanel, IPanelCloseGuard
        {
            public bool AllowClose { get; set; }

            public GuardedPanel(string panelId) : base(panelId)
            {
            }

            public bool TryPrepareClose() => AllowClose;
        }

        private GameObject gameObject;
        private BattleTestPanelHost host;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject(nameof(PanelCloseGuardTests));
            host = gameObject.AddComponent<BattleTestPanelHost>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void Close_GuardRejects_KeepsPanelOpen()
        {
            var panel = new GuardedPanel("A") { AllowClose = false };
            host.RegisterPopupPanel(panel);
            host.Open("A");

            host.Close("A");

            Assert.AreEqual(0, panel.CloseCount);
            Assert.IsTrue(host.IsModalPopupOpen);
        }

        [Test]
        public void Close_GuardAllows_ClosesPanel()
        {
            var panel = new GuardedPanel("A") { AllowClose = true };
            host.RegisterPopupPanel(panel);
            host.Open("A");

            host.Close("A");

            Assert.AreEqual(1, panel.CloseCount);
            Assert.IsFalse(host.IsModalPopupOpen);
        }

        [Test]
        public void Close_UnguardedPanel_ClosesAsBefore()
        {
            var panel = new FakePanel("A");
            host.RegisterPopupPanel(panel);
            host.Open("A");

            host.Close("A");

            Assert.AreEqual(1, panel.CloseCount);
        }
    }
}
