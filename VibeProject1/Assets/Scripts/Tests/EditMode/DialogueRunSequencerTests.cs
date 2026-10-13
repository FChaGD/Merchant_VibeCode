using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class DialogueRunSequencerTests
    {
        private readonly List<string> log = new();

        private bool Begin(DialogueRunSequencer s, string name)
            => s.TryBegin(() => log.Add("start:" + name), completed => log.Add($"done:{name}:{completed}"));

        [SetUp]
        public void Clear() => log.Clear();

        [Test]
        public void Idle_StartsImmediately_AndRejectsSecond()
        {
            var s = new DialogueRunSequencer();
            Assert.IsTrue(Begin(s, "a"));
            Assert.IsTrue(s.IsBusy);
            Assert.IsFalse(Begin(s, "b"));
            CollectionAssert.AreEqual(new[] { "start:a" }, log);
        }

        [Test]
        public void EngineComplete_FinishesTrue_ThenIdleAfterSettle()
        {
            var s = new DialogueRunSequencer();
            Begin(s, "a");
            s.NotifyEngineComplete();
            CollectionAssert.AreEqual(new[] { "start:a", "done:a:True" }, log);
            Assert.IsTrue(s.IsBusy);   // 엔진 완료 처리가 끝나는 다음 프레임까지는 새 실행을 미룬다
            s.NotifySettled();
            Assert.IsFalse(s.IsBusy);
        }

        [Test]
        public void Stop_FinishesFalseOnce_AndLateEngineCompleteIsNotTreatedAsCompletion()
        {
            var s = new DialogueRunSequencer();
            Begin(s, "a");
            Assert.IsTrue(s.Stop());
            s.NotifyEngineComplete();   // 중단으로 인한 늦은 완료 신호
            s.NotifySettled();
            CollectionAssert.AreEqual(new[] { "start:a", "done:a:False" }, log);
            Assert.IsFalse(s.IsBusy);
        }

        [Test]
        public void BeginWhileStopping_IsDeferredUntilEngineSettles()
        {
            var s = new DialogueRunSequencer();
            Begin(s, "a");
            s.Stop();
            Assert.IsTrue(Begin(s, "b"));
            CollectionAssert.AreEqual(new[] { "start:a", "done:a:False" }, log);
            s.NotifyEngineComplete();
            Assert.AreEqual(2, log.Count);   // 완료 신호 안에서는 시작하지 않는다
            s.NotifySettled();
            CollectionAssert.AreEqual(new[] { "start:a", "done:a:False", "start:b" }, log);
            s.NotifyEngineComplete();
            CollectionAssert.AreEqual(new[] { "start:a", "done:a:False", "start:b", "done:b:True" }, log);
        }

        [Test]
        public void BeginFromCompletionCallback_IsDeferred()
        {
            var s = new DialogueRunSequencer();
            s.TryBegin(() => log.Add("start:a"), _ => Begin(s, "b"));
            s.NotifyEngineComplete();
            CollectionAssert.AreEqual(new[] { "start:a" }, log);
            s.NotifySettled();
            CollectionAssert.AreEqual(new[] { "start:a", "start:b" }, log);
        }

        [Test]
        public void StopWhilePending_CancelsPendingWithoutEngineStop()
        {
            var s = new DialogueRunSequencer();
            Begin(s, "a");
            s.Stop();
            Begin(s, "b");
            Assert.IsFalse(s.Stop());   // 아직 엔진에서 시작되지 않은 대본 - 엔진 중단은 필요 없다
            s.NotifyEngineComplete();
            s.NotifySettled();
            CollectionAssert.AreEqual(new[] { "start:a", "done:a:False", "done:b:False" }, log);
            Assert.IsFalse(s.IsBusy);
        }

        [Test]
        public void StopWhenIdle_DoesNothing()
        {
            var s = new DialogueRunSequencer();
            Assert.IsFalse(s.Stop());
            s.NotifyEngineComplete();
            s.NotifySettled();
            Assert.IsEmpty(log);
            Assert.IsFalse(s.IsBusy);
        }
    }
}
