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
            public bool IsHalted => false;
            public bool IsHeld(TripHaltReason reason) => false;
            public event Action HaltStarted { add { } remove { } }
            public event Action HaltEnded { add { } remove { } }
            public void Acquire(TripHaltReason reason) => Calls.Add($"Acquire:{reason}");
            public void Release(TripHaltReason reason) => Calls.Add($"Release:{reason}");
            public void ClearAll() => Calls.Add("ClearAll");
        }

        private sealed class RecordingSink : ITripMilestoneSink
        {
            public readonly List<TripMilestone> Raised = new();
            public void Raise(TripMilestone milestone) => Raised.Add(milestone);
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
    }
}
