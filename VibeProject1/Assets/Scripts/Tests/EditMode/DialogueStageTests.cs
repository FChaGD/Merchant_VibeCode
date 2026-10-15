using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class DialogueStageTests
    {
        private sealed class FakeStage : IDialogueStageView
        {
            public bool Visible;
            public string Name;
            public string Text;
            public int Chars;
            public bool Marker;
            public readonly Dictionary<DialogueSide, StandingState> Standing = new();
            public List<DialogueOptionData> Options;
            public void SetVisible(bool visible) => Visible = visible;
            public void SetBubble(string speakerName, string text) { Name = speakerName; Text = text; }
            public void SetVisibleCharacters(int count) => Chars = count;
            public void SetContinueMarker(bool shown) => Marker = shown;
            public void SetStanding(DialogueSide side, StandingState state) => Standing[side] = state;
            public void ShowOptions(IReadOnlyList<DialogueOptionData> options, Action<int> onChosen) => Options = new List<DialogueOptionData>(options);
            public void SetOptionTimeRemaining(float fraction01) { }
            public void HideOptions() => Options = null;
        }

        private static DialogueLineData L(string speaker, string text, params string[] tags) => new(speaker, text, tags);

        [Test]
        public void Layout_FirstTagDecidesSide_AndSticks()
        {
            var layout = new DialogueStageLayout();
            Assert.AreEqual(DialogueSide.Right, layout.Apply(L("a", "1", "right")));
            Assert.AreEqual(DialogueSide.Right, layout.Apply(L("a", "2", "left")));   // 첫 대사 뒤의 자리 태그는 무시한다
            Assert.AreEqual(DialogueSide.Left, layout.Apply(L("b", "3", "left")));
            Assert.IsNull(layout.Apply(L("", "서술")));
            Assert.AreEqual("a", layout.RightSpeaker);
            Assert.AreEqual("b", layout.LeftSpeaker);
        }

        [Test]
        public void Layout_UntaggedSpeakers_FillLeftThenRight()
        {
            // 콘텐츠 검증이 막는 경우지만 실행 중엔 멈추지 않고 빈 자리에 둔다.
            var layout = new DialogueStageLayout();
            Assert.AreEqual(DialogueSide.Left, layout.Apply(L("a", "1")));
            Assert.AreEqual(DialogueSide.Right, layout.Apply(L("b", "2")));
        }

        [Test]
        public void Staged_SpeakerHighlighted_ListenerDimmed_NarrationDimsBoth()
        {
            var stage = new FakeStage();
            var view = new StagedDialogueView(stage, new SpeakerPresentation(id => id == "a" ? "에이" : null));

            view.ShowLine(L("a", "안녕", "left"));
            Assert.IsTrue(stage.Visible);
            Assert.AreEqual("에이", stage.Name);
            Assert.AreEqual("안녕", stage.Text);
            Assert.IsTrue(stage.Standing[DialogueSide.Left].Highlighted);
            Assert.IsFalse(stage.Standing[DialogueSide.Right].Visible);

            view.ShowLine(L("b", "응", "right"));
            Assert.AreEqual("b", stage.Name);   // 이름을 못 찾으면 Id
            Assert.IsFalse(stage.Standing[DialogueSide.Left].Highlighted);
            Assert.IsTrue(stage.Standing[DialogueSide.Right].Highlighted);
            Assert.AreEqual("에이", stage.Standing[DialogueSide.Left].Name);

            view.ShowLine(L("", "바람이 분다."));
            Assert.AreEqual(string.Empty, stage.Name);
            Assert.IsTrue(stage.Standing[DialogueSide.Left].Visible);
            Assert.IsFalse(stage.Standing[DialogueSide.Left].Highlighted);
            Assert.IsFalse(stage.Standing[DialogueSide.Right].Highlighted);
        }

        [Test]
        public void Staged_Hide_ResetsSidesForNextDialogue()
        {
            var stage = new FakeStage();
            var view = new StagedDialogueView(stage, new SpeakerPresentation(null));
            view.ShowLine(L("a", "1", "left"));
            view.Hide();
            Assert.IsFalse(stage.Visible);
            Assert.IsFalse(stage.Standing[DialogueSide.Left].Visible);

            view.ShowLine(L("b", "2", "left"));
            Assert.AreEqual("b", stage.Standing[DialogueSide.Left].Name);
            Assert.IsFalse(stage.Standing[DialogueSide.Right].Visible);
        }

        [Test]
        public void Staged_ForwardsTypingAndOptions()
        {
            var stage = new FakeStage();
            var view = new StagedDialogueView(stage, new SpeakerPresentation(null));
            view.SetVisibleLength(3);
            view.SetTypingComplete(true);
            view.ShowOptions(new[] { new DialogueOptionData(0, "예", true, null) }, _ => { });
            Assert.AreEqual(3, stage.Chars);
            Assert.IsTrue(stage.Marker);
            Assert.AreEqual(1, stage.Options.Count);
            view.HideOptions();
            Assert.IsNull(stage.Options);
        }

        [Test]
        public void SpeakerColor_IsStablePerId()
        {
            Assert.AreEqual(SpeakerPresentation.ColorOf("Warrior01"), SpeakerPresentation.ColorOf("Warrior01"));
            Assert.AreNotEqual(SpeakerPresentation.ColorOf("Warrior01"), SpeakerPresentation.ColorOf("Archer01"));
        }
    }
}
