using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class EventPresentationTargetTests
    {
        internal sealed class FakeScreens : IEventEncounterScreens
        {
            public readonly List<string> Calls = new();
            public EventChoiceContent LastChoice;
            public Action<int> ChoiceCallback;
            public Action CargoDone;
            public bool HasCargoScreen = true;
            public bool IsAvailable => true;
            public IDialogueStageView DialogueStage { get; } = new FakeStage();
            public event Action Clicked;
            public event Action FormationViewRequested;
            public event Action Reshown;
            public int ClickedSubscribers => Clicked?.GetInvocationList().Length ?? 0;
            public void RaiseClicked() => Clicked?.Invoke();
            public void RaiseFormationView() => FormationViewRequested?.Invoke();
            public void RaiseReshown() => Reshown?.Invoke();
            public void ShowDialogue() => Calls.Add("ShowDialogue");
            public void HideDialogue() => Calls.Add("HideDialogue");
            public void ShowChoice(EventChoiceContent content, Action<int> onChosen) { Calls.Add("ShowChoice"); LastChoice = content; ChoiceCallback = onChosen; }
            public void HideChoice() => Calls.Add("HideChoice");
            public bool TryShowCargoRecovery(Action onDone) { Calls.Add("Cargo"); CargoDone = onDone; return HasCargoScreen; }
            public void OpenFormationViewer() => Calls.Add("Formation");
            public void SetEventControls(bool eventActive) => Calls.Add($"Controls:{eventActive}");
        }

        internal sealed class FakeStage : IDialogueStageView
        {
            public string Name;
            public string Text;
            public bool Visible;
            public void SetVisible(bool visible) => Visible = visible;
            public void SetBubble(string speakerName, string text) { Name = speakerName; Text = text; }
            public void SetVisibleCharacters(int count) { }
            public void SetContinueMarker(bool shown) { }
            public void SetStanding(DialogueSide side, StandingState state) { }
        }

        private static (EventPresentationTarget target, FakeScreens screens, DialoguePlaybackController playback) Make()
        {
            var screens = new FakeScreens();
            var speakers = new SpeakerPresentation(id => id == "bandit" ? "산적" : null);
            var playback = new DialoguePlaybackController(new StagedDialogueView(screens.DialogueStage, speakers), DialoguePlaybackOptions.Event);
            return (new EventPresentationTarget(playback, screens, speakers), screens, playback);
        }

        private static DialogueLineData Line(string speaker, string text, params string[] tags) => new(speaker, text, tags);

        [Test]
        public void ChoiceLine_NotShownInDialogue_AdvancesImmediately_FeedsChoiceScreen()
        {
            var (target, screens, _) = Make();
            var advanced = false;
            target.PresentLine(Line("", "무리가 길을 막는다.", "choice", "people:bandit,Warrior01", "lastline"), () => advanced = true);
            Assert.IsTrue(advanced);
            Assert.IsNull(((FakeStage)screens.DialogueStage).Text, "설명문은 대화 화면에 나오지 않는다");

            int? chosen = null;
            target.PresentOptions(new[] { new DialogueOptionData(0, "낸다", false, new[] { "lock:골드_300_필요" }), new DialogueOptionData(1, "거절한다", true, null) }, i => chosen = i);
            Assert.AreEqual("무리가 길을 막는다.", screens.LastChoice.Description);
            Assert.AreEqual(2, screens.LastChoice.People.Count);
            Assert.AreEqual("산적", screens.LastChoice.People[0].Name);
            Assert.AreEqual(2, screens.LastChoice.Options.Count);

            screens.ChoiceCallback(0);
            Assert.IsNull(chosen, "잠긴 선택지는 고를 수 없다");
            screens.ChoiceCallback(1);
            Assert.AreEqual(1, chosen);
            CollectionAssert.Contains(screens.Calls, "HideChoice");
            chosen = null;
            screens.ChoiceCallback(1);
            Assert.IsNull(chosen, "두 번 눌러도 한 번만");
        }

        [Test]
        public void AllLocked_NoChoiceScreen_ChoosesNone()
        {
            var (target, screens, _) = Make();
            int? chosen = null;
            target.PresentOptions(new[] { new DialogueOptionData(0, "낸다", false, null) }, i => chosen = i);
            Assert.AreEqual(-1, chosen);
            CollectionAssert.DoesNotContain(screens.Calls, "ShowChoice");
        }

        [Test]
        public void ResultLine_IsNarration_ClickToAdvance()
        {
            var (target, screens, playback) = Make();
            var advanced = false;
            target.PresentResult("골드 −300", () => advanced = true);
            var stage = (FakeStage)screens.DialogueStage;
            Assert.AreEqual("골드 −300", stage.Text);
            Assert.AreEqual(string.Empty, stage.Name);
            playback.Tick(10f);
            Assert.IsFalse(advanced, "이벤트는 자동 넘김이 없다");
            playback.Click();
            Assert.IsTrue(advanced);
        }
    }
}
