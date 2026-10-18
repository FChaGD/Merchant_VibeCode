using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class EventEffectCommandsTests
    {
        private sealed class Host : IEventEffectHost
        {
            public readonly List<string> Lines = new();
            public Action PendingResult;
            public Action PendingCargo;
            public Action PendingVictory;
            public bool HasCargo = true;
            public bool IsRunning { get; set; } = true;
            public void PresentResult(string text, Action onDone) { Lines.Add(text); PendingResult = onDone; }
            public bool TryShowCargoRecovery(Action onDone) { if (!HasCargo) return false; PendingCargo = onDone; return true; }
            public void RequestBattle(Action onVictory) => PendingVictory = onVictory;
        }

        private sealed class Gold : IGoldEventAdjuster
        {
            public int Owned = 200;
            public int OwnedGold => Owned;
            public int Gain(int amount) { Owned += amount; return amount; }
            public int LoseUpTo(int amount) { var lost = Math.Min(amount, Owned); Owned -= lost; return lost; }
            public bool IsGoldItem(string itemId) => itemId == "gold-box";
        }

        private sealed class Clock : ITripClockAdjuster
        {
            public float Shortenable = 5f;
            public float AddedSeconds => 0f;
            public float ShortenDuration(float seconds) => Math.Min(seconds, Shortenable);
            public void RebaseDuration(float baseSeconds) { }
        }

        private sealed class RecordingRegistry : IDialogueCommandRegistry
        {
            public readonly Dictionary<string, Delegate> Commands = new();
            public readonly Dictionary<string, Delegate> Functions = new();
            public void AddCommand(string name, Delegate handler) => Commands.Add(name, handler);
            public void AddFunction(string name, Delegate implementation) => Functions.Add(name, implementation);
        }

        // 중첩 IEnumerator를 Unity 코루틴처럼 펼쳐 한 단계씩 진행한다. 끝나면 true.
        private static bool Step(Stack<IEnumerator> stack)
        {
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); continue; }
                if (top.Current is IEnumerator nested) { stack.Push(nested); continue; }
                return false;
            }
            return true;
        }

        private static Stack<IEnumerator> Start(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            return stack;
        }

        private readonly List<string> warnings = new();

        // NUnit은 한 클래스의 테스트가 인스턴스 하나를 공유한다 - 경고 기록이 테스트 사이에 남지 않게 비운다.
        [SetUp]
        public void ClearWarnings() => warnings.Clear();

        private EventEffectCommands Commands(Host host, Gold gold = null, EventCargoGoods goods = null, ITripClockAdjuster clock = null)
            => new(new EventEffectServices { Gold = gold ?? new Gold(), Goods = goods, Clock = clock, Warn = warnings.Add }, host);

        private static EventCargoGoods GoodsWith(GoldTestInventory inventory) => new(inventory, new CargoTestSettlement(inventory), _ => false, n => 0);

        [Test]
        public void Gold_Lose_ShowsActualAmount_WaitsForClick()
        {
            var host = new Host();
            var routine = Start(Commands(host).Gold(-300));
            Assert.IsFalse(Step(routine));
            CollectionAssert.AreEqual(new[] { "골드 −200" }, host.Lines);
            Assert.IsFalse(Step(routine), "클릭 전에는 끝나지 않는다");
            host.PendingResult();
            Assert.IsTrue(Step(routine));
        }

        [Test]
        public void Gold_LoseWithNone_ShowsNoGoldLost()
        {
            var host = new Host();
            var routine = Start(Commands(host, new Gold { Owned = 0 }).Gold(-100));
            Assert.IsFalse(Step(routine), "결과 줄 클릭 대기");
            CollectionAssert.AreEqual(new[] { "잃은 골드 없음" }, host.Lines);
        }

        [Test]
        public void Gold_Gain_ShowsPlus()
        {
            var host = new Host();
            Step(Start(Commands(host).Gold(100)));
            CollectionAssert.AreEqual(new[] { "골드 +100" }, host.Lines);
        }

        [Test]
        public void Delay_Negative_ShowsActualShortened()
        {
            var host = new Host();
            Step(Start(Commands(host, clock: new Clock()).Delay(-10f)));
            CollectionAssert.AreEqual(new[] { "이동 단축 5초" }, host.Lines);
        }

        [Test]
        public void Battle_WaitsUntilVictory()
        {
            var host = new Host();
            var routine = Start(Commands(host).Battle());
            Assert.IsFalse(Step(routine));
            Assert.IsNotNull(host.PendingVictory);
            Assert.IsFalse(Step(routine));
            host.PendingVictory();
            Assert.IsTrue(Step(routine));
        }

        [Test]
        public void Battle_StopsWaitingWhenEventEnded()
        {
            var host = new Host();
            var routine = Start(Commands(host).Battle());
            Step(routine);
            host.IsRunning = false;     // 도주·패배로 이벤트가 끝났다
            Assert.IsTrue(Step(routine));
        }

        [Test]
        public void GoodsAdd_NoRecoveryScreen_ContinuesAfterResult()
        {
            var inventory = new GoldTestInventory(GoldTestInventory.Section("A", 1, 1));
            inventory.AddDefinition(new GoldTestItem("wheat", 1, 1));
            var host = new Host { HasCargo = false };
            var routine = Start(Commands(host, goods: GoodsWith(inventory)).GoodsAdd("wheat", 2));
            Assert.IsFalse(Step(routine));
            CollectionAssert.AreEqual(new[] { "wheat ×2 획득" }, host.Lines, "정리 화면이 없어도 결과 줄로 넘어간다");
        }

        [Test]
        public void GoodsAdd_Overflow_ResultLineThenRecoveryScreen()
        {
            var inventory = new GoldTestInventory(GoldTestInventory.Section("A", 1, 1));
            inventory.AddDefinition(new GoldTestItem("wheat", 1, 1));
            var host = new Host();
            var routine = Start(Commands(host, goods: GoodsWith(inventory)).GoodsAdd("wheat", 2));
            Assert.IsFalse(Step(routine));
            CollectionAssert.AreEqual(new[] { "wheat ×2 획득" }, host.Lines, "결과 줄이 먼저");
            Assert.IsNull(host.PendingCargo, "결과 줄을 넘기기 전에는 정리 화면이 없다");
            host.PendingResult();
            Assert.IsFalse(Step(routine), "정리 화면이 닫힐 때까지 대기");
            Assert.IsNotNull(host.PendingCargo);
            host.PendingCargo();
            Assert.IsTrue(Step(routine));
        }

        [Test]
        public void GoodsRemove_NoneOwned_ShowsNoneLine()
        {
            var inventory = new GoldTestInventory(GoldTestInventory.Section("A", 1, 1));
            inventory.AddDefinition(new GoldTestItem("wheat", 1, 1));
            var host = new Host();
            Step(Start(Commands(host, goods: GoodsWith(inventory)).GoodsRemove("wheat", 1)));
            CollectionAssert.AreEqual(new[] { "wheat — 잃은 것 없음" }, host.Lines);
        }

        [Test]
        public void GoodsRemoveRandom_NoneOwned_ShowsNoneLine()
        {
            var inventory = new GoldTestInventory(GoldTestInventory.Section("A", 1, 1));
            var host = new Host();
            Step(Start(Commands(host, goods: GoodsWith(inventory)).GoodsRemove("random", 2)));
            CollectionAssert.AreEqual(new[] { "잃은 무역품 없음" }, host.Lines);
        }

        [Test]
        public void UnknownItem_WarnsAndSkips_NoLine()
        {
            var inventory = new GoldTestInventory(GoldTestInventory.Section("A", 1, 1));
            var host = new Host();
            Assert.IsTrue(Step(Start(Commands(host, goods: GoodsWith(inventory)).GoodsAdd("ghost", 1))));
            CollectionAssert.IsEmpty(host.Lines);
            Assert.AreEqual(1, warnings.Count);
        }

        [Test]
        public void AfterEventEnded_EffectsDoNothing()
        {
            // 도주·패배로 이벤트가 끝난 뒤 흘려보내는 대본의 효과는 적용하지 않는다.
            var host = new Host { IsRunning = false };
            var gold = new Gold();
            Assert.IsTrue(Step(Start(Commands(host, gold).Gold(-100))));
            Assert.AreEqual(200, gold.Owned);
            CollectionAssert.IsEmpty(host.Lines);
            Assert.IsTrue(Step(Start(Commands(host).Battle())));
            Assert.IsNull(host.PendingVictory, "끝난 이벤트에서는 전투를 요청하지 않는다");
        }

        [Test]
        public void MissingService_WarnsAndSkips()
        {
            var host = new Host();
            Assert.IsTrue(Step(Start(Commands(host).Join("Escort01"))));
            Assert.AreEqual(1, warnings.Count);
        }

        [Test]
        public void Register_MatchesSpecs()
        {
            var registry = new RecordingRegistry();
            Commands(new Host()).Register(registry);

            CollectionAssert.AreEquivalent(EventCommandSpecs.Commands.Select(s => s.Name), registry.Commands.Keys);
            foreach (var spec in EventCommandSpecs.Commands)
            {
                var parameters = registry.Commands[spec.Name].Method.GetParameters().Select(p => p.ParameterType);
                CollectionAssert.AreEqual(spec.Args.Select(EventCommandSpecs.ClrTypeOf), parameters, spec.Name);
            }

            CollectionAssert.AreEquivalent(EventCommandSpecs.Functions.Select(s => s.Name), registry.Functions.Keys);
            foreach (var spec in EventCommandSpecs.Functions)
            {
                var method = registry.Functions[spec.Name].Method;
                Assert.AreEqual(spec.Returns, method.ReturnType, spec.Name);
                CollectionAssert.AreEqual(spec.Parameters, method.GetParameters().Select(p => p.ParameterType), spec.Name);
            }
        }
    }
}
