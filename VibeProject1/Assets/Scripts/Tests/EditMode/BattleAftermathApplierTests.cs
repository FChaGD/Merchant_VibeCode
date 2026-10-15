using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Core.Tests
{
    /// <summary>
    /// 전투 정산기의 내구도·파괴 경로(설계 91번 §4.2·§4.4, 2026-10-15 검진 권장 2). 계산기·공용 파괴 처리는 각자 단위 테스트가 있고,
    /// 여기서는 정산기가 둘을 이어 생존 개체만 저장하고 파괴 시설만 보유에서 빼는지, 포로·사망이면 아무것도 하지 않는지 본다.
    /// 대열·화물 저장소는 넘기지 않는다 - 공용 파괴 처리는 없는 쪽을 건너뛰고 보유 제거만 한다.
    /// </summary>
    public class BattleAftermathApplierTests
    {
        private sealed class FakeResolver : IDependencyResolver
        {
            private readonly Dictionary<Type, object> instances = new();
            public void Add<T>(T instance) where T : class => instances[typeof(T)] = instance;
            public T Resolve<T>() where T : class => (T)instances[typeof(T)];

            public bool TryResolve<T>(out T instance) where T : class
            {
                instance = instances.TryGetValue(typeof(T), out var value) ? (T)value : null;
                return instance != null;
            }
        }

        private sealed class FakeDurabilityWriter : ICaravanAssetDurabilityWriter
        {
            public readonly Dictionary<string, float> Saved = new();
            public bool TrySet(string instanceId, float value) { Saved[instanceId] = value; return true; }
            public bool TryRestore(string instanceId) => false;
        }

        private sealed class FakeRemover : IOwnedCaravanAssetRemover
        {
            public readonly List<string> Removed = new();
            public bool TryRemoveOwned(string id) { Removed.Add(id); return true; }
        }

        private GameObject host;
        private BattleAftermathApplier applier;
        private FakeDurabilityWriter writer;
        private FakeRemover remover;

        [SetUp]
        public void SetUp()
        {
            writer = new FakeDurabilityWriter();
            remover = new FakeRemover();
            var resolver = new FakeResolver();
            resolver.Add<ICaravanAssetDurabilityWriter>(writer);
            resolver.Add<IOwnedCaravanAssetRemover>(remover);

            host = new GameObject("BattleAftermathApplierTests");
            applier = host.AddComponent<BattleAftermathApplier>();
            applier.ResolveDependencies(resolver);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(host);

        private static BattleResult Result(BattleOutcome outcome) => new(outcome, BattleDefeatCause.None, BattleCargoReport.Empty, new[]
        {
            new ProtectedUnitRecord("Facility01#1", ProtectedUnitKind.Facility, 0f, true),
            new ProtectedUnitRecord("Facility02#1", ProtectedUnitKind.Facility, 80f, false),
            new ProtectedUnitRecord("Wagon01#1", ProtectedUnitKind.Wagon, 120f, false),
        });

        [Test]
        public void Victory_SavesSurvivorsAndRemovesDestroyedFacility()
        {
            var summary = applier.Apply(Result(BattleOutcome.Victory), null);

            CollectionAssert.AreEquivalent(new Dictionary<string, float> { ["Facility02#1"] = 80f, ["Wagon01#1"] = 120f }, writer.Saved);
            CollectionAssert.AreEqual(new[] { "Facility01#1" }, remover.Removed);
            Assert.AreEqual(1, summary.DestroyedFacilities);
        }

        [TestCase(DefeatConsequence.Flee)]
        [TestCase(DefeatConsequence.Rout)]
        public void ContinuedOrRoutedDefeat_AppliesSameDurabilitySettlement(DefeatConsequence consequence)
        {
            applier.Apply(Result(BattleOutcome.Defeat), consequence);

            Assert.AreEqual(2, writer.Saved.Count);
            CollectionAssert.AreEqual(new[] { "Facility01#1" }, remover.Removed);
        }

        [TestCase(DefeatConsequence.Captured)]
        [TestCase(DefeatConsequence.Death)]
        public void CapturedOrDeath_SkipsSettlement(DefeatConsequence consequence)
        {
            var summary = applier.Apply(Result(BattleOutcome.Defeat), consequence);

            Assert.IsEmpty(writer.Saved);
            Assert.IsEmpty(remover.Removed);
            Assert.IsTrue(summary.IsEmpty);
        }
    }
}
