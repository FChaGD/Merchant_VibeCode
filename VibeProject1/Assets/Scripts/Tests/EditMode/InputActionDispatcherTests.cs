using System.Linq;
using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>
    /// 입력 구독 분배(Docs/설계/99번 §4.2). Unity 입력 없이 Id 문자열로 직접 분배해 검증한다.
    /// </summary>
    public class InputActionDispatcherTests
    {
        private bool focused;
        private InputActionDispatcher dispatcher;

        [SetUp]
        public void SetUp()
        {
            focused = false;
            dispatcher = new InputActionDispatcher(id => GameInputActionIds.All.Contains(id), () => focused);
        }

        [Test]
        public void Dispatch_InvokesSubscriber()
        {
            int count = 0;
            dispatcher.Subscribe(GameInputActionIds.OpenTradeGoods, () => count++);

            dispatcher.Dispatch(GameInputActionIds.OpenTradeGoods);

            Assert.AreEqual(1, count);
        }

        [Test]
        public void Dispose_StopsDelivery()
        {
            int count = 0;
            var handle = dispatcher.Subscribe(GameInputActionIds.OpenTradeGoods, () => count++);

            handle.Dispose();
            dispatcher.Dispatch(GameInputActionIds.OpenTradeGoods);

            Assert.AreEqual(0, count);
        }

        [Test]
        public void MultipleSubscribers_SameId_AllInvoked()
        {
            int first = 0, second = 0;
            dispatcher.Subscribe(GameInputActionIds.OpenTradeGoods, () => first++);
            dispatcher.Subscribe(GameInputActionIds.OpenTradeGoods, () => second++);

            dispatcher.Dispatch(GameInputActionIds.OpenTradeGoods);

            Assert.AreEqual(1, first);
            Assert.AreEqual(1, second);
        }

        [Test]
        public void UnsubscribeInsideCallback_IsSafe()
        {
            int first = 0, second = 0;
            System.IDisposable secondHandle = null;
            dispatcher.Subscribe(GameInputActionIds.OpenTradeGoods, () => { first++; secondHandle.Dispose(); });
            secondHandle = dispatcher.Subscribe(GameInputActionIds.OpenTradeGoods, () => second++);

            Assert.DoesNotThrow(() => dispatcher.Dispatch(GameInputActionIds.OpenTradeGoods));
            Assert.AreEqual(1, first);
            Assert.AreEqual(1, second);

            dispatcher.Dispatch(GameInputActionIds.OpenTradeGoods);
            Assert.AreEqual(2, first);
            Assert.AreEqual(1, second);
        }

        [Test]
        public void BlockedMap_WhileTextFocused_NotDelivered()
        {
            int shortcut = 0, press = 0;
            dispatcher.Subscribe(GameInputActionIds.OpenTradeGoods, () => shortcut++);
            dispatcher.Subscribe(GameInputActionIds.PointerPress, () => press++);
            focused = true;

            dispatcher.Dispatch(GameInputActionIds.OpenTradeGoods);
            dispatcher.Dispatch(GameInputActionIds.PointerPress);

            Assert.AreEqual(0, shortcut);
            Assert.AreEqual(1, press);
        }

        [Test]
        public void UnknownId_ReturnsNoOpHandle()
        {
            int count = 0;
            var handle = dispatcher.Subscribe("Nope/Nope", () => count++);

            Assert.IsNotNull(handle);
            Assert.DoesNotThrow(() => handle.Dispose());
            dispatcher.Dispatch("Nope/Nope");
            Assert.AreEqual(0, count);
        }

        [Test]
        public void DoubleDispose_IsSafe()
        {
            int count = 0;
            var handle = dispatcher.Subscribe(GameInputActionIds.OpenTradeGoods, () => count++);

            handle.Dispose();
            Assert.DoesNotThrow(() => handle.Dispose());
            dispatcher.Dispatch(GameInputActionIds.OpenTradeGoods);
            Assert.AreEqual(0, count);
        }
    }
}
