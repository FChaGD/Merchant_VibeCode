using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class FinishStepTripSignalTests
    {
        private sealed class RecordingHalt : ITripHaltControl
        {
            public readonly List<string> Calls = new();
            public readonly HashSet<TripHaltReason> Held = new();
            public bool IsHalted => Held.Count > 0;
            public bool IsHeld(TripHaltReason reason) => Held.Contains(reason);
            public event Action HaltStarted { add { } remove { } }
            public event Action HaltEnded { add { } remove { } }
            public void Acquire(TripHaltReason reason) { Calls.Add($"Acquire:{reason}"); Held.Add(reason); }
            public void Release(TripHaltReason reason) { Calls.Add($"Release:{reason}"); Held.Remove(reason); }
            public void ClearAll() { Calls.Add("ClearAll"); Held.Clear(); }
        }

        private sealed class RecordingSink : ITripMilestoneSink
        {
            public readonly List<TripMilestone> Raised = new();
            public void Raise(TripMilestone milestone) => Raised.Add(milestone);
        }

        private sealed class RecordingEventSink : IEventBattleOutcomeSink
        {
            public readonly List<EventBattleOutcome> Outcomes = new();
            public void OnEventBattleFinished(EventBattleOutcome outcome) => Outcomes.Add(outcome);
        }

        [Test]
        public void ContinueTrip_ReleasesBattle_ThenRaisesBattleEnded()
        {
            var halt = new RecordingHalt();
            var sink = new RecordingSink();
            var legacyCalled = false;
            FinishStep.ResumeTrip(new FieldTripSignals(halt, sink), () => legacyCalled = true);
            CollectionAssert.AreEqual(new[] { "Release:Battle" }, halt.Calls);
            CollectionAssert.AreEqual(new[] { TripMilestone.BattleEnded }, sink.Raised);
            Assert.IsFalse(legacyCalled);
        }

        [Test]
        public void ContinueTrip_WithoutHalt_UsesLegacyResume()
        {
            var legacyCalled = false;
            FinishStep.ResumeTrip(new FieldTripSignals(null, null), () => legacyCalled = true);
            Assert.IsTrue(legacyCalled);
        }

        [Test]
        public void EventBattle_Victory_KeepsHalt_NoBattleEnded()
        {
            var halt = new RecordingHalt();
            halt.Held.Add(TripHaltReason.Event);
            halt.Held.Add(TripHaltReason.Battle);
            var sink = new RecordingSink();
            var events = new RecordingEventSink();
            FinishStep.ResumeTrip(new FieldTripSignals(halt, sink, events), () => Assert.Fail("직접 재개 금지"), EventBattleOutcome.Victory);
            CollectionAssert.AreEqual(new[] { "Release:Battle" }, halt.Calls);
            Assert.IsTrue(halt.IsHeld(TripHaltReason.Event), "이벤트 정지는 남는다");
            CollectionAssert.IsEmpty(sink.Raised, "전투 종료 신호는 이벤트가 끝날 때");
            CollectionAssert.AreEqual(new[] { EventBattleOutcome.Victory }, events.Outcomes);
        }

        [Test]
        public void EventBattle_Flee_PassesFlee()
        {
            var halt = new RecordingHalt();
            halt.Held.Add(TripHaltReason.Event);
            var events = new RecordingEventSink();
            FinishStep.ResumeTrip(new FieldTripSignals(halt, new RecordingSink(), events), () => { }, EventBattleOutcome.Flee);
            CollectionAssert.AreEqual(new[] { EventBattleOutcome.Flee }, events.Outcomes);
        }

        [Test]
        public void IsInEvent_OnlyWhenEventHeldAndSinkPresent()
        {
            var halt = new RecordingHalt();
            Assert.IsFalse(FinishStep.IsInEvent(new FieldTripSignals(halt, null, new RecordingEventSink())));
            halt.Held.Add(TripHaltReason.Event);
            Assert.IsTrue(FinishStep.IsInEvent(new FieldTripSignals(halt, null, new RecordingEventSink())));
            Assert.IsFalse(FinishStep.IsInEvent(new FieldTripSignals(halt, null, null)));
        }
    }
}
