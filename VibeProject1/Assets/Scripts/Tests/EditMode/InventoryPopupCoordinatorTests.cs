using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Game.Core;

namespace Game.Core.Tests
{
    public class InventoryPopupCoordinatorTests
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

        private GameObject gameObject;
        private InventoryPopupCoordinator coordinator;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject(nameof(InventoryPopupCoordinatorTests));
            coordinator = gameObject.AddComponent<InventoryPopupCoordinator>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void Toggle_FirstCall_OpensPopup()
        {
            var panel = new FakePanel("A");
            coordinator.RegisterPopup(panel);

            coordinator.Toggle("A");

            Assert.AreEqual(1, panel.OpenCount);
            Assert.IsTrue(coordinator.IsOpen("A"));
        }

        [Test]
        public void Toggle_SecondCall_ClosesPopup()
        {
            var panel = new FakePanel("A");
            coordinator.RegisterPopup(panel);
            coordinator.Toggle("A");

            coordinator.Toggle("A");

            Assert.AreEqual(1, panel.CloseCount);
            Assert.IsFalse(coordinator.IsOpen("A"));
        }

        [Test]
        public void Toggle_TwoDifferentPopups_BothStayOpenIndependently()
        {
            var panelA = new FakePanel("A");
            var panelB = new FakePanel("B");
            coordinator.RegisterPopup(panelA);
            coordinator.RegisterPopup(panelB);

            coordinator.Toggle("A");
            coordinator.Toggle("B");

            Assert.IsTrue(coordinator.IsOpen("A"));
            Assert.IsTrue(coordinator.IsOpen("B"));
            Assert.AreEqual(0, panelA.CloseCount);
        }

        [Test]
        public void Toggle_UnregisteredId_LogsWarning_DoesNotThrow()
        {
            LogAssert.Expect(LogType.Warning, new Regex(".*등록되어 있지 않다.*"));

            Assert.DoesNotThrow(() => coordinator.Toggle("unknown"));
        }
    }
}
