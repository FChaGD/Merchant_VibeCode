using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Core.Tests
{
    /// <summary>
    /// Field 상행 시작·종료·최종 도착 처리(Docs/Refactor/2026-10-26_씬전용.md C-1, SS-1) - 이동 화면 조율자에서 떼어 낸 도메인 흐름.
    /// 의존성은 전부 선택적이다(없으면 그 처리만 건너뛴다).
    /// </summary>
    public class FieldTripLifecycleTests
    {
        private sealed class Conditions : IUnitConditionRepository
        {
            public int ResetCount;
            public bool TryGetCurrentHp(string unitId, out float currentHp) { currentHp = 0f; return false; }
            public bool IsDead(string unitId) => false;
            public void ApplyBattleResult(string unitId, float currentHp, bool died) { }
            public void ResetAllToFull() => ResetCount++;
        }

        private sealed class Milestones : ITripMilestoneSink
        {
            public readonly List<TripMilestone> Raised = new();
            public void Raise(TripMilestone milestone) => Raised.Add(milestone);
        }

        private sealed class Destination : ITripDestinationAssigner
        {
            public int? DestinationCityId { get; set; }
            public bool IsAssigned => DestinationCityId.HasValue;
#pragma warning disable CS0067
            public event Action Changed;
#pragma warning restore CS0067
            public void HandleCityClicked(int cityId, ITripRouteReader routeReader) { }
            public void HandleCityDeleted(int cityId) { }
            public void Reset() => DestinationCityId = null;
        }

        private GameObject host;
        private InMemoryTripCurrentLocationRepository location;
        private InMemoryFieldFormationActivityRepository activities;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Repos");
            location = host.AddComponent<InMemoryTripCurrentLocationRepository>();
            activities = host.AddComponent<InMemoryFieldFormationActivityRepository>();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(host);

        [Test]
        public void BeginTrip_ResetsAndRaisesStarted()
        {
            var conditions = new Conditions();
            var milestones = new Milestones();
            new FieldTripLifecycle(conditions, milestones, null, null, null, null).BeginTrip();
            Assert.AreEqual(1, conditions.ResetCount);
            CollectionAssert.AreEqual(new[] { TripMilestone.TripStarted }, milestones.Raised);
        }

        [Test]
        public void FinishTrip_UsesProcessorWhenPresent()
        {
            var processorConditions = new Conditions();
            var lifecycleConditions = new Conditions();
            var milestones = new Milestones();
            var processor = new TripEndProcessor(null, processorConditions, null, null, null);
            new FieldTripLifecycle(lifecycleConditions, milestones, processor, null, null, null).FinishTrip();
            Assert.AreEqual(1, processorConditions.ResetCount, "처리기가 사망 제거·회복을 맡는다");
            Assert.AreEqual(0, lifecycleConditions.ResetCount);
            CollectionAssert.AreEqual(new[] { TripMilestone.TripEnded }, milestones.Raised);
        }

        [Test]
        public void FinishTrip_WithoutProcessor_Resets()
        {
            var conditions = new Conditions();
            new FieldTripLifecycle(conditions, null, null, null, null, null).FinishTrip();
            Assert.AreEqual(1, conditions.ResetCount);
        }

        [Test]
        public void CompleteArrival_SetsCurrentCity_AndResetsDestination()
        {
            var destination = new Destination { DestinationCityId = 7 };
            var milestones = new Milestones();
            new FieldTripLifecycle(new Conditions(), milestones, null, activities, location, destination).CompleteArrival();
            Assert.AreEqual(7, location.CurrentCityId);
            Assert.IsNull(destination.DestinationCityId);
            CollectionAssert.AreEqual(new[] { TripMilestone.TripEnded }, milestones.Raised);
        }

        private sealed class Arrival : ITripArrivalSink
        {
            public readonly List<int> Cities = new();
            public Func<int> ReadLocation;
            public int LocationAtNotify = -1;
            public void NotifyFinalArrival(int cityId)
            {
                Cities.Add(cityId);
                LocationAtNotify = ReadLocation();
            }
        }

        [Test]
        public void CompleteArrival_NotifiesAfterLocation()
        {
            var arrival = new Arrival { ReadLocation = () => location.CurrentCityId };
            new FieldTripLifecycle(new Conditions(), null, null, activities, location, new Destination { DestinationCityId = 9 }, arrival).CompleteArrival();
            CollectionAssert.AreEqual(new[] { 9 }, arrival.Cities);
            Assert.AreEqual(9, arrival.LocationAtNotify, "위치 갱신 뒤에 알린다");

            var none = new Arrival { ReadLocation = () => location.CurrentCityId };
            new FieldTripLifecycle(new Conditions(), null, null, activities, location, new Destination(), none).CompleteArrival();
            Assert.IsEmpty(none.Cities, "목적지가 없으면 알리지 않는다");
        }

        [Test]
        public void CompleteArrival_NoDestination_KeepsLocation()
        {
            location.SetCurrentCity(3);
            new FieldTripLifecycle(new Conditions(), null, null, activities, location, new Destination()).CompleteArrival();
            Assert.AreEqual(3, location.CurrentCityId);
        }

        [Test]
        public void AllNull_DoesNotThrow()
        {
            var lifecycle = new FieldTripLifecycle(null, null, null, null, null, null);
            Assert.DoesNotThrow(() =>
            {
                lifecycle.BeginTrip();
                lifecycle.FinishTrip();
                lifecycle.CompleteArrival();
            });
        }
    }
}
