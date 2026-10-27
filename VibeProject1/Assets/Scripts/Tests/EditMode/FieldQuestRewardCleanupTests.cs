using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    /// <summary>퀘스트 보상 즉시 정리 화면(Docs/설계/107번 §8.3).</summary>
    public class FieldQuestRewardCleanupTests
    {
        private sealed class Source : IQuestRewardCleanupSource
        {
            public bool HasPendingCleanup { get; set; }
            public void ClearPendingCleanup() => HasPendingCleanup = false;
            public event Action RewardStaged;
            public void Stage()
            {
                HasPendingCleanup = true;
                RewardStaged?.Invoke();
            }
        }

        private sealed class Halt : ITripHaltControl
        {
            private readonly HashSet<TripHaltReason> held = new();
            public readonly List<string> Calls = new();
            public bool IsHalted => held.Count > 0;
            public bool IsHeld(TripHaltReason reason) => held.Contains(reason);
            public event Action HaltStarted { add { } remove { } }
            public event Action HaltEnded;
            public void Acquire(TripHaltReason reason)
            {
                held.Add(reason);
                Calls.Add("+" + reason);
            }
            public void Release(TripHaltReason reason)
            {
                held.Remove(reason);
                Calls.Add("-" + reason);
                if (held.Count == 0) HaltEnded?.Invoke();
            }
            public void ClearAll() => held.Clear();
        }

        private Source source;
        private Halt halt;
        private QuestTestEvents events;
        private QuestTestMilestones milestones;
        private Action pendingDone;
        private int opened;

        [SetUp]
        public void SetUp()
        {
            source = new Source();
            halt = new Halt();
            events = new QuestTestEvents();
            milestones = new QuestTestMilestones();
            pendingDone = null;
            opened = 0;
        }

        private FieldQuestRewardCleanup Create(bool hasPanel = true) => new(source, halt, events, null, milestones, onDone =>
        {
            if (!hasPanel) return false;
            opened++;
            pendingDone = onDone;
            return true;
        });

        [Test]
        public void OpensImmediatelyWhenIdle_HaltAcquiredAndReleased()
        {
            using var cleanup = Create();
            source.Stage();
            Assert.AreEqual(1, opened);
            Assert.IsTrue(halt.IsHeld(TripHaltReason.QuestReward));
            pendingDone();
            Assert.IsFalse(halt.IsHeld(TripHaltReason.QuestReward));
            Assert.IsFalse(source.HasPendingCleanup);
        }

        [Test]
        public void WaitsForBattleHaltThenOpens()
        {
            using var cleanup = Create();
            halt.Acquire(TripHaltReason.Battle);
            source.Stage();
            Assert.AreEqual(0, opened);
            halt.Release(TripHaltReason.Battle);
            Assert.AreEqual(1, opened);
        }

        [Test]
        public void WaitsForEventThenOpens()
        {
            using var cleanup = Create();
            events.IsRunning = true;
            source.Stage();
            Assert.AreEqual(0, opened);
            events.IsRunning = false;
            milestones.Raise(TripMilestone.EventEnded);
            Assert.AreEqual(1, opened);
        }

        [Test]
        public void BeforeArrival_RunsThenCallback_WithoutHalt()
        {
            using var cleanup = Create();
            cleanup.BeginArrival();
            source.Stage();
            Assert.AreEqual(0, opened, "도착 처리 중에는 자동으로 열지 않는다");
            var popup = 0;
            Assert.IsTrue(cleanup.TryRunBeforeArrival(() => popup++));
            Assert.AreEqual(1, opened);
            Assert.AreEqual(0, popup);
            pendingDone();
            Assert.AreEqual(1, popup);
            CollectionAssert.IsEmpty(halt.Calls, "상행이 끝났으니 정지를 걸지 않는다");
        }

        [Test]
        public void BeforeArrival_NoPending_CallbackImmediately()
        {
            using var cleanup = Create();
            var popup = 0;
            Assert.IsFalse(cleanup.TryRunBeforeArrival(() => popup++));
            Assert.AreEqual(1, popup);
            Assert.AreEqual(0, opened);
        }

        [Test]
        public void NoPanel_ClearsAndCallsBack()
        {
            using var cleanup = Create(hasPanel: false);
            source.HasPendingCleanup = true;
            var popup = 0;
            cleanup.TryRunBeforeArrival(() => popup++);
            Assert.AreEqual(1, popup);
            Assert.IsFalse(source.HasPendingCleanup);
        }

        [Test]
        public void Disposed_StopsListening()
        {
            var cleanup = Create();
            cleanup.Dispose();
            source.Stage();
            Assert.AreEqual(0, opened);
        }
    }
}
