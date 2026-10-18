using System;
using System.Collections.Generic;
using NUnit.Framework;
using FakeScreens = Game.Core.Tests.EventPresentationTargetTests.FakeScreens;

namespace Game.Core.Tests
{
    public class EventEncounterDirectorTests
    {
        private sealed class Content : IEncounterContentReader
        {
            public readonly List<EventEntry> List = new();
            public IReadOnlyList<EventEntry> Events => List;
            public IReadOnlyList<DialogueEntry> Dialogues => Array.Empty<DialogueEntry>();
            public IReadOnlyList<PersonEntry> People => Array.Empty<PersonEntry>();
            public bool TryGetEvent(string id, out EventEntry entry) { entry = List.Find(e => e.Id == id); return entry != null; }
            public bool TryGetDialogue(string id, out DialogueEntry entry) { entry = null; return false; }
            public bool TryGetPerson(string id, out PersonEntry entry) { entry = null; return false; }
        }

        private sealed class Runner : IDialogueScriptRunner
        {
            public bool Accept = true;
            public string Node;
            public IDialoguePresentationTarget Target;
            public Action<bool> OnFinished;
            public int StopCount;
            public bool IsRunning => OnFinished != null;
            public bool HasNode(string node) => true;

            public bool TryRun(string node, IDialoguePresentationTarget target, Action<bool> onFinished)
            {
                if (!Accept) return false;
                Node = node;
                Target = target;
                OnFinished = onFinished;
                return true;
            }

            public void Stop()
            {
                StopCount++;
                Complete(false);   // 실제 실행기처럼 중단도 완료 콜백을 한 번 부른다
            }

            public void Complete(bool completed)
            {
                var cb = OnFinished;
                OnFinished = null;
                cb?.Invoke(completed);
            }
        }

        private sealed class Halt : ITripHaltControl
        {
            public readonly HashSet<TripHaltReason> Held = new();
            public bool IsHalted => Held.Count > 0;
            public bool IsHeld(TripHaltReason reason) => Held.Contains(reason);
            public event Action HaltStarted { add { } remove { } }
            public event Action HaltEnded { add { } remove { } }
            public void Acquire(TripHaltReason reason) => Held.Add(reason);
            public void Release(TripHaltReason reason) => Held.Remove(reason);
            public void ClearAll() => Held.Clear();
        }

        private sealed class Sink : ITripMilestoneSink
        {
            public readonly List<TripMilestone> Raised = new();
            public void Raise(TripMilestone milestone) => Raised.Add(milestone);
        }

        private sealed class Battle : IEventBattleRequester
        {
            public int Requests;
            public void RequestEventBattle() => Requests++;
        }

        private sealed class UIHide : IFieldUIHideControl
        {
            public readonly List<string> Calls = new();
            public bool Hidden;
            public void Acquire(FieldUIHideReason reason) { Hidden = true; Calls.Add("Hide"); }
            public void Release(FieldUIHideReason reason) { Hidden = false; Calls.Add("Show"); }
        }

        private UIHide uiHide;
        private Content content;
        private Runner runner;
        private Halt halt;
        private Sink sink;
        private Battle battle;
        private EncounterHistory history;
        private FakeScreens screens;

        // NUnit은 테스트 사이에 인스턴스를 공유한다 - 가짜 객체를 테스트마다 새로 만든다.
        [SetUp]
        public void SetUp()
        {
            content = new Content();
            runner = new Runner();
            halt = new Halt();
            sink = new Sink();
            battle = new Battle();
            history = new EncounterHistory();
            screens = new FakeScreens();
            uiHide = new UIHide();
        }

        private EventEncounterDirector Make(bool withHide = true)
        {
            content.List.Add(new EventEntry { Id = "E1", YarnNode = "E1_Node", Occurrence = EventOccurrence.Fixed });
            var director = new EventEncounterDirector(new EventEncounterSources
            {
                Content = content,
                History = history,
                HistoryWriter = history,
                Runner = runner,
                Halt = halt,
                Milestones = sink,
                Battle = battle,
                UIHide = withHide ? uiHide : null,
            });
            director.BindScreens(screens);
            return director;
        }

        [Test]
        public void UIHide_StartHides_BattleShows_VictoryHidesAgain_EndShows()
        {
            var director = Make();
            director.OnEventCheck();
            Assert.IsTrue(uiHide.Hidden);
            director.RequestBattle(() => { });
            Assert.IsFalse(uiHide.Hidden, "전투 동안 보인다");
            director.OnEventBattleFinished(EventBattleOutcome.Victory);
            Assert.IsTrue(uiHide.Hidden, "승리로 이벤트 화면 재오픈 → 다시 가림");
            runner.Complete(true);
            Assert.IsFalse(uiHide.Hidden);
            CollectionAssert.AreEqual(new[] { "Hide", "Show", "Hide", "Show" }, uiHide.Calls);
        }

        [Test]
        public void UIHide_Flee_Shows()
        {
            var director = Make();
            director.OnEventCheck();
            director.RequestBattle(() => { });
            director.OnEventBattleFinished(EventBattleOutcome.Flee);
            Assert.IsFalse(uiHide.Hidden);
        }

