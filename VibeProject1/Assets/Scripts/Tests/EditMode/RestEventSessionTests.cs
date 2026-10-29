using System;
using System.Collections.Generic;
using NUnit.Framework;
using FakeScreens = Game.Core.Tests.EventPresentationTargetTests.FakeScreens;

namespace Game.Core.Tests
{
    /// <summary>휴식 이벤트 세션(설계 109 §5.3·§5.4·§7) - 시작·끝 알림·강제 종료 요청·디버그 강제 지정.</summary>
    public class RestEventSessionTests
    {
        private sealed class Content : IEncounterContentReader
        {
            public readonly List<EventEntry> List = new();
            public IReadOnlyList<EventEntry> Events => List;
            public IReadOnlyList<EventEntry> EventsAt(EventPlace place) => List.FindAll(e => e.Place == place);
            public IReadOnlyList<DialogueEntry> Dialogues => Array.Empty<DialogueEntry>();
            public IReadOnlyList<PersonEntry> People => Array.Empty<PersonEntry>();
            public bool TryGetEvent(string id, out EventEntry entry) { entry = List.Find(e => e.Id == id); return entry != null; }
            public bool TryGetDialogue(string id, out DialogueEntry entry) { entry = null; return false; }
            public bool TryGetPerson(string id, out PersonEntry entry) { entry = null; return false; }
        }

        private sealed class Runner : IDialogueScriptRunner
        {
            public string Node;
            public Action<bool> OnFinished;
            public bool IsRunning => OnFinished != null;
            public bool HasNode(string node) => true;
            public bool TryRun(string node, IDialoguePresentationTarget target, Action<bool> onFinished) { Node = node; OnFinished = onFinished; return true; }
            public void Stop() => Complete(false);
            public void Complete(bool completed) { var cb = OnFinished; OnFinished = null; cb?.Invoke(completed); }
        }

        private Content content;
        private Runner runner;
        private EncounterHistory history;
        private EventEffectCommandRouter router;
        private RestEventSession session;
        private float roll;

        [SetUp]
        public void SetUp()
        {
            content = new Content();
            content.List.Add(new EventEntry { Id = "T1", YarnNode = "T1_Node", Occurrence = EventOccurrence.Fixed, Place = EventPlace.Trip });
            content.List.Add(new EventEntry { Id = "RF", YarnNode = "RF_Node", Occurrence = EventOccurrence.Fixed, Place = EventPlace.Rest, Title = "확정 휴식" });
            content.List.Add(new EventEntry { Id = "RR", YarnNode = "RR_Node", Occurrence = EventOccurrence.Random, Place = EventPlace.Rest, Title = "랜덤 휴식",
                Conditions = EncounterConditionParser.Parse("flag:never") });
            runner = new Runner();
            history = new EncounterHistory();
            router = new EventEffectCommandRouter();
            roll = 0.99f;
            session = new RestEventSession(new RestEventSessionSources
            {
                Content = content,
                History = history,
                HistoryWriter = history,
                Runner = runner,
                Router = router,
                Random01 = () => roll,
            });
            session.BindScreens(new FakeScreens());
        }

        [Test]
        public void TryStartFixed_PicksRestEventOnly()
        {
            Assert.IsTrue(session.TryStartFixed(_ => { }));
            Assert.AreEqual("RF_Node", runner.Node);
        }

        [Test]
        public void Outcome_CarriesTitleAndSummary()
        {
            RestEventOutcome? outcome = null;
            session.TryStartFixed(o => outcome = o);
            router.PresentResult("골드 +10", () => { });
            router.PresentResult("밀 ×2 획득", () => { });
            runner.Complete(true);

            Assert.AreEqual("RF", outcome.Value.EventId);
            Assert.AreEqual("확정 휴식", outcome.Value.Title);
            Assert.AreEqual("골드 +10, 밀 ×2 획득", outcome.Value.ResultSummary);
            Assert.IsFalse(outcome.Value.ForcedEnd);
            Assert.IsFalse(session.IsRunning);
        }

        [Test]
        public void Outcome_StoppedScript_NotCompleted()
        {
            RestEventOutcome? outcome = null;
            session.TryStartFixed(o => outcome = o);
            Assert.IsTrue(outcome == null);
            runner.Complete(false);   // 대본 도중 실행기 중단
            Assert.IsFalse(outcome.Value.Completed);

            history.ResetTrip();
            session.TryStartFixed(o => outcome = o);
            runner.Complete(true);
            Assert.IsTrue(outcome.Value.Completed);
        }

        [Test]
        public void RestEndCommand_ReachesSession()
        {
            RestEventOutcome? outcome = null;
            session.TryStartFixed(o => outcome = o);
            Assert.IsTrue(router.IsRunning, "휴식 맥락이 라우터에 들어가 있다");
            session.RequestEnd();
            runner.Complete(true);
            Assert.IsTrue(outcome.Value.ForcedEnd);
        }

        [Test]
        public void ForcedEnd_ClearedAfterOutcome()
        {
            RestEventOutcome? outcome = null;
            session.TryStartFixed(o => outcome = o);
            session.RequestEnd();
            runner.Complete(true);

            history.ResetTrip();
            session.TryStartFixed(o => outcome = o);
            runner.Complete(true);
            Assert.IsFalse(outcome.Value.ForcedEnd, "앞 이벤트의 강제 종료 요청이 다음 이벤트로 새지 않는다");
        }

        [Test]
        public void TryStartFixed_SpanExhausted_False()
        {
            session.TryStartFixed(_ => { });
            runner.Complete(true);
            Assert.IsFalse(session.TryStartFixed(_ => { }), "반복 이벤트는 휴식 1회당 1번");
            session.BeginRestSpan();
            Assert.IsTrue(session.TryStartFixed(_ => { }), "다음 휴식에서 다시");
        }

        [Test]
        public void TryStartRandom_RespectsChance()
        {
            content.List[2].Conditions = new List<EncounterCondition>();
            roll = 0.5f;
            Assert.IsFalse(session.TryStartRandom(_ => { }));
            roll = 0.1f;
            Assert.IsTrue(session.TryStartRandom(_ => { }));
            Assert.AreEqual("RR_Node", runner.Node);
        }

        [Test]
        public void ForceNext_IgnoresConditions_OnceOnly()
        {
            Assert.IsFalse(session.ForceNext("T1"), "상행 이벤트는 지정할 수 없다");
            Assert.IsTrue(session.ForceNext("RR"));
            Assert.IsTrue(session.TryStartFixed(_ => { }));
            Assert.AreEqual("RR_Node", runner.Node, "조건(flag:never)을 무시하고 시작");
            runner.Complete(true);
            Assert.IsNull(session.ForcedNextId);

            session.BeginRestSpan();
            session.TryStartFixed(_ => { });
            Assert.AreEqual("RF_Node", runner.Node, "한 번 쓰면 지워진다");
        }

        [Test]
        public void Abort_NoOutcome_RouterCleared()
        {
            var called = 0;
            session.TryStartFixed(_ => called++);
            session.Abort();
            runner.Complete(true);   // Drain 뒤 늦은 대본 완료

            Assert.AreEqual(0, called);
            Assert.IsFalse(session.IsRunning);
            Assert.IsFalse(router.IsRunning);
            Assert.IsFalse(history.HasOccurred("RF"), "중단은 기록하지 않는다");
        }
    }
}
