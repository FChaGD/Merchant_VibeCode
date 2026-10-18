using System;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class CurrentLegRuntimeTests
    {
        private sealed class Pairs : ICityPairDifficultyReader
        {
            public int Delta;
            public int GetDifficulty(CityPairKey pair, int baseDifficulty) => TripTravelRules.ClampDifficulty(baseDifficulty + Delta);
            public System.Collections.Generic.IReadOnlyList<CityPairDifficultyChange> GetChanges(CityPairKey pair) => Array.Empty<CityPairDifficultyChange>();
            public event Action<CityPairKey> Changed { add { } remove { } }
        }

        private static TripLeg Leg(int difficulty, int baseDifficulty)
            => new(1, 2, 500f, difficulty, false, "짧음", "보통", 100f, 2f, 1, baseDifficulty);

        private static CurrentLegRuntime Runtime(Pairs pairs) => new(TripTravelSettings.Default, pairs);

        [Test]
        public void Reset_UsesPlannedValues()
        {
            var runtime = Runtime(new Pairs());
            runtime.Reset(Leg(40, 40));
            Assert.AreEqual(40, runtime.Difficulty);
            Assert.AreEqual(0, runtime.LegOnlyChange);
            Assert.AreEqual(CityPairKey.Of(2, 1), runtime.Pair);
            Assert.AreEqual("보통", runtime.GradeName);
        }

        [Test]
        public void LegOnly_Clamps_AndChangesRateGradeAndDuration()
        {
            var runtime = Runtime(new Pairs());
            runtime.Reset(Leg(95, 95));
            var before = runtime.BaseDurationSeconds;
            Assert.IsTrue(runtime.AdjustLegOnly(10));
            Assert.AreEqual(100, runtime.Difficulty);
            Assert.AreEqual(10, runtime.LegOnlyChange, "요청 변경량 그대로 기록 - 읽을 때 자른다");
            Assert.Greater(runtime.BaseDurationSeconds, before);
            var grade = TripTravelRules.ResolveGrade(TripTravelSettings.Default(), 100);
            Assert.AreEqual(grade.EncountersPerFiveMinutes, runtime.EncountersPerFiveMinutes);
            Assert.AreEqual(grade.Name, runtime.GradeName);
            runtime.AdjustLegOnly(-10);
            Assert.AreEqual(95, runtime.Difficulty, "95 +10 −10 = 95 - 도시 쌍 방식과 같은 규칙");
        }

        [Test]
        public void Recompute_AddsPairDelta_KeepsLegOnly()
        {
            var pairs = new Pairs();
            var runtime = Runtime(pairs);
            runtime.Reset(Leg(40, 40));
            runtime.AdjustLegOnly(5);
            pairs.Delta = 10;
            Assert.IsTrue(runtime.Recompute());
            Assert.AreEqual(55, runtime.Difficulty);
            Assert.IsFalse(runtime.Recompute(), "값이 같으면 false");
        }

        [Test]
        public void Reset_PairDeltaExpiredAfterPlanning_UsesCurrentValue()
        {
            // 출발 때 도시 쌍 +10이 반영돼 60으로 계획됐지만, 그 구간에 들어서기 전에 기간이 끝났다.
            var runtime = Runtime(new Pairs { Delta = 0 });
            runtime.Reset(Leg(60, 50));
            Assert.AreEqual(50, runtime.Difficulty);
            Assert.AreEqual(TripTravelRules.ResolveGrade(TripTravelSettings.Default(), 50).Name, runtime.GradeName);
        }

        [Test]
        public void NoLeg_NothingChanges()
        {
            var runtime = Runtime(new Pairs());
            Assert.IsFalse(runtime.AdjustLegOnly(10));
            Assert.IsFalse(runtime.Recompute());
        }
    }
}