        [Test]
        public void UIHide_ScriptRunFailure_Shows()
        {
            runner.Accept = false;
            var director = Make();
            director.OnEventCheck();
            Assert.IsFalse(uiHide.Hidden);
            CollectionAssert.AreEqual(new[] { "Hide", "Show" }, uiHide.Calls);
        }

        [Test]
        public void UIHide_TripEndedWhileRunning_Shows()
        {
            var director = Make();
            director.OnEventCheck();
            director.OnMilestone(TripMilestone.TripEnded);
            Assert.IsFalse(uiHide.Hidden, "방어 경로도 진행자가 직접 푼다");
        }

        [Test]
        public void TripEndedWhileRunning_EndsLikeNormalPath_DrainsPendingLine()
        {
            var director = Make();
            director.OnEventCheck();
            var advanced = false;
            runner.Target.PresentLine(new DialogueLineData("Warrior01", "무슨 일이지?", null), () => advanced = true);
            screens.Calls.Clear();

            director.OnMilestone(TripMilestone.TripEnded);
            Assert.IsFalse(director.IsRunning);
            Assert.AreEqual(0, runner.StopCount, "도주·패배와 같이 Yarn을 멈추지 않고 흘려보낸다");
            Assert.IsTrue(advanced, "떠 있던 줄을 넘겨 남은 대본이 흘러가게 한다");
            CollectionAssert.Contains(screens.Calls, "HideDialogue");
            CollectionAssert.Contains(screens.Calls, "Controls:False");
            Assert.IsFalse(history.HasOccurred("E1"), "끝까지 진행하지 않았다");
            CollectionAssert.IsEmpty(sink.Raised, "상행이 끝났으니 이벤트 종료 신호를 내지 않는다");
        }

        [Test]
        public void TripEndedWhileChoosing_SkipsChoice()
        {
            var director = Make();
            director.OnEventCheck();
            int? chosen = null;
            runner.Target.PresentOptions(new[] { new DialogueOptionData(0, "가", true, null) }, i => chosen = i);
            director.OnMilestone(TripMilestone.TripEnded);
            Assert.AreEqual(-1, chosen, "선택 화면에서 기다리던 선택지는 선택 없음으로 넘긴다");
            CollectionAssert.Contains(screens.Calls, "HideChoice");
        }

        [Test]
        public void UIHide_FormationViewer_KeepsHidden()
        {
            var director = Make();
            director.OnEventCheck();
            screens.RaiseFormationView();
            screens.RaiseReshown();
            Assert.IsTrue(uiHide.Hidden);
            CollectionAssert.AreEqual(new[] { "Hide" }, uiHide.Calls);
        }

        [Test]
        public void UIHide_Missing_EventStillRuns()
        {
            var director = Make(withHide: false);
            director.OnEventCheck();
            Assert.IsTrue(director.IsRunning);
            runner.Complete(true);
            Assert.IsFalse(director.IsRunning);
        }

        [Test]
        public void Check_StartsEvent_HaltsLocksShowsRuns()
        {
            var director = Make();
            director.OnEventCheck();
            Assert.IsTrue(director.IsRunning);
            Assert.AreEqual("E1", director.CurrentEventId);
            Assert.IsTrue(halt.IsHeld(TripHaltReason.Event));
            CollectionAssert.AreEqual(new[] { "Controls:True", "ShowDialogue" }, screens.Calls);
            Assert.AreEqual("E1_Node", runner.Node);
        }

        [Test]
        public void Check_ModalOpen_WaitsThenStartsOnClose()
        {
            var director = Make();
            director.SetModalOpen(true);
            director.OnEventCheck();
            Assert.AreEqual("E1", director.PendingEventId);
            Assert.IsTrue(halt.IsHeld(TripHaltReason.EventPending));
            Assert.IsNull(runner.Node);

            director.SetModalOpen(false);
            Assert.IsNull(runner.Node, "닫힘 신호 처리 중에는 시작하지 않는다(다음 프레임)");
            director.Tick(0.016f);
            Assert.AreEqual("E1_Node", runner.Node);
            Assert.IsFalse(halt.IsHeld(TripHaltReason.EventPending));
            Assert.IsTrue(halt.IsHeld(TripHaltReason.Event));
        }

        [Test]
        public void Finish_Completed_RecordsReleasesSignalsUnlocks()
        {
            var director = Make();
            director.OnEventCheck();
            runner.Complete(true);
            Assert.IsFalse(director.IsRunning);
            Assert.IsTrue(history.HasOccurred("E1"));
            Assert.IsFalse(halt.IsHalted);
            CollectionAssert.AreEqual(new[] { TripMilestone.EventEnded }, sink.Raised);
            CollectionAssert.Contains(screens.Calls, "HideDialogue");
            Assert.AreEqual("Controls:False", screens.Calls[screens.Calls.Count - 1]);
        }

