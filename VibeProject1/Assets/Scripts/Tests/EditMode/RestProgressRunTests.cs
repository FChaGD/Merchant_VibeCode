using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>휴식 진행 상태(Docs/설계/103번 §4.2) - 한 번의 경과가 여러 날을 넘어도 빠뜨리지 않는다.</summary>
    public class RestProgressRunTests
    {
        [Test]
        public void Advance_AcrossSeveralDays_ReturnsAllNewDays()
        {
            var run = new RestProgressRun(5, 2f);

            Assert.AreEqual(0, run.Advance(1.5f));
            Assert.AreEqual(3, run.Advance(5f)); // 6.5초 → 3일
            Assert.AreEqual(3, run.ElapsedDays);
        }

        [Test]
        public void Advance_ExactBoundary_CountsDay()
        {
            var run = new RestProgressRun(3, 2f);

            Assert.AreEqual(1, run.Advance(2f));
            Assert.AreEqual(1, run.Advance(2f));
        }

        [Test]
        public void Finish_ClampsToTotal_AndProgressIsOne()
        {
            var run = new RestProgressRun(2, 1f);

            Assert.AreEqual(2, run.Advance(10f));
            Assert.IsTrue(run.IsFinished);
            Assert.AreEqual(1f, run.Progress);
            Assert.AreEqual(0, run.Advance(1f));
        }

        [Test]
        public void Progress_IsContinuous()
        {
            var run = new RestProgressRun(4, 2f);
            run.Advance(3f);

            Assert.AreEqual(3f / 8f, run.Progress, 0.0001f);
        }

        [Test]
        public void Stop_IgnoresLaterAdvance()
        {
            var run = new RestProgressRun(5, 1f);
            run.Advance(2.5f);
            run.Stop();

            Assert.IsTrue(run.IsStopped);
            Assert.AreEqual(0, run.Advance(5f));
            Assert.AreEqual(2, run.ElapsedDays);
            Assert.IsFalse(run.IsFinished);
        }
    }
}
