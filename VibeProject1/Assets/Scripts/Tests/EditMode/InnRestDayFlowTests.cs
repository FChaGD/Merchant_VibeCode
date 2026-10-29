using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    /// <summary>숙소 휴식 하루 순서(설계 109 §4.2) - 대기 → 확정 → 랜덤 → 하루 처리, 미성립 종료, 중단 규칙.</summary>
    public class InnRestDayFlowTests
    {
        private sealed class FakeService : IInnRestService
        {
            public readonly List<string> Calls = new();
            public int PaidDaysLeft = int.MaxValue;   // 이만큼 결제 후 부족
            public bool CanSettle = true;
            public int CostPerDay => 10;
            public bool CanSettleDay => CanSettle;
            public void BeginRest(int plannedDays) => Calls.Add($"Begin:{plannedDays}");
            public RestDaySettlement SettleDay()
            {
                if (PaidDaysLeft <= 0) { Calls.Add("Settle:Fail"); return RestDaySettlement.InsufficientGold; }
                PaidDaysLeft--;
                Calls.Add("Settle");
                return RestDaySettlement.Paid;
            }
            public void EndRest(int restedDays) => Calls.Add($"End:{restedDays}");
        }

        private sealed class FakeRunner : IRestEventRunner
        {
            public readonly List<string> Calls = new();
            public bool FixedAvailable;
            public bool RandomAvailable;
            public Action<RestEventOutcome> Pending;
            public bool IsRunning => Pending != null;
            public void BeginRestSpan() => Calls.Add("Span");

            public bool TryStartFixed(Action<RestEventOutcome> onDone) => Start("Fixed", FixedAvailable, onDone);
            public bool TryStartRandom(Action<RestEventOutcome> onDone) => Start("Random", RandomAvailable, onDone);

            public void Abort()
            {
                Calls.Add("Abort");
                Pending = null;
            }

            public void Finish(string title, string summary = "", bool forced = false, bool completed = true)
            {
                var cb = Pending;
                Pending = null;
                cb(new RestEventOutcome(title, title, summary, forced, completed));
            }

            private bool Start(string kind, bool available, Action<RestEventOutcome> onDone)
            {
                Calls.Add(kind);
                if (!available) return false;
                Pending = onDone;
                return true;
            }
        }

        private FakeService service;
        private FakeRunner runner;

        [SetUp]
        public void SetUp()
        {
            service = new FakeService();
            runner = new FakeRunner();
        }

        private InnRestDayFlow Start(int days, bool withRunner = true)
        {
            var flow = new InnRestDayFlow(days, service, withRunner ? runner : null);
            flow.Start();
            return flow;
        }

        [Test]
        public void NoRunner_CompletesAllDays()
        {
            var flow = Start(3, withRunner: false);
            for (var i = 0; i < 3; i++) flow.OnWaitFinished();

            Assert.AreEqual(RestEndReason.Completed, flow.EndReason);
            Assert.AreEqual(3, flow.SettledDays);
            Assert.AreEqual(30, flow.PaidTotal);
            CollectionAssert.AreEqual(new[] { "Begin:3", "Settle", "Settle", "Settle", "End:3" }, service.Calls);
        }

        [Test]
        public void Order_FixedThenRandomThenSettle()
        {
            runner.FixedAvailable = true;
            runner.RandomAvailable = true;
            var flow = Start(2);
            flow.OnWaitFinished();
            Assert.AreEqual(RestDayPhase.FixedEvent, flow.Phase);
            runner.Finish("F");
            Assert.AreEqual(RestDayPhase.RandomEvent, flow.Phase);
            Assert.AreEqual(0, flow.SettledDays, "랜덤 이벤트 끝 전에는 결제하지 않는다");
            runner.Finish("R");
            Assert.AreEqual(RestDayPhase.Waiting, flow.Phase);
            Assert.AreEqual(1, flow.SettledDays);
            CollectionAssert.AreEqual(new[] { "Span", "Fixed", "Random" }, runner.Calls);
        }

        [Test]
        public void FixedForcedEnd_SkipsRandomAndSettle()
        {
            runner.FixedAvailable = true;
            runner.RandomAvailable = true;
            var flow = Start(3);
            flow.OnWaitFinished();
            runner.Finish("F", forced: true);

            Assert.AreEqual(RestEndReason.ForcedByEvent, flow.EndReason);
            Assert.AreEqual(0, flow.SettledDays);
            CollectionAssert.DoesNotContain(runner.Calls, "Random");
            CollectionAssert.AreEqual(new[] { "Begin:3", "End:0" }, service.Calls);
        }

        [Test]
        public void RandomForcedEnd_NoSettle()
        {
            runner.RandomAvailable = true;
            var flow = Start(3);
            flow.OnWaitFinished();
            runner.Finish("R", forced: true);

            Assert.AreEqual(RestEndReason.ForcedByEvent, flow.EndReason);
            CollectionAssert.DoesNotContain(service.Calls, "Settle");
        }

        [Test]
        public void Insufficient_EndsWithoutSettle()
        {
            service.PaidDaysLeft = 1;
            var flow = Start(3, withRunner: false);
            flow.OnWaitFinished();
            flow.OnWaitFinished();

            Assert.AreEqual(RestEndReason.InsufficientGold, flow.EndReason);
            Assert.AreEqual(1, flow.SettledDays);
            Assert.AreEqual(10, flow.PaidTotal);
            CollectionAssert.AreEqual(new[] { "Begin:3", "Settle", "Settle:Fail", "End:1" }, service.Calls);
        }

        [Test]
        public void Stop_OnlyWhileWaiting()
        {
            runner.FixedAvailable = true;
            var flow = Start(3);
            flow.OnWaitFinished();
            Assert.IsFalse(flow.CanStop);
            Assert.IsFalse(flow.Stop());
            runner.Finish("F");
            Assert.IsTrue(flow.CanStop);
            Assert.IsTrue(flow.Stop());
            Assert.AreEqual(RestEndReason.Stopped, flow.EndReason);
            Assert.AreEqual(1, flow.SettledDays);
        }

        [Test]
        public void RunnerStartFails_TreatedAsNoEvent()
        {
            var flow = Start(1);
            flow.OnWaitFinished();

            Assert.AreEqual(RestEndReason.Completed, flow.EndReason);
            CollectionAssert.AreEqual(new[] { "Span", "Fixed", "Random" }, runner.Calls);
            CollectionAssert.IsEmpty(flow.Records);
        }

        [Test]
        public void Abort_DuringEvent_RequestsRunnerAbort()
        {
            runner.FixedAvailable = true;
            var flow = Start(3);
            flow.OnWaitFinished();
            flow.Abort();

            CollectionAssert.Contains(runner.Calls, "Abort");
            Assert.AreEqual(RestEndReason.Stopped, flow.EndReason);
            Assert.AreEqual(0, flow.SettledDays);
        }

        [Test]
        public void Abort_WhileWaiting_NoRunnerAbort()
        {
            var flow = Start(3);
            flow.Abort();
            CollectionAssert.DoesNotContain(runner.Calls, "Abort");
            Assert.AreEqual(RestEndReason.Stopped, flow.EndReason);
        }

        [Test]
        public void LateOutcomeAfterAbort_Ignored()
        {
            runner.FixedAvailable = true;
            var flow = Start(3);
            flow.OnWaitFinished();
            var stale = runner.Pending;
            flow.Abort();
            stale(new RestEventOutcome("F", "F", "", false));

            Assert.AreEqual(RestDayPhase.Ended, flow.Phase);
            CollectionAssert.IsEmpty(flow.Records);
        }

        [Test]
        public void Records_DayIsSettledPlusOne()
        {
            runner.FixedAvailable = true;
            var flow = Start(3);
            flow.OnWaitFinished();
            runner.Finish("첫날", "골드 +10");
            flow.OnWaitFinished();
            runner.Finish("둘째 날");

            Assert.AreEqual(2, flow.Records.Count);
            Assert.AreEqual(1, flow.Records[0].Day);
            Assert.AreEqual("골드 +10", flow.Records[0].ResultSummary);
            Assert.AreEqual(2, flow.Records[1].Day);
        }

        [Test]
        public void EndRest_CalledOnceWithSettledDays()
        {
            var flow = Start(2, withRunner: false);
            flow.OnWaitFinished();
            flow.Stop();
            flow.Abort();
            flow.Stop();

            Assert.AreEqual(1, service.Calls.FindAll(c => c.StartsWith("End")).Count);
            CollectionAssert.Contains(service.Calls, "End:1");
        }

        [Test]
        public void Changed_RaisedOnPhaseChange()
        {
            runner.FixedAvailable = true;
            var flow = new InnRestDayFlow(2, service, runner);
            var phases = new List<RestDayPhase>();
            flow.Changed += () => phases.Add(flow.Phase);
            flow.Start();
            flow.OnWaitFinished();
            runner.Finish("F");

            CollectionAssert.AreEqual(new[] { RestDayPhase.Waiting, RestDayPhase.FixedEvent, RestDayPhase.Waiting }, phases);
        }

        [Test]
        public void ServiceCannotSettle_NoSettleCall_EndsStopped()
        {
            var flow = Start(2, withRunner: false);
            service.CanSettle = false;   // 밖에서 휴식이 끝난 상태
            flow.OnWaitFinished();

            Assert.AreEqual(RestEndReason.Stopped, flow.EndReason);
            Assert.AreEqual(0, flow.SettledDays);
            CollectionAssert.DoesNotContain(service.Calls, "Settle");
        }

        [Test]
        public void IncompleteEvent_NoRecord_EndsStopped()
        {
            runner.FixedAvailable = true;
            runner.RandomAvailable = true;
            var flow = Start(2);
            flow.OnWaitFinished();
            runner.Finish("F", completed: false);

            Assert.AreEqual(RestEndReason.Stopped, flow.EndReason);
            Assert.IsEmpty(flow.Records, "끊긴 이벤트는 기록하지 않는다");
            CollectionAssert.DoesNotContain(runner.Calls, "Random", "다음 단계로 가지 않는다");
            CollectionAssert.DoesNotContain(service.Calls, "Settle");
        }

        [Test]
        public void IsEventRunning_TrueOnlyDuringEvent()
        {
            runner.FixedAvailable = true;
            runner.RandomAvailable = true;
            var flow = Start(2);
            Assert.IsFalse(flow.IsEventRunning);
            flow.OnWaitFinished();
            Assert.IsTrue(flow.IsEventRunning, "확정 이벤트 중");
            runner.Finish("F");
            Assert.IsTrue(flow.IsEventRunning, "이어서 랜덤 이벤트 중");
            runner.Finish("R");
            Assert.IsFalse(flow.IsEventRunning, "하루 처리 뒤 대기");
            flow.OnWaitFinished();
            runner.Finish("F", forced: true);
            Assert.IsFalse(flow.IsEventRunning, "종료 후");
        }
    }
}
