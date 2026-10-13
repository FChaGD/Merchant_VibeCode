using NUnit.Framework;

namespace Game.Core.Tests
{
    public class DialogueLinePlaybackTests
    {
        [Test]
        public void Metrics_SkipWhitespace()
        {
            Assert.AreEqual(5, DialogueTextMetrics.CountTyped("가 나다, 라"));
            Assert.AreEqual(0, DialogueTextMetrics.CountTyped("   "));
            // "가 나다" - 1자 타이핑 후 뒤따르는 공백까지 보인다.
            Assert.AreEqual(2, DialogueTextMetrics.VisibleLengthFor("가 나다", 1));
            Assert.AreEqual(4, DialogueTextMetrics.VisibleLengthFor("가 나다", 3));
            Assert.AreEqual(1, DialogueTextMetrics.VisibleLengthFor(" 가", 0));
        }

        [TestCase(0, 2.5f)]
        [TestCase(20, 2.5f)]
        [TestCase(60, 4f)]
        [TestCase(80, 5f)]
        [TestCase(200, 5f)]
        public void WaitFormula(int typed, float expected) => Assert.AreEqual(expected, DialoguePacing.WaitSecondsFor(typed), 1e-5f);

        [Test]
        public void TypesAt12PerSecond_ThenWaits_ThenFinishes()
        {
            var p = new DialogueLinePlayback(autoAdvance: true);
            var finished = 0;
            p.Finished += () => finished++;
            p.Begin("가나다라마바사아자차카타", finishOnTypingComplete: false);   // 12자
            p.Tick(0.5f);
            Assert.AreEqual(6, p.VisibleLength);
            p.Tick(0.5f);
            Assert.AreEqual(DialogueLinePhase.Waiting, p.Phase);
            p.Tick(2.49f);
            Assert.AreEqual(0, finished);
            p.Tick(0.02f);
            Assert.AreEqual(1, finished);
            Assert.AreEqual(DialogueLinePhase.Finished, p.Phase);
        }

        [Test]
        public void TwoStepClick()
        {
            var p = new DialogueLinePlayback(autoAdvance: true);
            var finished = 0;
            p.Finished += () => finished++;
            p.Begin("가나다라마", false);
            p.Click();
            Assert.AreEqual(5, p.VisibleLength);
            Assert.AreEqual(DialogueLinePhase.Waiting, p.Phase);
            Assert.AreEqual(0, finished);
            p.Click();
            Assert.AreEqual(1, finished);
        }

        [Test]
        public void NoAutoAdvance_WaitsForClick()
        {
            var p = new DialogueLinePlayback(autoAdvance: false);
            var finished = 0;
            p.Finished += () => finished++;
            p.Begin("가", false);
            p.Tick(100f);
            Assert.AreEqual(0, finished);
            p.Click();
            Assert.AreEqual(1, finished);
        }

        [Test]
        public void FinishOnTypingComplete_SkipsWait()
        {
            var p = new DialogueLinePlayback(autoAdvance: true);
            int typed = 0, finished = 0;
            p.TypingCompleted += () => typed++;
            p.Finished += () => finished++;
            p.Begin("가나", finishOnTypingComplete: true);
            p.Tick(1f);
            Assert.AreEqual((1, 1), (typed, finished));
        }

        [Test]
        public void Pause_FreezesTypingWaitAndClicks()
        {
            var p = new DialogueLinePlayback(autoAdvance: true);
            var finished = 0;
            p.Finished += () => finished++;
            p.Begin("가나다라마바사아자차카타", false);
            var reason = new object();
            p.SetPaused(reason, true);
            p.Tick(10f);
            p.Click();
            Assert.AreEqual(0, p.VisibleLength);
            p.SetPaused(reason, false);
            p.Tick(1f);
            Assert.AreEqual(12, p.VisibleLength);
            Assert.AreEqual(0, finished);
        }

        [Test]
        public void WhitespaceOnlyLine_GoesStraightToWait()
        {
            var p = new DialogueLinePlayback(autoAdvance: true);
            var finished = 0;
            p.Finished += () => finished++;
            p.Begin("   ", false);
            Assert.AreEqual(DialogueLinePhase.Waiting, p.Phase);
            p.Tick(2.5f);
            Assert.AreEqual(1, finished);
        }

        [Test]
        public void Stop_GoesIdleWithoutFinishing()
        {
            var p = new DialogueLinePlayback(autoAdvance: true);
            var finished = 0;
            p.Finished += () => finished++;
            p.Begin("가나다", false);
            p.Stop();
            p.Tick(10f);
            Assert.AreEqual(DialogueLinePhase.Idle, p.Phase);
            Assert.AreEqual(0, finished);
        }
    }
}
