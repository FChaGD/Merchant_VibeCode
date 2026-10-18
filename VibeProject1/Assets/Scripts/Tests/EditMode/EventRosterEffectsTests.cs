using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class EventRosterEffectsTests
    {
        private sealed class Catalog : ICharacterCatalogReader
        {
            private readonly List<CharacterProfile> all = new();
            public Catalog(params string[] ids) { foreach (var id in ids) all.Add(new CharacterProfile(id, id + "이름", "Warrior", "전사", default, 0)); }
            public IReadOnlyList<CharacterProfile> All => all;
            public bool TryGet(string id, out CharacterProfile profile) { profile = all.Find(p => p.CharacterId == id); return profile != null; }
            public bool TryGetClassLabel(string c, out string label) { label = null; return false; }
        }

        private sealed class Roster : IHiredCharacterRoster, IHiredCharacterRemover
        {
            public readonly HashSet<string> Hired = new();
            public event Action OnHiredChanged { add { } remove { } }
            public bool IsHired(string id) => Hired.Contains(id);
            public int CountHiredOfClass(string c) => Hired.Count;
            public bool TryAddHired(string id) => Hired.Add(id);
            public bool TryRemoveHired(string id) => Hired.Remove(id);
        }

        private sealed class Formation : IFormationRepository
        {
            public FormationLayout Layout = new(3, 1);
            public event Action Changed { add { } remove { } }
            public bool TryLoadCurrent(out FormationLayout layout) { layout = Layout; return true; }
            public void Apply(FormationLayout layout) => Layout = layout;
        }

        private readonly List<string> cancelled = new();
        private readonly HashSet<string> busy = new();

        private EventRosterEffects Effects(Roster roster, Formation formation)
            => new(new Catalog("Escort01", "Warrior01"), roster, roster, formation, id => busy.Contains(id), id => cancelled.Add(id));

        [Test]
        public void Join_AddsToRoster_NoPlacement()
        {
            var roster = new Roster();
            var formation = new Formation();
            Assert.AreEqual(RosterEffectResult.Done, Effects(roster, formation).Join("Escort01"));
            Assert.IsTrue(roster.IsHired("Escort01"));
            Assert.IsNull(formation.Layout.GetUnitId(0), "자동 배치 없음 - 팔레트에만 나타난다");
        }

        [Test]
        public void Join_AlreadyHired_Or_Unknown_Skips()
        {
            var roster = new Roster();
            roster.Hired.Add("Warrior01");
            var effects = Effects(roster, new Formation());
            Assert.AreEqual(RosterEffectResult.AlreadyHired, effects.Join("Warrior01"));
            Assert.AreEqual(RosterEffectResult.UnknownCharacter, effects.Join("ghost"));
        }

        [Test]
        public void Leave_CancelsActivity_ClearsSlot_RemovesHired()
        {
            var roster = new Roster();
            roster.Hired.Add("Warrior01");
            var formation = new Formation();
            formation.Layout.SetUnitId(1, "Warrior01");
            busy.Add("Warrior01");

            Assert.AreEqual(RosterEffectResult.Done, Effects(roster, formation).Leave("Warrior01"));
            CollectionAssert.AreEqual(new[] { "Warrior01" }, cancelled);
            Assert.IsNull(formation.Layout.GetUnitId(1));
            Assert.IsFalse(roster.IsHired("Warrior01"));
        }

        [Test]
        public void Leave_NotHired_Skips()
        {
            Assert.AreEqual(RosterEffectResult.NotHired, Effects(new Roster(), new Formation()).Leave("Warrior01"));
        }
    }
}
