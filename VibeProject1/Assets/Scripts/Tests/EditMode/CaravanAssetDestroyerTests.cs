using System;
using System.Collections.Generic;
using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>
    /// 공용 파괴 처리(설계 91번 §4.4). 배치: Wagon01(20,24) 영역 ±4, Facility01(24,24) 영역 ±4 → 열 25~28은 시설 영역에만 있다.
    /// Warrior01(27,24)은 시설이 빠지면 대열 밖이 된다.
    /// </summary>
    public class CaravanAssetDestroyerTests
    {
        private const int Size = 50;

        private sealed class FakeFormation : IFormationRepository
        {
            public FormationLayout Layout;
            private readonly List<string> log;
            public event Action Changed;
            public FakeFormation(FormationLayout layout, List<string> log) { Layout = layout; this.log = log; }
            public bool TryLoadCurrent(out FormationLayout layout) { layout = Layout; return layout != null; }
            public void Apply(FormationLayout layout) { Layout = layout; log.Add("apply"); Changed?.Invoke(); }
        }

        private sealed class FakeRoster : ICaravanRosterProvider
        {
            public readonly List<IFormationUnit> Units = new();
            public IReadOnlyList<IFormationUnit> GetRoster() => Units;
        }

        private sealed class FakeRemover : IOwnedCaravanAssetRemover
        {
            private readonly FakeRoster roster;
            private readonly List<string> log;
            public FakeRemover(FakeRoster roster, List<string> log) { this.roster = roster; this.log = log; }
            public bool TryRemoveOwned(string id)
            {
                log.Add("remove " + id);
                return roster.Units.RemoveAll(u => u.Id == id) > 0;
            }
        }

        private List<string> log;
        private FakeRoster roster;
        private FakeFormation formation;

        private static int Slot(int column, int row) => row * Size + column;

        [SetUp]
        public void SetUp()
        {
            log = new List<string>();
            roster = new FakeRoster();
            roster.Units.Add(new PlaceholderFormationUnit("Wagon01#1", "마차", null, FormationUnitKind.Wagon, FormationAreaShape.Square(4)));
            roster.Units.Add(new PlaceholderFormationUnit("Facility01#1", "시설", null, FormationUnitKind.Facility, FormationAreaShape.Square(4)));
            roster.Units.Add(new PlaceholderMercenaryUnit("Warrior01", "전사", null, "Warrior"));

            var layout = FormationLayout.CreateDefault();
            layout.SetUnitId(Slot(20, 24), "Wagon01#1");
            layout.SetUnitId(Slot(24, 24), "Facility01#1");
            layout.SetUnitId(Slot(27, 24), "Warrior01");
            formation = new FakeFormation(layout, log);
        }

        private CaravanAssetDestroyer Destroyer() => new(formation, roster, null, new FakeRemover(roster, log));

        [Test]
        public void Destroy_Facility_ClearsSlotAndRemovesOwned()
        {
            var result = Destroyer().Destroy(new[] { "Facility01#1" });

            // 비운 시설 칸은 가장 가까운 칸이라 밀려난 전사가 들어올 수 있다 - 시설이 빠졌는지만 본다.
            Assert.AreNotEqual("Facility01#1", formation.Layout.GetUnitId(Slot(24, 24)));
            Assert.AreNotEqual("Warrior01", formation.Layout.GetUnitId(Slot(27, 24)));
            Assert.AreEqual(1, result.Relocated + result.Released);
            CollectionAssert.AreEqual(new[] { "apply", "remove Facility01#1" }, log); // 대열 정리 → 보유 제거 순서(설계 91번 §4.4)
        }

        [Test]
        public void Destroy_Empty_OnlyChecksConnection()
        {
            var result = Destroyer().Destroy(Array.Empty<string>());

            Assert.IsFalse(result.Disconnected);
            Assert.AreEqual(0, result.Relocated + result.Released);
            CollectionAssert.IsEmpty(log);
        }

        [Test]
        public void Destroy_NoFormation_StillRemovesOwned()
        {
            var result = new CaravanAssetDestroyer(null, roster, null, new FakeRemover(roster, log)).Destroy(new[] { "Facility01#1" });

            Assert.AreEqual(0, result.Relocated + result.Released);
            CollectionAssert.AreEqual(new[] { "remove Facility01#1" }, log);
        }
    }
}
