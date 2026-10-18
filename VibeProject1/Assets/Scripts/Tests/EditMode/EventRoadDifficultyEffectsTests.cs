using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class EventRoadDifficultyEffectsTests
    {
        private sealed class Leg : ICurrentLegDifficulty
        {
            public bool HasLeg => true;
            public CityPairKey CurrentPair => CityPairKey.Of(1, 2);
            public int CurrentBaseDifficulty => 40;
            public int CurrentDifficulty => 40;
            public int LegOnlyChange => 0;
            public string CurrentGradeName => "보통";
            public float CurrentBaseDurationSeconds => 100f;
            public void AdjustCurrentLeg(int change) { }
            public event Action Changed { add { } remove { } }
        }

        private sealed class Editor : ICityPairDifficultyEditor
        {
            public readonly List<(string source, int change, int? expires)> Calls = new();
            public int GetDifficulty(CityPairKey pair, int baseDifficulty) => baseDifficulty;
            public IReadOnlyList<CityPairDifficultyChange> GetChanges(CityPairKey pair) => Array.Empty<CityPairDifficultyChange>();
            public event Action<CityPairKey> Changed { add { } remove { } }
            public string Apply(CityPairKey pair, string sourceEventId, int change, int? expiresOnDay)
            {
                Calls.Add((sourceEventId, change, expiresOnDay));
                return "id";
            }
        }

        private sealed class Date : IDateReader
        {
            public GameDate Today => new(10);
            public event Action Changed { add { } remove { } }
        }

        [Test]
        public void TryApply_PassesSourceId()
        {
            var editor = new Editor();
            var effects = new EventRoadDifficultyEffects(new Leg(), editor, new Date(), () => "E1");
            Assert.IsTrue(effects.TryApply(10, EventRoadDifficultyEffects.DaysMode, 3, out _));
            Assert.IsTrue(effects.TryApply(-5, EventRoadDifficultyEffects.PermanentMode, 0, out _));
            Assert.AreEqual(("E1", 10, (int?)13), editor.Calls[0]);
            Assert.AreEqual(("E1", -5, (int?)null), editor.Calls[1]);
        }

        [Test]
        public void NoDate_DaysModeFails_OthersWork()
        {
            var editor = new Editor();
            var effects = new EventRoadDifficultyEffects(new Leg(), editor, null, () => "E1");
            Assert.IsFalse(effects.TryApply(10, EventRoadDifficultyEffects.DaysMode, 3, out _), "만료시킬 날짜가 없으면 기간 변경이 영구로 남는다 - 걸지 않는다");
            Assert.IsTrue(effects.TryApply(10, EventRoadDifficultyEffects.PermanentMode, 0, out _));
            Assert.AreEqual(1, editor.Calls.Count);
        }

        [Test]
        public void IsMode_OnlyThreeModes()
        {
            Assert.IsTrue(EventRoadDifficultyEffects.IsMode("leg"));
            Assert.IsTrue(EventRoadDifficultyEffects.IsMode("days"));
            Assert.IsTrue(EventRoadDifficultyEffects.IsMode("permanent"));
            Assert.IsFalse(EventRoadDifficultyEffects.IsMode("forever"));
            Assert.IsFalse(EventRoadDifficultyEffects.IsMode(null));
        }
    }
}
