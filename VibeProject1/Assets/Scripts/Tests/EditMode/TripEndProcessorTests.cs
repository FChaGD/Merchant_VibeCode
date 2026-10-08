using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Game.Core;

namespace Game.Core.Tests
{
    public class TripEndProcessorTests
    {
        private class Merc : IMercenaryUnit
        {
            public Merc(string id)
            {
                Id = id;
                CharacterId = id;
            }

            public string Id { get; }
            public string CharacterId { get; }
            public string DisplayName => Id;
            public Sprite Icon => null;
            public FormationUnitKind Kind => FormationUnitKind.Character;
            public string Class => "Warrior";
        }

        private class Roster : ICaravanRosterProvider, IHiredCharacterRemover
        {
            public readonly List<IFormationUnit> Units = new();
            public IReadOnlyList<IFormationUnit> GetRoster() => Units;
            public bool TryRemoveHired(string id) => Units.RemoveAll(u => u.Id == id) > 0;
        }

        private class Conditions : IUnitConditionRepository
        {
            public readonly HashSet<string> Dead = new();
            public int ResetCount;
            public bool TryGetCurrentHp(string unitId, out float currentHp)
            {
                currentHp = 0;
                return false;
            }
            public bool IsDead(string unitId) => Dead.Contains(unitId);
            public void ApplyBattleResult(string unitId, float currentHp, bool died) { }
            public void ResetAllToFull()
            {
                ResetCount++;
                Dead.Clear();
            }
        }

        private class Formation : IFormationRepository
        {
            public FormationLayout Layout = new(3, 1);
            public event Action Changed;
            public bool TryLoadCurrent(out FormationLayout layout)
            {
                layout = Layout;
                return true;
            }
            public void Apply(FormationLayout layout)
            {
                Layout = layout;
                Changed?.Invoke();
            }
        }

        private class Deceased : IDeceasedCharacterRecorder
        {
            public readonly List<string> Recorded = new();
            public void Record(string id) => Recorded.Add(id);
        }

        [Test]
        public void Finish_ClearsDeadFromLayout_RemovesFromRoster_Records_ThenResets()
        {
            var roster = new Roster();
            roster.Units.Add(new Merc("alive"));
            roster.Units.Add(new Merc("dead"));
            var conditions = new Conditions();
            conditions.Dead.Add("dead");
            var formation = new Formation();
            formation.Layout.SetUnitId(0, "alive");
            formation.Layout.SetUnitId(1, "dead");
            var deceased = new Deceased();

            new TripEndProcessor(roster, conditions, formation, roster, deceased).Finish();

            Assert.AreEqual("alive", formation.Layout.GetUnitId(0));
            Assert.IsNull(formation.Layout.GetUnitId(1));
            Assert.AreEqual(1, roster.Units.Count);
            CollectionAssert.AreEqual(new[] { "dead" }, deceased.Recorded);
            Assert.AreEqual(1, conditions.ResetCount);
        }

        [Test]
        public void Finish_NoDead_OnlyResets()
        {
            var roster = new Roster();
            roster.Units.Add(new Merc("alive"));
            var conditions = new Conditions();
            var formation = new Formation();
            var applied = 0;
            formation.Changed += () => applied++;

            new TripEndProcessor(roster, conditions, formation, roster, new Deceased()).Finish();

            Assert.AreEqual(0, applied, "사망자가 없으면 배치를 다시 적용하지 않는다");
            Assert.AreEqual(1, conditions.ResetCount);
        }

        [Test]
        public void Finish_NullDependencies_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => new TripEndProcessor(null, null, null, null, null).Finish());
        }
    }
}
