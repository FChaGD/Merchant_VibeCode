using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class TripHaltControllerTests
    {
        private sealed class FakeSession : ISessionState
        {
            public readonly List<string> Calls = new();
            public float Progress => 0f;
            public float ElapsedSeconds => 0f;
            public float DurationSeconds => 1f;
            public event Action<float> OnProgressChanged { add { } remove { } }
            public event Action OnArrived { add { } remove { } }
            public event Action<float> OnMoved { add { } remove { } }
            public void Begin(float durationSeconds) { }
            public void Pause() => Calls.Add("Pause");
            public void Resume() => Calls.Add("Resume");
            public void ExtendDuration(float seconds) { }
        }

        private sealed class FakeActivities : IFieldFormationActivityRepository
        {
            public readonly List<string> Calls = new();
            public IReadOnlyList<FormationActivity> ActiveActivities => Array.Empty<FormationActivity>();
            public bool TryGetActivity(string unitId, out FormationActivity activity) { activity = null; return false; }
            public bool IsSlotReserved(int slotIndex) => false;
            public bool IsUnitBusy(string unitId) => false;
            public void BeginAdd(string unitId, int targetSlotIndex, float requiredSeconds) { }
            public void BeginMove(string unitId, int originSlotIndex, int targetSlotIndex, IReadOnlyList<int> pathSlotIndices, float requiredSeconds) { }
            public void RedirectMove(string unitId, int newTargetSlotIndex, IReadOnlyList<int> pathSlotIndices, float requiredSeconds, float elapsedSeconds, int partialSegmentIndex, float partialSegmentWeight) { }
            public void Cancel(string unitId) { }
            public void PauseAll() => Calls.Add("PauseAll");
            public void ResumeAdding() => Calls.Add("ResumeAdding");
            public void ResumeAll() => Calls.Add("ResumeAll");
            public void ForceCompleteAll() { }
            public event Action<FormationActivity> OnActivityCompleted { add { } remove { } }
            public event Action<string> OnActivityCancelled { add { } remove { } }
        }

        [Test]
        public void FirstAcquire_PausesBoth_LastRelease_ResumesBoth()
        {
            var s = new FakeSession();
            var a = new FakeActivities();
            var c = new TripHaltController(s, a);
            int started = 0, ended = 0;
            c.HaltStarted += () => started++;
            c.HaltEnded += () => ended++;

            c.Acquire(TripHaltReason.Battle);
            c.Release(TripHaltReason.Battle);

            CollectionAssert.AreEqual(new[] { "Pause", "Resume" }, s.Calls);
            CollectionAssert.AreEqual(new[] { "PauseAll", "ResumeAll" }, a.Calls);
            Assert.AreEqual((1, 1), (started, ended));
        }

        [Test]
        public void ReleasingBattle_WhileEventHeld_RepausesActivities_AndDoesNotResume()
        {
            var s = new FakeSession();
            var a = new FakeActivities();
            var c = new TripHaltController(s, a);
            c.Acquire(TripHaltReason.Event);
            c.Acquire(TripHaltReason.Battle);
            a.Calls.Clear();
            s.Calls.Clear();

            c.Release(TripHaltReason.Battle);   // 전투 중 배치 재개(ResumeAdding)를 되돌려야 한다

            Assert.IsTrue(c.IsHalted);
            CollectionAssert.AreEqual(new[] { "PauseAll" }, a.Calls);
            CollectionAssert.IsEmpty(s.Calls);

            c.Release(TripHaltReason.Event);
            CollectionAssert.AreEqual(new[] { "Resume" }, s.Calls);
        }

        [Test]
        public void ReleaseOfUnheldReason_IsNoOp()
        {
            var s = new FakeSession();
            var c = new TripHaltController(s, null);
            c.Release(TripHaltReason.Event);
            CollectionAssert.IsEmpty(s.Calls);
        }

        [Test]
        public void ClearAll_DropsReasonsWithoutResuming_ThenNextAcquireStartsAgain()
        {
            var s = new FakeSession();
            var c = new TripHaltController(s, null);
            c.Acquire(TripHaltReason.Battle);
            c.ClearAll();                         // 궤주로 상행 종료
            Assert.IsFalse(c.IsHalted);
            CollectionAssert.AreEqual(new[] { "Pause" }, s.Calls);

            var started = 0;
            c.HaltStarted += () => started++;
            c.Acquire(TripHaltReason.Battle);
            Assert.AreEqual(1, started);
        }

        [Test]
        public void IsHeld_ReflectsReasons()
        {
            var c = new TripHaltController(new FakeSession(), null);
            c.Acquire(TripHaltReason.EventPending);
            Assert.IsTrue(c.IsHeld(TripHaltReason.EventPending));
            Assert.IsFalse(c.IsHeld(TripHaltReason.Event));
        }
    }
}
