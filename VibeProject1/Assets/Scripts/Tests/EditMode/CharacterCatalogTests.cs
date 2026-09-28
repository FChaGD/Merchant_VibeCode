using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Game.Core;

namespace Game.Core.Tests
{
    public class CharacterCatalogTests
    {
        private CharacterStatsTableAsset statsTable;
        private CharacterStringsTableAsset nameStrings;
        private MercenaryClassStringsTableAsset classStrings;
        private readonly List<Object> created = new();

        [SetUp]
        public void SetUp()
        {
            // 직업당 3명(테이블 순서: 전사 → 궁수). 캐릭터마다 스탯이 달라야 행 공유가 없음을 확인할 수 있다.
            var entries = new List<CharacterStatsEntry>();
            var names = new List<SlugLocalizedStringEntry>();
            foreach (var (mercenaryClass, label) in new[] { ("Warrior", "전사"), ("Archer", "궁수") })
            {
                for (var i = 1; i <= 3; i++)
                {
                    var id = $"{mercenaryClass}0{i}";
                    entries.Add(new CharacterStatsEntry { Id = id, MercenaryClass = mercenaryClass, MaxHp = 100 + i, Attack = 10, Defense = 5, MoveSpeed = 3, AttackInterval = 1, Range = 1.5f, MoraleSyncRate = 5, HireCost = 1000 });
                    names.Add(new SlugLocalizedStringEntry { Id = id, Ko = $"{label}이름{i}" });
                }
            }

            statsTable = Create<CharacterStatsTableAsset>();
            SetPrivateField(statsTable, "entries", entries);
            nameStrings = Create<CharacterStringsTableAsset>();
            SetPrivateField(nameStrings, "strings", names);
            classStrings = Create<MercenaryClassStringsTableAsset>();
            SetPrivateField(classStrings, "strings", new List<SlugLocalizedStringEntry>
            {
                new() { Id = "Warrior", Ko = "전사" },
                new() { Id = "Archer", Ko = "궁수" },
            });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in created) Object.DestroyImmediate(obj);
            created.Clear();
        }

        [Test]
        public void Catalog_JoinsNameClassLabelAndOwnStats()
        {
            var catalog = new TableCharacterCatalog(statsTable, nameStrings, classStrings);

            Assert.AreEqual(6, catalog.All.Count);
            Assert.IsTrue(catalog.TryGet("Warrior02", out var profile));
            Assert.AreEqual("전사이름2", profile.Name);
            Assert.AreEqual("Warrior", profile.MercenaryClass);
            Assert.AreEqual("전사", profile.ClassLabel);
            Assert.AreEqual(102f, profile.Stats.MaxHp);
            Assert.AreEqual(1000, profile.HireCost);
            Assert.IsFalse(catalog.TryGet("Unknown", out _));
        }

        [Test]
        public void StatProvider_LooksUpByCharacterId_AndClassRepresentativeIsFirstRow()
        {
            var provider = new TableBattleUnitStatProvider(statsTable);

            Assert.AreEqual(103f, provider.GetStats("Warrior03").MaxHp);
            Assert.AreEqual(101f, provider.GetRepresentativeStats("Archer").MaxHp);
            Assert.Throws<System.InvalidOperationException>(() => provider.GetStats("Warrior"));
        }

        [Test]
        public void PlaceholderRoster_StartsWithTwoPerClass_AndHireAppendsWithinClass()
        {
            var go = new GameObject(nameof(CharacterCatalogTests));
            created.Add(go);
            var dependencyManager = go.AddComponent<DependencyManager>();
            dependencyManager.Register<ICharacterCatalogReader>(new TableCharacterCatalog(statsTable, nameStrings, classStrings));
            var roster = go.AddComponent<PlaceholderCaravanRosterProvider>();
            roster.ResolveDependencies(dependencyManager);

            var mercenaryIds = roster.GetRoster().OfType<IMercenaryUnit>().Select(u => u.CharacterId).ToList();
            CollectionAssert.AreEqual(new[] { "Warrior01", "Warrior02", "Archer01", "Archer02" }, mercenaryIds);
            Assert.AreEqual(2, roster.CountHiredOfClass("Warrior"));

            var changed = 0;
            roster.OnHiredChanged += () => changed++;
            Assert.IsTrue(roster.TryAddHired("Warrior03"));
            Assert.IsFalse(roster.TryAddHired("Warrior03"));
            Assert.AreEqual(1, changed);

            mercenaryIds = roster.GetRoster().OfType<IMercenaryUnit>().Select(u => u.CharacterId).ToList();
            CollectionAssert.AreEqual(new[] { "Warrior01", "Warrior02", "Warrior03", "Archer01", "Archer02" }, mercenaryIds);
            // 정비창 팔레트는 캐릭터 이름이 아니라 직업명을 표시한다(기획 53번 §3.2).
            Assert.AreEqual("전사", roster.GetRoster().First(u => u.Id == "Warrior03").DisplayName);
        }

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            created.Add(asset);
            return asset;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }
    }
}
