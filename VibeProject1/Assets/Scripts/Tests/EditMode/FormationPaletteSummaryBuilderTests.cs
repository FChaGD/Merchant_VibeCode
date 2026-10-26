using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Core.Tests
{
    /// <summary>
    /// 정비창 팔레트 범주 요약(Docs/Refactor/2026-10-26_공통.md C-3a). 화면 클래스 안에 있어 테스트할 수 없던 계산을 순수 클래스로 뺐다.
    /// </summary>
    public class FormationPaletteSummaryBuilderTests
    {
        private sealed class Unit : IFormationUnit
        {
            public Unit(string id, FormationUnitKind kind) { Id = id; Kind = kind; DisplayName = id; }
            public string Id { get; }
            public string DisplayName { get; }
            public Sprite Icon => null;
            public FormationUnitKind Kind { get; }
        }

        private static bool Always(IFormationUnit _) => true;
        private static bool Never(IFormationUnit _) => false;

        [Test]
        public void GroupsByCategory_InFirstAppearanceOrder()
        {
            var roster = new List<IFormationUnit> { new Unit("f1", FormationUnitKind.Facility), new Unit("w1", FormationUnitKind.Wagon), new Unit("f2", FormationUnitKind.Facility) };

            var summaries = FormationPaletteSummaryBuilder.Build(roster, Always, Never);

            Assert.AreEqual(2, summaries.Count);
            Assert.AreEqual(FormationUnitKind.Facility, summaries[0].Key.Kind);
            Assert.AreEqual("f1", summaries[0].DisplayName, "이름은 범주 첫 유닛");
            Assert.AreEqual(FormationUnitKind.Wagon, summaries[1].Key.Kind);
        }

        [Test]
        public void CountsTotalAndAvailable()
        {
            var roster = new List<IFormationUnit> { new Unit("w1", FormationUnitKind.Wagon), new Unit("w2", FormationUnitKind.Wagon), new Unit("w3", FormationUnitKind.Wagon) };

            var summaries = FormationPaletteSummaryBuilder.Build(roster, Always, unit => unit.Id == "w2");

            Assert.AreEqual(3, summaries[0].TotalCount);
            Assert.AreEqual(2, summaries[0].AvailableCount);
        }

        [Test]
        public void HiddenFromPalette_Excluded()
        {
            var roster = new List<IFormationUnit> { new Unit("w1", FormationUnitKind.Wagon), new Unit("f1", FormationUnitKind.Facility) };

            var summaries = FormationPaletteSummaryBuilder.Build(roster, unit => unit.Kind != FormationUnitKind.Wagon, Never);

            Assert.AreEqual(1, summaries.Count);
            Assert.AreEqual(FormationUnitKind.Facility, summaries[0].Key.Kind);
        }

        [Test]
        public void EmptyRoster_Empty()
        {
            Assert.AreEqual(0, FormationPaletteSummaryBuilder.Build(new List<IFormationUnit>(), Always, Never).Count);
        }
    }
}
