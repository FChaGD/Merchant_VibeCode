using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Game.Core;

namespace Game.Core.Tests
{
    public class TripTravelRulesTests
    {
        private static readonly TripTravelSettings Settings = TripTravelSettings.Default();
        private const float Tolerance = 0.6f; // 초 - 기획 문서 예시는 초 단위 반올림

        private static float Seconds(int minutes, int seconds) => minutes * 60 + seconds;

        // 기획 75번 §4.1 예시(난이도 1 / 50 / 100).
        [TestCase(329f, 1, 1, 46)]
        [TestCase(329f, 50, 2, 4)]
        [TestCase(329f, 100, 2, 22)]
        [TestCase(731f, 1, 5, 38)]
        [TestCase(731f, 50, 5, 56)]
        [TestCase(731f, 100, 6, 14)]
        [TestCase(1256f, 1, 15, 37)]
        [TestCase(1256f, 50, 15, 55)]
        [TestCase(1256f, 100, 16, 13)]
        public void Duration_MatchesPlanningExamples(float distance, int difficulty, int minutes, int seconds)
        {
            Assert.AreEqual(Seconds(minutes, seconds), TripTravelRules.ComputeDurationSeconds(Settings, distance, difficulty, false), Tolerance);
        }

        [Test]
        public void Duration_BandRangesAndBoundaries()
        {
            Assert.AreEqual(60f, TripTravelRules.ComputeDurationSeconds(Settings, 0f, 1, false), 0.01f);
            Assert.AreEqual(180f, TripTravelRules.ComputeDurationSeconds(Settings, 599.999f, 100, false), 0.1f);
            Assert.AreEqual(300f, TripTravelRules.ComputeDurationSeconds(Settings, 600f, 1, false), 0.01f, "600은 중간");
            Assert.AreEqual(480f, TripTravelRules.ComputeDurationSeconds(Settings, 1099.999f, 100, false), 0.1f);
            Assert.AreEqual(900f, TripTravelRules.ComputeDurationSeconds(Settings, 1100f, 1, false), 0.01f, "1100은 긴 거리");
            Assert.Greater(TripTravelRules.ComputeDurationSeconds(Settings, 3394f, 1, false), 1200f, "긴 거리는 상한 없음");
        }

        [Test]
        public void Duration_CrossingRegion_AlwaysLongBand()
        {
            // 합이 1100 미만이어도 긴 거리 - 거리 부분 0, 최소 15분.
            Assert.AreEqual(900f, TripTravelRules.ComputeDurationSeconds(Settings, 500f, 1, true), 0.01f);
        }

        [TestCase(1, "매우 쉬움", 1.25f)]
        [TestCase(14, "매우 쉬움", 1.25f)]
        [TestCase(15, "쉬움", 1.5f)]
        [TestCase(50, "보통", 2.0f)]
        [TestCase(57, "보통", 2.0f)]
        [TestCase(58, "약간 어려움", 2.25f)]
        [TestCase(100, "매우 어려움", 2.75f)]
        public void Grade_Boundaries(int difficulty, string name, float rate)
        {
            var grade = TripTravelRules.ResolveGrade(Settings, difficulty);
            Assert.AreEqual(name, grade.Name);
            Assert.AreEqual(rate, grade.EncountersPerFiveMinutes, 0.0001f);
        }

        [Test]
        public void CheckProbability_NormalIsAboutThreePercent()
        {
            Assert.AreEqual(2f * 5f / 300f, TripTravelRules.ComputeCheckProbability(2f, 5f), 0.0001f);
        }
    }

    public class TripRoutePlannerTests
    {
        private sealed class FixedDifficulty : IRoadDifficultyReader
        {
            private readonly Dictionary<RoadKey, int> values = new();
            public FixedDifficulty Set(MapNodeId a, MapNodeId b, int value) { values[RoadKey.Of(a, b)] = value; return this; }
            public int GetDifficulty(RoadKey road) => values.TryGetValue(road, out var v) ? v : 50;
        }

        private static TripRoutePlanner Planner(WorldMap map, IRoadDifficultyReader difficulty = null)
            => new(map, difficulty ?? new FixedDifficulty(), TripTravelSettings.Default);

        // 지역 1: 도시 1(0,0) — 도시 2(500,0) — 도시 3(1000,0), 그리고 1 — 3 우회로(위로 크게 도는 두 도로, 도시 4 경유).
        private static WorldMap LineMap()
        {
            var map = new WorldMap();
            map.RestoreRegion(1, "지역1", WorldMap.DefaultRegionSize);
            map.RestoreCity(1, 1, new Vector2(0f, 0f), "A", null, string.Empty);
            map.RestoreCity(2, 1, new Vector2(500f, 0f), "B", null, string.Empty);
            map.RestoreCity(3, 1, new Vector2(1000f, 0f), "C", null, string.Empty);
            map.RestoreCity(4, 1, new Vector2(500f, 900f), "D", null, string.Empty);
            map.RestoreRoad(MapNodeId.City(1), MapNodeId.City(2));
            map.RestoreRoad(MapNodeId.City(2), MapNodeId.City(3));
            map.RestoreRoad(MapNodeId.City(1), MapNodeId.City(4));
            map.RestoreRoad(MapNodeId.City(4), MapNodeId.City(3));
            return map;
        }

