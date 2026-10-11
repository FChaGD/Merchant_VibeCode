using System;
using System.Collections.Generic;
using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    public class TripDateAccrualTests
    {
        private sealed class FakeSession : ISessionState
        {
            public float ElapsedSeconds { get; set; }
            public float DurationSeconds { get; set; }
            public float Progress => 0f;
            public event Action<float> OnProgressChanged { add { } remove { } }
            public event Action OnArrived { add { } remove { } }
            public void Begin(float durationSeconds) { }
            public void Resume() { }
            public void ExtendDuration(float seconds) { }
            public void Pause() { }
        }

        private sealed class RecordingAdvancer : IDateAdvancer
        {
            public readonly List<int> Calls = new();
            public void AdvanceDays(int days) => Calls.Add(days);
        }

        private sealed class FixedSettings : ITripTravelSettingsReader
        {
            public TripTravelSettings Settings { get; } = TripTravelSettings.Default();
        }

        [Test]
        public void LegArrival_UsesDuration_NotElapsed()
        {
            // 소요 90초(파괴 지연 반영값), 도착 프레임 경과는 소요를 약간 넘을 수 있다.
            var session = new FakeSession { DurationSeconds = 90f, ElapsedSeconds = 90.4f };
            var advancer = new RecordingAdvancer();
            new TripDateAccrual(session, advancer, new FixedSettings()).AccrueLegArrival();
            CollectionAssert.AreEqual(new[] { 3 }, advancer.Calls);
        }

        [Test]
        public void Rout_UsesElapsedTimesMultiplier()
        {
            var session = new FakeSession { DurationSeconds = 120f, ElapsedSeconds = 100f };
            var advancer = new RecordingAdvancer();
            new TripDateAccrual(session, advancer, new FixedSettings()).AccrueRout();
            CollectionAssert.AreEqual(new[] { 5 }, advancer.Calls);
        }

        [Test]
        public void ZeroDays_DoesNotCallAdvancer()
        {
            var session = new FakeSession { DurationSeconds = 0.01f, ElapsedSeconds = 0.01f };
            var advancer = new RecordingAdvancer();
            var accrual = new TripDateAccrual(session, advancer, new FixedSettings());
            accrual.AccrueLegArrival();
            accrual.AccrueRout();
            Assert.IsEmpty(advancer.Calls);
        }

        [Test]
        public void MissingDependencies_AreSafe()
        {
            var session = new FakeSession { DurationSeconds = 90f, ElapsedSeconds = 90f };
            Assert.DoesNotThrow(() => new TripDateAccrual(session, null, null).AccrueLegArrival());

            var advancer = new RecordingAdvancer();
            new TripDateAccrual(session, advancer, null).AccrueLegArrival(); // 이동 수치 없으면 기본값(30초)
            CollectionAssert.AreEqual(new[] { 3 }, advancer.Calls);
        }
    }
}
