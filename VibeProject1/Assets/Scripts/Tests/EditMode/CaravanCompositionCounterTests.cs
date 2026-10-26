using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>재화 HUD 보유 수 집계·표기(Docs/설계/101번 §4.2). 세는 기준은 정비창 팔레트와 같은 보유 목록.</summary>
    public class CaravanCompositionCounterTests
    {
        private sealed class Unit : IFormationUnit
        {
            public Unit(FormationUnitKind kind) { Kind = kind; }
            public string Id => Kind.ToString();
            public string DisplayName => Kind.ToString();
            public Sprite Icon => null;
            public FormationUnitKind Kind { get; }
        }

        [Test]
        public void Count_ByKind()
        {
            var units = new List<IFormationUnit>
            {
                new Unit(FormationUnitKind.Wagon), new Unit(FormationUnitKind.Wagon),
                new Unit(FormationUnitKind.Facility),
                new Unit(FormationUnitKind.Character), new Unit(FormationUnitKind.Character), new Unit(FormationUnitKind.Character),
            };

            var composition = CaravanCompositionCounter.Count(units);

            Assert.AreEqual(2, composition.Wagons);
            Assert.AreEqual(1, composition.Facilities);
            Assert.AreEqual(3, composition.Characters);
        }

        [Test]
        public void Count_EmptyOrNull_Zero()
        {
            var empty = CaravanCompositionCounter.Count(new List<IFormationUnit>());
            var none = CaravanCompositionCounter.Count(null);

            Assert.AreEqual(0, empty.Wagons + empty.Facilities + empty.Characters);
            Assert.AreEqual(0, none.Wagons + none.Facilities + none.Characters);
        }

        [Test]
        public void Format_MatchesSpec()
        {
            Assert.AreEqual("3", CaravanCompositionCounter.FormatWagons(3));
            Assert.AreEqual("5/2", CaravanCompositionCounter.FormatUnits(5, 2));
        }
    }
}