        [Test]
        public void ShortestPath_SplitsAtEveryCity()
        {
            Assert.IsTrue(Planner(LineMap()).TryPlan(1, 3, out var plan));
            Assert.AreEqual(2, plan.Legs.Count, "1→2→3 (우회로 1→4→3보다 짧다)");
            Assert.AreEqual(1, plan.Legs[0].FromCityId);
            Assert.AreEqual(2, plan.Legs[0].ToCityId);
            Assert.AreEqual(3, plan.Legs[1].ToCityId);
            Assert.AreEqual(500f, plan.Legs[0].Distance, 0.01f);
            Assert.AreEqual("짧음", plan.Legs[0].BandName);
            Assert.AreEqual(plan.Legs[0].DurationSeconds + plan.Legs[1].DurationSeconds, plan.TotalDurationSeconds, 0.01f);
        }

        [Test]
        public void LegDifficulty_ComesFromItsRoad()
        {
            var difficulty = new FixedDifficulty().Set(MapNodeId.City(1), MapNodeId.City(2), 10).Set(MapNodeId.City(2), MapNodeId.City(3), 90);
            Assert.IsTrue(Planner(LineMap(), difficulty).TryPlan(1, 3, out var plan));
            Assert.AreEqual(10, plan.Legs[0].Difficulty);
            Assert.AreEqual("매우 쉬움", plan.Legs[0].GradeName);
            Assert.AreEqual(90, plan.Legs[1].Difficulty);
            Assert.AreEqual("매우 어려움", plan.Legs[1].GradeName);
        }

        [Test]
        public void CrossingRegion_IsOneLongLeg_WithLengthWeightedDifficulty()
        {
            // 지역1 도시 1(0,0) — 관문 1(300,0) ⇒ 관문 2(지역2, 0,0) — 도시 2(지역2, 100,0)
            var map = new WorldMap();
            map.RestoreRegion(1, "지역1", WorldMap.DefaultRegionSize);
            map.RestoreRegion(2, "지역2", WorldMap.DefaultRegionSize);
            map.RestoreCity(1, 1, Vector2.zero, "A", null, string.Empty);
            map.RestoreCity(2, 2, new Vector2(100f, 0f), "B", null, string.Empty);
            map.RestoreGate(1, 1, new Vector2(300f, 0f), 2);
            map.RestoreGate(2, 2, Vector2.zero, 1);
            map.RestoreRoad(MapNodeId.City(1), MapNodeId.Gate(1));
            map.RestoreRoad(MapNodeId.Gate(2), MapNodeId.City(2));
            var difficulty = new FixedDifficulty().Set(MapNodeId.City(1), MapNodeId.Gate(1), 20).Set(MapNodeId.Gate(2), MapNodeId.City(2), 100);

            Assert.IsTrue(Planner(map, difficulty).TryPlan(1, 2, out var plan));
            Assert.AreEqual(1, plan.Legs.Count, "관문을 지나도 다음 도시까지 구간 하나");
            var leg = plan.Legs[0];
            Assert.IsTrue(leg.CrossesRegion);
            Assert.AreEqual(400f, leg.Distance, 0.01f, "관문 짝 사이는 0");
            Assert.AreEqual("긴", leg.BandName, "지역을 넘으면 거리와 무관하게 긴 거리");
            Assert.AreEqual(40, leg.Difficulty, "(20×300 + 100×100) ÷ 400");
            Assert.GreaterOrEqual(leg.DurationSeconds, 900f);
        }

        [Test]
        public void Unreachable_ReturnsFalse()
        {
            var map = LineMap();
            map.RestoreCity(5, 1, new Vector2(-900f, 0f), "E", null, string.Empty);
            Assert.IsFalse(Planner(map).TryPlan(1, 5, out _));
            Assert.IsFalse(Planner(map).TryPlan(1, 1, out _), "자기 자신");
        }

        [Test]
        public void RoadBaseDifficulty_RestoredFromData()
        {
            var map = LineMap();
            map.RestoreRoad(MapNodeId.City(3), MapNodeId.City(4), 77); // 이미 있는 도로라 무시
            Assert.AreEqual(50, map.GetRoadBaseDifficulty(RoadKey.Of(MapNodeId.City(4), MapNodeId.City(3))), "기존 도로 값 유지");
            var other = new WorldMap();
            other.RestoreRegion(1, "지역1", WorldMap.DefaultRegionSize);
            other.RestoreCity(1, 1, Vector2.zero, "A", null, string.Empty);
            other.RestoreCity(2, 1, Vector2.one, "B", null, string.Empty);
            other.RestoreRoad(MapNodeId.City(2), MapNodeId.City(1), 77);
            Assert.AreEqual(77, other.GetRoadBaseDifficulty(RoadKey.Of(MapNodeId.City(1), MapNodeId.City(2))), "방향 무관");
        }
    }
}
