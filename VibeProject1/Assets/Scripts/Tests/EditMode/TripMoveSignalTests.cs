using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Core.Tests
{
    /// <summary>
    /// 이동 신호 소비자(판정 시계·날짜 시계)의 프레임 스킵 회귀 방지(설계 93 §9-1). 렉 등으로 한 프레임에 이동 초가 크게 들어와도
    /// 판정은 넘긴 경계를 시각 순서대로 모두 내고, 날짜는 넘긴 일수를 한 번에 더하며, 도착 프레임에는 판정을 내지 않아야 한다.
    /// </summary>
    public class TripMoveSignalTests
    {
        private sealed class FakeSession : ISessionState
        {
            public float Progress => DurationSeconds > 0f ? Mathf.Min(1f, ElapsedSeconds / DurationSeconds) : 1f;
            public float ElapsedSeconds { get; set; }
            public float DurationSeconds { get; set; } = 1000f;
            public event Action<float> OnProgressChanged { add { } remove { } }
            public event Action OnArrived { add { } remove { } }
            public event Action<float> OnMoved;
            public void Begin(float durationSeconds) { }
            public void Pause() { }
            public void Resume() { }
            public void ExtendDuration(float seconds) { }

            // SessionStateTracker와 같은 순서 - 경과 증가 후 실제 이동 초로 신호.
            public void Move(TripLegClock clock, float delta)
            {
                var moved = clock.Tick(delta);
                ElapsedSeconds = clock.ElapsedSeconds;
                DurationSeconds = clock.DurationSeconds;
                if (moved > 0f) OnMoved?.Invoke(moved);
            }

            public void RaiseMoved(float seconds) => OnMoved?.Invoke(seconds);
        }

        private sealed class FakeMilestones : ITripMilestoneSource
        {
            public event Action<TripMilestone> Raised;
            public void Raise(TripMilestone milestone) => Raised?.Invoke(milestone);
        }

        private sealed class FakeDate : IDateAdvancer
        {
            public readonly List<int> Advances = new();
            public void AdvanceDays(int days) => Advances.Add(days);
        }

        private GameObject host;
        private FakeSession session;
        private FakeMilestones milestones;
        private FakeDate date;
        private TripCheckClock checks;
        private TripDayClock days;
        private List<TripCheckKind> fired;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("TripMoveSignalTests");
            session = new FakeSession();
            milestones = new FakeMilestones();
            date = new FakeDate();
            checks = host.AddComponent<TripCheckClock>();
            days = host.AddComponent<TripDayClock>();
            checks.Bind(session, milestones);
            days.Bind(session, milestones, date, null);
            fired = new List<TripCheckKind>();
            checks.BattleCheck += () => fired.Add(TripCheckKind.Battle);
            checks.DialogueCheck += () => fired.Add(TripCheckKind.Dialogue);
            checks.EventCheck += () => fired.Add(TripCheckKind.Event);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(host);

        [Test]
        public void FrameSkip_MidLeg_FiresEveryCrossedCheckInTimeOrder()
        {
            session.ElapsedSeconds = 11f;
            session.RaiseMoved(11f);

            // 1.25 대화, 2.5 이벤트, 5 전투, 7.5 이벤트, 10 전투
            CollectionAssert.AreEqual(new[] { TripCheckKind.Dialogue, TripCheckKind.Event, TripCheckKind.Battle, TripCheckKind.Event, TripCheckKind.Battle }, fired);
            Assert.AreEqual(1.5f, checks.SecondsUntil(TripCheckKind.Event), 1e-3f); // 다음 이벤트 12.5
        }

        [Test]
        public void FrameSkip_MidLeg_AdvancesAllCrossedDaysAtOnce()
        {
            session.RaiseMoved(95f);
            CollectionAssert.AreEqual(new[] { 3 }, date.Advances);
            Assert.AreEqual(25f, ((ITripDayClockDebug)days).SecondsUntilNextDay, 1e-3f);
        }

        [Test]
        public void FrameSkip_PastArrival_NoChecks_DaysCountOnlyUpToDuration()
        {
            var clock = new TripLegClock(61f);
            session.Move(clock, 59f);
            fired.Clear();

            session.Move(clock, 20f); // 렉 - 소요 61초를 18초 넘는다

            Assert.IsEmpty(fired, "도착 프레임에는 판정을 내지 않는다");
            CollectionAssert.AreEqual(new[] { 1, 1 }, date.Advances, "넘친 18초는 이동이 아니다 - 61초 = 2일");
        }

        [Test]
        public void FrameSkip_AfterShortenToElapsed_SendsNoMove()
        {
            var clock = new TripLegClock(60f);
            session.Move(clock, 40f);
            clock.Shorten(100f); // 남은 시간 0 - 다음 틱은 이동이 아니다
            fired.Clear();
            date.Advances.Clear();

            session.Move(clock, 10f);

            Assert.IsEmpty(fired);
            Assert.IsEmpty(date.Advances);
        }

        [Test]
        public void Rout_UsesActualMovedSinceMidCity_NotRebasedLegElapsed()
        {
            session.RaiseMoved(25f);                         // 첫 구간
            milestones.Raise(TripMilestone.MidCityArrived);
            session.RaiseMoved(20f);                         // 둘째 구간 20초 - 누적 45초 = 1일, 나머지 15초
            session.ElapsedSeconds = 30f;                    // 난이도 상승 재계산으로 구간 경과만 20 → 30

            ((ITripDayRout)days).SettleRout();

            // 실제 이동 기준 15 + 20 × 0.5 = 25초 → 0일. 구간 경과(30)를 쓰면 15 + 15 = 30초 → 1일이 더해진다.
            CollectionAssert.AreEqual(new[] { 1 }, date.Advances);
        }

        [Test]
        public void TripStarted_ResetsBoth_SoPreviousTripSkipDoesNotLeak()
        {
            session.ElapsedSeconds = 4f;
            session.RaiseMoved(29f);
            milestones.Raise(TripMilestone.TripStarted);
            fired.Clear();

            session.ElapsedSeconds = 1f;
            session.RaiseMoved(1f);

            Assert.IsEmpty(fired, "판정 시계가 0부터 다시 센다");
            Assert.IsEmpty(date.Advances, "직전 상행 나머지 29초가 이어지지 않는다");
        }
    }
}
