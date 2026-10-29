using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class TripDialogueDirectorTests
    {
        private sealed class FakeContent : IEncounterContentReader
        {
            public List<DialogueEntry> DialogueList = new();
            public List<PersonEntry> PeopleList = new();
            public IReadOnlyList<EventEntry> Events => Array.Empty<EventEntry>();
            public IReadOnlyList<EventEntry> EventsAt(EventPlace place) => Array.Empty<EventEntry>();
            public IReadOnlyList<DialogueEntry> Dialogues => DialogueList;
            public IReadOnlyList<PersonEntry> People => PeopleList;
            public bool TryGetEvent(string id, out EventEntry entry) { entry = null; return false; }
            public bool TryGetDialogue(string id, out DialogueEntry entry) { entry = DialogueList.Find(d => d.Id == id); return entry != null; }
            public bool TryGetPerson(string id, out PersonEntry entry) { entry = PeopleList.Find(p => p.Id == id); return entry != null; }
        }

        // 실제 실행기처럼 중단하면 onFinished(false)를 한 번 부른다.
        private sealed class FakeRunner : IDialogueScriptRunner
        {
            public readonly List<string> Started = new();
            public IDialoguePresentationTarget Target;
            private Action<bool> onFinished;
            public bool IsRunning => onFinished != null;
            public bool HasNode(string node) => true;
            public bool TryRun(string node, IDialoguePresentationTarget target, Action<bool> finished)
            {
                if (IsRunning) return false;
                Started.Add(node);
                Target = target;
                onFinished = finished;
                return true;
            }
            public void Stop() { var f = onFinished; onFinished = null; f?.Invoke(false); }
            public void Complete() { var f = onFinished; onFinished = null; f?.Invoke(true); }
        }

        private sealed class FakeView : ITripDialogueView
        {
            public bool Visible;
            public int Chars;
            public event Action Clicked;
            public void RaiseClick() => Clicked?.Invoke();
            public int ClickSubscribers => Clicked?.GetInvocationList().Length ?? 0;
            public void SetVisible(bool visible) => Visible = visible;
            public void SetBubble(string speakerName, string text) { }
            public void SetVisibleCharacters(int count) => Chars = count;
            public void SetContinueMarker(bool shown) { }
            public void SetStanding(DialogueSide side, StandingState state) { }
            public void ShowOptions(IReadOnlyList<DialogueOptionData> options, Action<int> onChosen) { }
            public void SetOptionTimeRemaining(float fraction01) { }
            public void HideOptions() { }
        }

        private sealed class FakeManual : IManualTalkView
        {
            public bool Allowed;
            public List<ManualTalkMember> Shown;
            public event Action OpenRequested;
            public event Action<string> MemberChosen;
            public int OpenSubscribers => OpenRequested?.GetInvocationList().Length ?? 0;
            public void RequestOpen() => OpenRequested?.Invoke();
            public void Choose(string id) => MemberChosen?.Invoke(id);
            public void SetTalkAllowed(bool allowed) => Allowed = allowed;
            public void ShowMembers(IReadOnlyList<ManualTalkMember> members) => Shown = new List<ManualTalkMember>(members);
            public void HideMembers() => Shown = null;
        }

        private sealed class Rig
        {
            public FakeContent Content = new();
            public EncounterHistory History = new();
            public FakeRunner Runner = new();
            public FakeView View = new();
            public FakeManual Manual = new();
            public Queue<float> Rolls = new();
            public bool Halted;
            public TripDialogueDirector Director;

            public Rig(params DialogueEntry[] dialogues)
            {
                Content.DialogueList.AddRange(dialogues);
                Director = new TripDialogueDirector(new TripDialogueSources
                {
                    Content = Content,
                    History = History,
                    HistoryWriter = History,
                    Runner = Runner,
                    Crew = () => new[] { new CrewMember("a", "에이"), new CrewMember("b", "비") },
                    Random01 = () => Rolls.Count > 0 ? Rolls.Dequeue() : 0.99f,
                    IsHalted = () => Halted,
                });
                Director.BindViews(View, Manual);
            }
        }

        private static DialogueEntry D(string id, DialogueOccurrence o, DialogueTrigger t = DialogueTrigger.None, string speakers = "a")
            => new() { Id = id, YarnNode = id + "_node", Occurrence = o, Trigger = t, Speakers = new List<string>(speakers.Split(';')) };

        [Test]
        public void TripStarted_FirstCheckStartsDepartureDialogue()
        {
            var rig = new Rig(D("dep", DialogueOccurrence.Trigger, DialogueTrigger.Departure));
            rig.Director.OnMilestone(TripMilestone.TripStarted);
            rig.Director.OnDialogueCheck();
            CollectionAssert.AreEqual(new[] { "dep_node" }, rig.Runner.Started);
            Assert.IsTrue(rig.Director.IsRunning);
            Assert.AreEqual("dep", rig.Director.CurrentDialogueId);
            Assert.IsEmpty(rig.Director.PendingTriggers);
        }

        [Test]
        public void CheckWhileRunning_DiscardsPending()
        {
            var rig = new Rig(D("r", DialogueOccurrence.Random), D("city", DialogueOccurrence.Trigger, DialogueTrigger.MidCityArrival));
            rig.Rolls.Enqueue(0f); rig.Rolls.Enqueue(0f);
            rig.Director.OnDialogueCheck();                      // 랜덤 대화 시작
            rig.Director.OnMilestone(TripMilestone.MidCityArrived);
            rig.Director.OnDialogueCheck();                      // 진행 중 → 쌓인 일 소멸
            rig.Runner.Complete();
            rig.Director.OnDialogueCheck();
            CollectionAssert.AreEqual(new[] { "r_node" }, rig.Runner.Started);
        }

        [Test]
        public void Completed_IsRecorded_Stopped_IsNot()
        {
            var rig = new Rig(D("dep", DialogueOccurrence.Trigger, DialogueTrigger.Departure), D("city", DialogueOccurrence.Trigger, DialogueTrigger.MidCityArrival));
            rig.Director.OnMilestone(TripMilestone.TripStarted);
            rig.Director.OnDialogueCheck();
            rig.Runner.Complete();
            Assert.IsTrue(rig.History.HasOccurred("dep"));
            Assert.IsFalse(rig.Director.IsRunning);

            rig.Director.OnMilestone(TripMilestone.MidCityArrived);
            rig.Director.OnDialogueCheck();
            rig.Director.OnHaltStarted();                        // 전투 판정 성공 → 즉시 종료
            Assert.IsFalse(rig.History.HasOccurred("city"));
            Assert.IsFalse(rig.Director.IsRunning);
            Assert.IsFalse(rig.View.Visible);
        }

        [Test]
        public void TripEnded_StopsAndClears()
        {
            var rig = new Rig(D("dep", DialogueOccurrence.Trigger, DialogueTrigger.Departure));
            rig.Director.OnMilestone(TripMilestone.TripStarted);
            rig.Director.OnDialogueCheck();
            rig.Director.OnMilestone(TripMilestone.MidCityArrived);
            rig.Director.OnMilestone(TripMilestone.TripEnded);
            Assert.IsFalse(rig.Director.IsRunning);
            Assert.IsEmpty(rig.Director.PendingTriggers);
            Assert.IsFalse(rig.History.HasOccurred("dep"));
        }

        [Test]
        public void BattleEnded_StartsAfterBattleDialogue_FiveTripSecondsLater()
        {
            var rig = new Rig(D("battle", DialogueOccurrence.Trigger, DialogueTrigger.AfterBattle));
            rig.Director.OnMilestone(TripMilestone.BattleEnded);
            Assert.IsEmpty(rig.Director.PendingTriggers);
            Assert.AreEqual(5f, rig.Director.AfterBattleSecondsRemaining, 0.001f);
            rig.Director.AdvanceTripTime(4.9f);
            Assert.IsEmpty(rig.Runner.Started);
            rig.Director.AdvanceTripTime(0.2f);
            CollectionAssert.AreEqual(new[] { "battle_node" }, rig.Runner.Started);
            Assert.Less(rig.Director.AfterBattleSecondsRemaining, 0f);
        }

        [Test]
        public void RegularCheckDuringDelay_DoesNotConsumeBattle()
        {
            var rig = new Rig(D("battle", DialogueOccurrence.Trigger, DialogueTrigger.AfterBattle));
            rig.Director.OnMilestone(TripMilestone.BattleEnded);
            rig.Director.OnDialogueCheck();
            Assert.IsEmpty(rig.Runner.Started);
            rig.Director.AdvanceTripTime(5f);
            CollectionAssert.AreEqual(new[] { "battle_node" }, rig.Runner.Started);
        }

        [Test]
        public void HaltOrTripEndDuringDelay_CancelsAfterBattle()
        {
            var rig = new Rig(D("battle", DialogueOccurrence.Trigger, DialogueTrigger.AfterBattle));
            rig.Director.OnMilestone(TripMilestone.BattleEnded);
            rig.Director.AdvanceTripTime(2f);
            rig.Director.OnHaltStarted();
            rig.Director.AdvanceTripTime(10f);
            rig.Director.OnMilestone(TripMilestone.BattleEnded);
            rig.Director.OnMilestone(TripMilestone.TripEnded);
            rig.Director.AdvanceTripTime(10f);
            Assert.IsEmpty(rig.Runner.Started);
        }

        [Test]
        public void AfterBattleWhileRunning_IsDiscarded()
        {
            var rig = new Rig(D("battle", DialogueOccurrence.Trigger, DialogueTrigger.AfterBattle), D("m", DialogueOccurrence.Manual));
            rig.Manual.RequestOpen();
            rig.Manual.Choose("a");
            rig.Director.OnMilestone(TripMilestone.BattleEnded);
            rig.Director.AdvanceTripTime(5f);
            rig.Runner.Complete();
            rig.Director.AdvanceTripTime(5f);
            CollectionAssert.AreEqual(new[] { "m_node" }, rig.Runner.Started);
        }

        [Test]
        public void AfterBattle_NoCandidate_DoesNotFallBackToRandom()
        {
            var rig = new Rig(D("r", DialogueOccurrence.Random));
            rig.Rolls.Enqueue(0f); rig.Rolls.Enqueue(0f);
            rig.Director.OnMilestone(TripMilestone.BattleEnded);
            rig.Director.AdvanceTripTime(5f);
            Assert.IsEmpty(rig.Runner.Started);
        }

        [Test]
        public void TalkAllowed_FalseWhileRunningOrHalted()
        {
            var rig = new Rig(D("rep", DialogueOccurrence.ManualRepeat));
            Assert.IsTrue(rig.Manual.Allowed);
            rig.Halted = true;
            rig.Director.OnHaltStarted();
            Assert.IsFalse(rig.Manual.Allowed);
            rig.Halted = false;
            rig.Director.OnHaltEnded();
            Assert.IsTrue(rig.Manual.Allowed);

            rig.Manual.RequestOpen();
            rig.Manual.Choose("a");
            Assert.IsFalse(rig.Manual.Allowed);
            rig.Runner.Complete();
            Assert.IsTrue(rig.Manual.Allowed);
        }

        [Test]
        public void ManualTalk_ListMarksNoTalk_AndChoiceStartsDialogue()
        {
            var rig = new Rig(D("m", DialogueOccurrence.Manual, speakers: "a"));
            rig.Manual.RequestOpen();
            Assert.AreEqual(2, rig.Manual.Shown.Count);
            Assert.IsTrue(rig.Manual.Shown[0].HasTalk);
            Assert.AreEqual("에이", rig.Manual.Shown[0].Name);
            Assert.IsFalse(rig.Manual.Shown[1].HasTalk);

            rig.Manual.Choose("b");                              // 할 말 없음 → 아무 일 없음
            Assert.IsEmpty(rig.Runner.Started);
            rig.Manual.RequestOpen();
            rig.Manual.Choose("a");
            CollectionAssert.AreEqual(new[] { "m_node" }, rig.Runner.Started);
            Assert.IsNull(rig.Manual.Shown);
        }

        [Test]
        public void ManualTalk_IgnoredWhileHalted()
        {
            var rig = new Rig(D("m", DialogueOccurrence.Manual, speakers: "a"));
            rig.Halted = true;
            rig.Manual.RequestOpen();
            Assert.IsNull(rig.Manual.Shown);
            rig.Manual.Choose("a");
            Assert.IsEmpty(rig.Runner.Started);
        }

        [Test]
        public void CheckDialogueStart_ClosesMemberList()
        {
            var rig = new Rig(D("dep", DialogueOccurrence.Trigger, DialogueTrigger.Departure), D("m", DialogueOccurrence.Manual));
            rig.Manual.RequestOpen();
            Assert.IsNotNull(rig.Manual.Shown);
            rig.Director.OnMilestone(TripMilestone.TripStarted);
            rig.Director.OnDialogueCheck();
            Assert.IsNull(rig.Manual.Shown);
        }

        [Test]
        public void StartWhileModalOpen_StartsPaused_ThenResumes()
        {
            var rig = new Rig(D("dep", DialogueOccurrence.Trigger, DialogueTrigger.Departure));
            rig.Director.SetModalOpen(true);
            rig.Director.OnMilestone(TripMilestone.TripStarted);
            rig.Director.OnDialogueCheck();
            rig.Runner.Target.PresentLine(new DialogueLineData("a", "안녕하세요", new[] { "left" }), () => { });
            rig.Director.Tick(10f);
            Assert.AreEqual(0, rig.View.Chars);
            rig.Director.SetModalOpen(false);
            rig.Director.Tick(10f);
            Assert.AreEqual(5, rig.View.Chars);
        }

        [Test]
        public void ScreenClick_CompletesTyping()
        {
            var rig = new Rig(D("dep", DialogueOccurrence.Trigger, DialogueTrigger.Departure));
            rig.Director.OnMilestone(TripMilestone.TripStarted);
            rig.Director.OnDialogueCheck();
            rig.Runner.Target.PresentLine(new DialogueLineData("a", "안녕하세요", new[] { "left" }), () => { });
            rig.View.RaiseClick();
            Assert.AreEqual(5, rig.View.Chars);
        }

        [Test]
        public void BindViewsTwice_SubscribesOnce()
        {
            var rig = new Rig();
            rig.Director.BindViews(rig.View, rig.Manual);
            Assert.AreEqual(1, rig.View.ClickSubscribers);
            Assert.AreEqual(1, rig.Manual.OpenSubscribers);

            var other = new FakeView();
            rig.Director.BindViews(other, rig.Manual);
            Assert.AreEqual(0, rig.View.ClickSubscribers);
            Assert.AreEqual(1, other.ClickSubscribers);
        }

        [Test]
        public void NoView_CheckDoesNothing()
        {
            var rig = new Rig(D("dep", DialogueOccurrence.Trigger, DialogueTrigger.Departure));
            rig.Director.BindViews(null, null);
            rig.Director.OnMilestone(TripMilestone.TripStarted);
            rig.Director.OnDialogueCheck();
            Assert.IsEmpty(rig.Runner.Started);
            Assert.IsEmpty(rig.Director.PendingTriggers);
        }

        [Test]
        public void DebugStart_IgnoresConditions_AndReplacesCurrent()
        {
            var blocked = D("blocked", DialogueOccurrence.Random);
            blocked.Conditions = EncounterConditionParser.Parse("flag:never");
            var rig = new Rig(D("dep", DialogueOccurrence.Trigger, DialogueTrigger.Departure), blocked);
            rig.Director.OnMilestone(TripMilestone.TripStarted);
            rig.Director.OnDialogueCheck();
            Assert.IsTrue(rig.Director.DebugStart("blocked"));
            CollectionAssert.AreEqual(new[] { "dep_node", "blocked_node" }, rig.Runner.Started);
            Assert.AreEqual("blocked", rig.Director.CurrentDialogueId);
            Assert.IsFalse(rig.History.HasOccurred("dep"));
            Assert.IsFalse(rig.Director.DebugStart("missing"));
        }
    }
}
