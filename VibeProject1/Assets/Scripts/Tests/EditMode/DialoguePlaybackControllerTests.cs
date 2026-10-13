using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class DialoguePlaybackControllerTests
    {
        private sealed class FakeView : IDialogueView
        {
            public DialogueLineData? Line;
            public int Visible;
            public List<DialogueOptionData> Options;
            public Action<int> Choose;
            public float Remaining = -1f;
            public bool Hidden;
            public void ShowLine(DialogueLineData line) => Line = line;
            public void SetVisibleLength(int length) => Visible = length;
            public void SetTypingComplete(bool complete) { }
            public void ShowOptions(IReadOnlyList<DialogueOptionData> visibleOptions, Action<int> onChosen) { Options = new List<DialogueOptionData>(visibleOptions); Choose = onChosen; }
            public void SetOptionTimeRemaining(float fraction01) => Remaining = fraction01;
            public void HideOptions() => Options = null;
            public void Hide() => Hidden = true;
        }

        private static DialogueLineData L(string text, params string[] tags) => new("a", text, tags);
        private static DialogueOptionData O(int i, string text, bool available = true, params string[] tags) => new(i, text, available, tags);

        [Test]
        public void Tags_Parse()
        {
            // Yarn 태그는 공백을 넣을 수 없어 잠김 문구의 공백은 _로 쓴다 - 표시할 때 공백으로 바꾼다.
            var o = O(0, "x", false, "lock:골드_300_필요");
            Assert.AreEqual("골드 300 필요", o.LockText);
            Assert.IsTrue(O(1, "(무응답)", true, "timeout").IsTimeout);
            Assert.IsTrue(L("q", "ask").HasTag(DialogueTags.Ask));
            Assert.IsTrue(L("q", "people:a,b").TryGetTagValue(DialogueTags.People, out var people));
            Assert.AreEqual("a,b", people);
        }

        [Test]
        public void Line_TypesAndAdvances()
        {
            var view = new FakeView();
            var c = new DialoguePlaybackController(view, DialoguePlaybackOptions.Trip);
            var advanced = 0;
            c.PresentLine(L("가나"), () => advanced++);
            Assert.AreEqual("가나", view.Line.Value.Text);
            c.Tick(1f);
            Assert.AreEqual(2, view.Visible);
            c.Tick(2.5f);
            Assert.AreEqual(1, advanced);
        }

        [Test]
        public void AskLine_AdvancesRightAfterTyping()
        {
            var c = new DialoguePlaybackController(new FakeView(), DialoguePlaybackOptions.Trip);
            var advanced = 0;
            c.PresentLine(L("질문?", DialogueTags.Ask), () => advanced++);
            c.Tick(1f);
            Assert.AreEqual(1, advanced);
        }

        [Test]
        public void YarnLastLine_AdvancesRightAfterTyping()
        {
            // Yarn은 선택지 바로 앞 줄에 lastline 태그를 자동으로 붙인다 - 작가가 #ask를 쓰지 않아도 질문 줄이 대기 없이 넘어가야 한다.
            var c = new DialoguePlaybackController(new FakeView(), DialoguePlaybackOptions.Trip);
            var advanced = 0;
            c.PresentLine(L("질문?", DialogueTags.LastLine), () => advanced++);
            c.Tick(1f);
            Assert.AreEqual(1, advanced);
        }

        [Test]
        public void Options_HideTimeout_AndTimeoutPicksIt()
        {
            var view = new FakeView();
            var c = new DialoguePlaybackController(view, DialoguePlaybackOptions.Trip);
            var chosen = int.MinValue;
            c.PresentOptions(new[] { O(0, "예"), O(1, "(무응답)", true, DialogueTags.Timeout) }, i => chosen = i);
            Assert.AreEqual(1, view.Options.Count);
            c.Tick(5f);
            Assert.AreEqual(0.5f, view.Remaining, 1e-4f);
            c.Tick(5f);
            Assert.AreEqual(1, chosen);
            Assert.IsNull(view.Options);
        }

        [Test]
        public void Options_PlayerChoice_IgnoresLocked()
        {
            var view = new FakeView();
            var c = new DialoguePlaybackController(view, DialoguePlaybackOptions.Event);
            var chosen = int.MinValue;
            c.PresentOptions(new[] { O(0, "낸다", false, "lock:골드_300_필요"), O(1, "거절") }, i => chosen = i);
            view.Choose(0);
            Assert.AreEqual(int.MinValue, chosen);
            view.Choose(1);
            Assert.AreEqual(1, chosen);
        }

        [Test]
        public void Options_EventHasNoTimeout()
        {
            var view = new FakeView();
            var c = new DialoguePlaybackController(view, DialoguePlaybackOptions.Event);
            var chosen = int.MinValue;
            c.PresentOptions(new[] { O(0, "예"), O(1, "(무응답)", true, DialogueTags.Timeout) }, i => chosen = i);
            c.Tick(100f);
            Assert.AreEqual(int.MinValue, chosen);
        }

        [Test]
        public void Options_AllLockedNoTimeout_ChoosesNone()
        {
            var c = new DialoguePlaybackController(new FakeView(), DialoguePlaybackOptions.Trip);
            var chosen = int.MinValue;
            c.PresentOptions(new[] { O(0, "x", false) }, i => chosen = i);
            Assert.AreEqual(-1, chosen);
        }

        [Test]
        public void Options_EventAllLockedWithTimeoutOption_ChoosesNone()
        {
            // 이벤트는 제한시간이 없어 무응답 선택지가 골라질 일이 없다 - 남겨 두면 잠긴 선택지만 뜬 채 멈춘다.
            var view = new FakeView();
            var c = new DialoguePlaybackController(view, DialoguePlaybackOptions.Event);
            var chosen = int.MinValue;
            c.PresentOptions(new[] { O(0, "x", false), O(1, "(무응답)", true, DialogueTags.Timeout) }, i => chosen = i);
            Assert.AreEqual(-1, chosen);
            Assert.IsNull(view.Options);
        }

        [Test]
        public void Pause_FreezesOptionTimer()
        {
            var view = new FakeView();
            var c = new DialoguePlaybackController(view, DialoguePlaybackOptions.Trip);
            var chosen = int.MinValue;
            c.PresentOptions(new[] { O(0, "예"), O(1, "-", true, DialogueTags.Timeout) }, i => chosen = i);
            var reason = new object();
            c.SetPaused(reason, true);
            c.Tick(20f);
            Assert.AreEqual(int.MinValue, chosen);
            c.SetPaused(reason, false);
            c.Tick(10f);
            Assert.AreEqual(1, chosen);
        }

        [Test]
        public void Stop_HidesWithoutCallbacks()
        {
            var view = new FakeView();
            var c = new DialoguePlaybackController(view, DialoguePlaybackOptions.Trip);
            var advanced = 0;
            c.PresentLine(L("가나다"), () => advanced++);
            c.Stop();
            c.Tick(100f);
            Assert.AreEqual(0, advanced);
            Assert.IsTrue(view.Hidden);
        }
    }
}
