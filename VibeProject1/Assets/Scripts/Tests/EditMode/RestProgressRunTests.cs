using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>휴식 하루치 대기 측정(설계 109 §4.1) - 하루 경계에서 멈추고 다음 날 시작 전까지 진행하지 않는다.</summary>
    public class RestProgressRunTests
    {
        [Test]
        public void Advance_ReachesDayBoundary_ReturnsTrueOnce()
        {
            var run = new RestProgressRun(3, 2f);

            Assert.IsFalse(run.Advance(1.5f));
            Assert.IsTrue(run.Advance(0.5f));
            Assert.IsTrue(run.IsDayReady);
            Assert.IsFalse(run.Advance(1f), "경계에서 멈춘 동안은 다시 알리지 않는다");
        }

        [Test]
        public void LargeDelta_StopsAtDayBoundary()
        {
            var run = new RestProgressRun(5, 2f);

            Assert.IsTrue(run.Advance(30f));
            Assert.AreEqual(1f / 5f, run.Progress, 0.0001f, "큰 지연에도 하루만 진행");
        }

        [Test]
        public void WhileReady_AdvanceIgnored()
        {
            var run = new RestProgressRun(4, 1f);
            run.Advance(1f);
            run.Advance(5f);

            Assert.AreEqual(1f / 4f, run.Progress, 0.0001f);
        }

        [Test]
        public void BeginNextDay_ResumesFromZero()
        {
            var run = new RestProgressRun(4, 2f);
            run.Advance(2f);
            run.BeginNextDay(1);

            Assert.IsFalse(run.IsDayReady);
            Assert.IsFalse(run.Advance(1f));
            Assert.AreEqual(1.5f / 4f, run.Progress, 0.0001f);
            Assert.IsTrue(run.Advance(1f));
        }

        [Test]
        public void Progress_UsesSettledDays()
        {
            var run = new RestProgressRun(4, 2f);
            run.Advance(2f);
            run.BeginNextDay(0);   // 미성립 - 성립 일수 기준이라 게이지가 되돌아간다(실제 흐름에선 미성립이면 휴식이 끝난다)

            Assert.AreEqual(0f, run.Progress, 0.0001f);
        }

        [Test]
        public void Stopped_Ignored()
        {
            var run = new RestProgressRun(5, 1f);
            run.Advance(0.5f);
            run.Stop();

            Assert.IsTrue(run.IsStopped);
            Assert.IsFalse(run.Advance(5f));
            Assert.AreEqual(0.5f / 5f, run.Progress, 0.0001f);
        }

        [Test]
        public void ZeroDays_ProgressIsOne()
        {
            var run = new RestProgressRun(0, 1f);
            Assert.AreEqual(1f, run.Progress);
            Assert.IsFalse(run.Advance(1f));
        }
    }
}
