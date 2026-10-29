using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class EventEffectCommandRouterTests
    {
        private sealed class Host : IEventEffectHost
        {
            public readonly List<string> Lines = new();
            public bool IsRunning { get; set; } = true;
            public void PresentResult(string text, Action onDone) { Lines.Add(text); onDone?.Invoke(); }
            public bool TryShowCargoRecovery(Action onDone) => true;
            public void RequestBattle(Action onVictory) => onVictory?.Invoke();
        }

        private sealed class Gold : IGoldEventAdjuster
        {
            public int Owned = 100;
            public int OwnedGold => Owned;
            public int Gain(int amount) { Owned += amount; return amount; }
            public int LoseUpTo(int amount) { var lost = Math.Min(amount, Owned); Owned -= lost; return lost; }
            public bool IsGoldItem(string itemId) => false;
        }

        private sealed class Registry : IDialogueCommandRegistry
        {
            public readonly Dictionary<string, Delegate> Commands = new();
            public void AddCommand(string name, Delegate handler) => Commands.Add(name, handler);
            public void AddFunction(string name, Delegate implementation) { }
        }

        private static void RunToEnd(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); continue; }
                if (top.Current is IEnumerator nested) stack.Push(nested);
            }
        }

        private readonly List<string> warnings = new();
        private EventEffectCommandRouter router;
        private Registry registry;

        [SetUp]
        public void SetUp()
        {
            warnings.Clear();
            router = new EventEffectCommandRouter(warnings.Add);
            registry = new Registry();
            router.Register(registry);
        }

        private void RunGold(int amount) => RunToEnd(((Func<int, IEnumerator>)registry.Commands[EventCommandSpecs.Gold])(amount));

        [Test]
        public void NoContext_NotRunning()
        {
            Assert.IsFalse(router.IsRunning);
            var done = false;
            router.PresentResult("x", () => done = true);
            Assert.IsTrue(done, "맥락이 없으면 결과 줄은 즉시 완료");
            Assert.IsFalse(router.TryShowCargoRecovery(() => { }));
        }

        [Test]
        public void Enter_DelegatesToHost()
        {
            var host = new Host();
            var gold = new Gold();
            router.Enter(host, new EventEffectServices { Gold = gold });
            RunGold(30);
            Assert.AreEqual(130, gold.Owned);
            CollectionAssert.AreEqual(new[] { EventEffectFormat.Gold(30) }, host.Lines);
        }

        [Test]
        public void AfterExit_CommandsNoEffect()
        {
            var host = new Host();
            var gold = new Gold();
            router.Enter(host, new EventEffectServices { Gold = gold });
            router.Exit(host);
            RunGold(30);
            Assert.AreEqual(100, gold.Owned, "끝난 이벤트의 남은 대본은 효과가 없다");
            CollectionAssert.IsEmpty(host.Lines);
        }

        [Test]
        public void ExitOtherHost_Ignored()
        {
            var host = new Host();
            router.Enter(host, new EventEffectServices());
            router.Exit(new Host());
            Assert.IsTrue(router.IsRunning);
        }

        [Test]
        public void Enter_WhileActive_Replaces()
        {
            var first = new Host();
            var second = new Host();
            var gold = new Gold();
            router.Enter(first, new EventEffectServices());
            router.Enter(second, new EventEffectServices { Gold = gold });
            Assert.AreEqual(1, warnings.Count);
            RunGold(5);
            Assert.AreEqual(105, gold.Owned);
            CollectionAssert.IsEmpty(first.Lines);
            CollectionAssert.AreEqual(new[] { EventEffectFormat.Gold(5) }, second.Lines);
            router.Exit(first);
            Assert.IsTrue(router.IsRunning, "덮어써진 옛 진행자의 늦은 Exit는 지금 맥락을 지우지 않는다");
        }
    }
}