        [Test]
        public void Battle_Victory_KeepsEventHalt_ReopensDialogue_ThenEndsWithBothSignals()
        {
            var director = Make();
            director.OnEventCheck();
            var won = false;
            director.RequestBattle(() => won = true);
            Assert.AreEqual(1, battle.Requests);
            CollectionAssert.Contains(screens.Calls, "HideDialogue");

            screens.Calls.Clear();
            director.OnEventBattleFinished(EventBattleOutcome.Victory);
            Assert.IsTrue(won);
            Assert.IsTrue(director.IsRunning);
            Assert.IsTrue(halt.IsHeld(TripHaltReason.Event));
            CollectionAssert.Contains(screens.Calls, "ShowDialogue");
            CollectionAssert.IsEmpty(sink.Raised);

            runner.Complete(true);
            CollectionAssert.AreEqual(new[] { TripMilestone.EventEnded, TripMilestone.BattleEnded }, sink.Raised);
        }

        [Test]
        public void Battle_Flee_EndsRecorded_ReleasesSignals()
        {
            var director = Make();
            director.OnEventCheck();
            director.RequestBattle(() => { });
            director.OnEventBattleFinished(EventBattleOutcome.Flee);
            Assert.IsFalse(director.IsRunning);
            Assert.IsTrue(history.HasOccurred("E1"), "도주도 끝까지 진행으로 기록");
            Assert.AreEqual(0, runner.StopCount, "Yarn을 강제로 멈추지 않는다 - 정리 뒤 늦은 명령 완료가 끝난 대화를 이어가려 한다");
            Assert.IsFalse(halt.IsHalted);
            CollectionAssert.AreEqual(new[] { TripMilestone.EventEnded, TripMilestone.BattleEnded }, sink.Raised);
        }

        [Test]
        public void AfterFlee_RemainingScript_IsSwallowed_NoScreen()
        {
            var director = Make();
            director.OnEventCheck();
            director.RequestBattle(() => { });
            director.OnEventBattleFinished(EventBattleOutcome.Flee);
            screens.Calls.Clear();

            var advanced = false;
            runner.Target.PresentLine(new DialogueLineData("Warrior01", "정리됐습니다.", null), () => advanced = true);
            Assert.IsTrue(advanced, "남은 줄은 화면 없이 바로 넘긴다");
            Assert.IsNull(((EventPresentationTargetTests.FakeStage)screens.DialogueStage).Text);
            int? chosen = null;
            runner.Target.PresentOptions(new[] { new DialogueOptionData(0, "가", true, null) }, i => chosen = i);
            Assert.AreEqual(-1, chosen, "선택지는 건너뛴다");
            CollectionAssert.IsEmpty(screens.Calls);

            runner.Complete(true);
            CollectionAssert.AreEqual(new[] { TripMilestone.EventEnded, TripMilestone.BattleEnded }, sink.Raised, "흘려보낸 대본이 끝나도 신호를 다시 내지 않는다");
        }

        [Test]
        public void Battle_Defeat_Records_NoReleaseNoSignals()
        {
            var director = Make();
            director.OnEventCheck();
            director.RequestBattle(() => { });
            director.OnEventBattleFinished(EventBattleOutcome.Defeat);
            Assert.IsFalse(director.IsRunning);
            Assert.AreEqual(0, runner.StopCount);
            Assert.IsTrue(history.HasOccurred("E1"));
            Assert.IsTrue(halt.IsHeld(TripHaltReason.Event), "상행 종료 신호가 비운다");
            CollectionAssert.IsEmpty(sink.Raised);
        }

        [Test]
        public void RunnerRefuses_ReleasesWithoutRecordOrSignals()
        {
            runner.Accept = false;
            var director = Make();
            director.OnEventCheck();
            Assert.IsFalse(director.IsRunning);
            Assert.IsFalse(halt.IsHalted);
            Assert.IsFalse(history.HasOccurred("E1"));
            CollectionAssert.IsEmpty(sink.Raised);
        }

        [Test]
        public void BindScreensTwice_SubscribesOnce()
        {
            var director = Make();
            director.BindScreens(screens);
            Assert.AreEqual(1, screens.ClickedSubscribers);
        }

        [Test]
        public void NoScreens_CheckDoesNothing()
        {
            var director = Make();
            director.BindScreens(null);
            director.OnEventCheck();
            Assert.IsFalse(director.IsRunning);
            Assert.IsFalse(halt.IsHalted);
        }

        [Test]
        public void TripStarted_ClearsQueue()
        {
            var director = Make();
            director.SetModalOpen(true);
            content.List.Add(new EventEntry { Id = "E4", YarnNode = "E4_Node", Occurrence = EventOccurrence.Fixed });
            director.OnEventCheck();                  // E1 대기, E4 대기열
            Assert.AreEqual(1, director.QueuedEvents.Count);
            director.OnMilestone(TripMilestone.TripStarted);
            Assert.AreEqual(0, director.QueuedEvents.Count);
            Assert.IsNull(director.PendingEventId);
        }

        [Test]
        public void FormationView_OpensViewer()
        {
            var director = Make();
            director.OnEventCheck();
            screens.RaiseFormationView();
            CollectionAssert.Contains(screens.Calls, "Formation");
        }
    }
}
