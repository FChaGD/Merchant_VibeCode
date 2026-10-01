using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>월드 지도 모델·끝점 표기·테이블 검증(Docs/설계/69번 §10).</summary>
    public class WorldMapTests
    {
        private static WorldMap TwoRegions()
        {
            var map = new WorldMap();
            map.RestoreRegion(1, "지역1", WorldMap.DefaultRegionSize);
            map.RestoreRegion(2, "지역2", WorldMap.DefaultRegionSize);
            return map;
        }

        [Test]
        public void MapNodeId_ParsesAndFormats()
        {
            Assert.IsTrue(MapNodeId.TryParse("C4", out var city));
            Assert.AreEqual(MapNodeId.City(4), city);
            Assert.IsTrue(MapNodeId.TryParse(" g12 ", out var gate));
            Assert.AreEqual(MapNodeId.Gate(12), gate);
            Assert.AreEqual("G12", gate.ToString());
            Assert.IsFalse(MapNodeId.TryParse("X1", out _));
            Assert.IsFalse(MapNodeId.TryParse("C", out _));
            Assert.IsFalse(MapNodeId.TryParse("", out _));
            Assert.AreNotEqual(MapNodeId.City(1), MapNodeId.Gate(1));
        }

        [Test]
        public void Reachability_CrossesRegionsThroughGatePair()
        {
            var map = TwoRegions();
            var a = map.AddCity(1, Vector2.zero);
            var b = map.AddCity(2, Vector2.one);
            var gate = map.AddGatePair(1, new Vector2(100f, 0f), 2);
            Assert.IsTrue(map.TryGetGate(gate, out var near));
            var pair = near.PairGateId;

            Assert.IsFalse(map.IsReachable(a, b), "도로가 없으면 관문만으로는 이어지지 않는다.");
            Assert.IsTrue(map.TryAddRoad(MapNodeId.City(a), MapNodeId.Gate(gate)));
            Assert.IsTrue(map.TryAddRoad(MapNodeId.Gate(pair), MapNodeId.City(b)));
            Assert.IsTrue(map.IsReachable(a, b));
            Assert.IsTrue(map.IsReachable(b, a));
        }

        [Test]
        public void AddGatePair_CreatesPairAtTargetCenter_AndRejectsSameRegion()
        {
            var map = TwoRegions();
            Assert.AreEqual(-1, map.AddGatePair(1, Vector2.zero, 1));

            var gate = map.AddGatePair(1, new Vector2(300f, 200f), 2);
            Assert.IsTrue(map.TryGetGate(gate, out var near));
            Assert.IsTrue(map.TryGetGate(near.PairGateId, out var far));
            Assert.AreEqual(2, far.RegionId);
            Assert.AreEqual(Vector2.zero, far.Position, "짝은 대상 지역 지도 중앙(콘텐츠 원점)에 생긴다.");
            Assert.AreEqual(gate, far.PairGateId);
        }

        [Test]
        public void RemoveGate_RemovesPairAndTheirRoads()
        {
            var map = TwoRegions();
            var a = map.AddCity(1, Vector2.zero);
            var b = map.AddCity(2, Vector2.zero);
            var gate = map.AddGatePair(1, Vector2.one, 2);
            map.TryGetGate(gate, out var near);
            map.TryAddRoad(MapNodeId.City(a), MapNodeId.Gate(gate));
            map.TryAddRoad(MapNodeId.City(b), MapNodeId.Gate(near.PairGateId));

            Assert.IsTrue(map.RemoveGate(near.PairGateId));
            Assert.AreEqual(0, map.AllGates.Count());
            Assert.AreEqual(0, map.AllRoads.Count());
        }

        [Test]
        public void Road_BetweenDifferentRegions_IsRejected()
        {
            var map = TwoRegions();
            var a = map.AddCity(1, Vector2.zero);
            var b = map.AddCity(2, Vector2.zero);
            Assert.IsFalse(map.TryAddRoad(MapNodeId.City(a), MapNodeId.City(b)));
            Assert.IsFalse(map.TryAddRoad(MapNodeId.City(a), MapNodeId.City(a)));
        }

        [Test]
        public void RemoveRegion_ChecksAndRemovesContents()
        {
            var map = TwoRegions();
            var home = map.AddCity(1, Vector2.zero);
            var far = map.AddCity(2, Vector2.zero);
            var gate = map.AddGatePair(1, Vector2.one, 2);
            var removed = new List<int>();
            map.CityRemoved += removed.Add;

            Assert.AreEqual(RegionRemovalCheck.HasCurrentLocation, map.CheckRegionRemoval(1, home));
            Assert.AreEqual(RegionRemovalCheck.Allowed, map.CheckRegionRemoval(2, home));

            Assert.IsTrue(map.RemoveRegion(2, home));
            CollectionAssert.AreEqual(new[] { far }, removed);
            Assert.IsFalse(map.TryGetGate(gate, out _), "다른 지역에 있던 짝 관문도 함께 지워진다.");
            Assert.AreEqual(1, map.Regions.Count);
            Assert.AreEqual(RegionRemovalCheck.LastRegion, map.CheckRegionRemoval(1, -1));
        }

        [Test]
        public void AddRegion_UsesNextId_AndDefaultName()
        {
            var map = TwoRegions();
            var id = map.AddRegion();
            Assert.AreEqual(3, id);
            Assert.IsTrue(map.TryGetRegion(3, out var region));
            Assert.AreEqual("지역3", region.Name);
            Assert.AreEqual(WorldMap.DefaultRegionSize, region.Size);
        }

        [Test]
        public void ClearCities_AffectsOnlyThatRegion()
        {
            var map = TwoRegions();
            map.AddCity(1, Vector2.zero);
            var keep = map.AddCity(2, Vector2.zero);
            map.ClearCities(1);
            CollectionAssert.AreEqual(new[] { keep }, map.AllCities.Select(city => city.Id).ToArray());
        }

        [Test]
        public void Validator_ReportsBrokenData()
        {
            var regions = new[] { 1, 2 };
            var cities = new[] { new WorldMapDataValidator.CityRow(1, 1), new WorldMapDataValidator.CityRow(2, 2), new WorldMapDataValidator.CityRow(3, 9) };
            var gates = new[]
            {
                new WorldMapDataValidator.GateRow(1, 1, 2),
                new WorldMapDataValidator.GateRow(2, 2, 1),
                new WorldMapDataValidator.GateRow(3, 1, 4), // 짝 없음
                new WorldMapDataValidator.GateRow(5, 1, 6),
                new WorldMapDataValidator.GateRow(6, 1, 5), // 같은 지역 짝
            };
            var roads = new List<(string, string)> { ("C1", "G1"), ("C1", "C2"), ("C1", "X9"), ("C1", "C77") };

            var errors = WorldMapDataValidator.Validate(regions, cities, gates, roads);

            Assert.IsTrue(errors.Any(e => e.Contains("도시 3의 지역 9")));
            Assert.IsTrue(errors.Any(e => e.Contains("관문 3의 짝 4")));
            Assert.IsTrue(errors.Any(e => e.Contains("같은 지역")));
            Assert.IsTrue(errors.Any(e => e.Contains("C1-C2")));
            Assert.IsTrue(errors.Any(e => e.Contains("'X9'")));
            Assert.IsTrue(errors.Any(e => e.Contains("'C77'")));
            Assert.IsFalse(errors.Any(e => e.Contains("C1-G1")), "같은 지역 도시-관문 도로는 정상.");
        }

        [Test]
        public void Validator_PassesCleanData()
        {
            var errors = WorldMapDataValidator.Validate(
                new[] { 1, 2 },
                new[] { new WorldMapDataValidator.CityRow(1, 1), new WorldMapDataValidator.CityRow(2, 2) },
                new[] { new WorldMapDataValidator.GateRow(1, 1, 2), new WorldMapDataValidator.GateRow(2, 2, 1) },
                new List<(string, string)> { ("C1", "G1"), ("G2", "C2") });
            CollectionAssert.IsEmpty(errors);
        }
    }
}
