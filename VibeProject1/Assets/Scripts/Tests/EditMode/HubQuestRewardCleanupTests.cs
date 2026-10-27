using System;
using NUnit.Framework;

namespace Game.Core.Tests
{
    /// <summary>Hub 퀘스트 보상 정리 - 무역품 인벤토리 팝업 자동 열기(Docs/설계/107번 §8.3).</summary>
    public class HubQuestRewardCleanupTests
    {
        private sealed class Source : IQuestRewardCleanupSource
        {
            public bool HasPendingCleanup { get; set; }
            public void ClearPendingCleanup() => HasPendingCleanup = false;
            public event Action RewardStaged;
            public void Stage()
            {
                HasPendingCleanup = true;
                RewardStaged?.Invoke();
            }
        }

        private sealed class Visibility : IUIVisibilitySignal
        {
            public bool IsAtRootDepth => true;
            public event Action<bool> RootDepthChanged { add { } remove { } }
            public bool IsModalPopupOpen { get; private set; }
            public event Action<bool> ModalPopupOpenChanged;
            public bool IsScreenOpen { get; private set; }
            public event Action<bool> ScreenOpenChanged;

            public void SetScreen(bool open)
            {
                IsScreenOpen = open;
                ScreenOpenChanged?.Invoke(open);
            }

            public void SetModal(bool open)
            {
                IsModalPopupOpen = open;
                ModalPopupOpenChanged?.Invoke(open);
            }
        }

        private Source source;
        private Visibility visibility;
        private int opened;

        [SetUp]
        public void SetUp()
        {
            source = new Source();
            visibility = new Visibility();
            opened = 0;
        }

        private HubQuestRewardCleanup Create() => new(source, visibility, () => opened++);

        [Test]
        public void Idle_OpensPopupAndClearsPending()
        {
            using var cleanup = Create();
            source.Stage();
            Assert.AreEqual(1, opened);
            Assert.IsFalse(source.HasPendingCleanup);
        }

        [Test]
        public void ScreenOpen_WaitsUntilClosed()
        {
            using var cleanup = Create();
            visibility.SetScreen(true);
            source.Stage();
            Assert.AreEqual(0, opened, "시설 화면이 열린 동안은 열지 않는다");
            visibility.SetModal(true);
            visibility.SetScreen(false);
            Assert.AreEqual(0, opened, "모달도 열려 있다");
            visibility.SetModal(false);
            Assert.AreEqual(1, opened);
            Assert.IsFalse(source.HasPendingCleanup);
        }

        [Test]
        public void Disposed_IgnoresSignals()
        {
            var cleanup = Create();
            cleanup.Dispose();
            source.Stage();
            Assert.AreEqual(0, opened);
            Assert.IsTrue(source.HasPendingCleanup);
        }
    }
}
