using System;
using NUnit.Framework;
using UnityEngine;

namespace Game.Core.Tests
{
    public class FieldUIHideStateTests
    {
        // 원인이 지금 하나뿐이라 "다른 원인"은 가상 값으로 만든다 - 원인 집합 규칙만 확인한다.
        private const FieldUIHideReason Other = (FieldUIHideReason)99;

        [Test]
        public void AcquireRelease_ReportsTransitions()
        {
            var s = new FieldUIHideState();
            Assert.IsTrue(s.Acquire(FieldUIHideReason.Event));
            Assert.IsTrue(s.IsHidden);
            Assert.IsFalse(s.Acquire(FieldUIHideReason.Event), "같은 원인 중복은 변화 없음");
            Assert.IsTrue(s.Release(FieldUIHideReason.Event));
            Assert.IsFalse(s.IsHidden);
            Assert.IsFalse(s.Release(FieldUIHideReason.Event));
        }

        [Test]
        public void OtherReasonKeepsHidden()
        {
            var s = new FieldUIHideState();
            s.Acquire(FieldUIHideReason.Event);
            Assert.IsFalse(s.Acquire(Other));
            Assert.IsFalse(s.Release(FieldUIHideReason.Event), "다른 원인이 남아 계속 가림");
            Assert.IsTrue(s.IsHidden);
        }

        private sealed class Milestones : ITripMilestoneSource
        {
            public event Action<TripMilestone> Raised;
            public void Raise(TripMilestone m) => Raised?.Invoke(m);
        }

        [Test]
        public void Coordinator_TripStartOrEnd_ClearsAndNotifies()
        {
            var go = new GameObject("FieldManager");
            try
            {
                var hide = go.AddComponent<FieldUIHideCoordinator>();
                var milestones = new Milestones();
                hide.Bind(milestones);
                var changes = 0;
                hide.Changed += () => changes++;
                hide.Acquire(FieldUIHideReason.Event);
                milestones.Raise(TripMilestone.TripEnded);
                Assert.IsFalse(hide.IsHidden);
                Assert.AreEqual(2, changes);
                milestones.Raise(TripMilestone.TripStarted);
                Assert.AreEqual(2, changes, "이미 비었으면 신호 없음");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
