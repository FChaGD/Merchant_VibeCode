using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Core.Tests
{
    /// <summary>정리 모드 끊어진 덩어리 강조 칸(설계 79번 §8). 반경 4 정사각형 마차(9 × 9) 기준.</summary>
    public class FormationDisconnectedCellsTests
    {
        private const int Size = 50;

        private readonly Dictionary<string, IFormationUnit> units = new();

        [SetUp]
        public void SetUp()
        {
            units.Clear();
            for (var i = 1; i <= 3; i++)
            {
                var wagon = new PlaceholderFormationUnit($"Wagon0{i}", "마차", null, FormationUnitKind.Wagon, FormationAreaShape.Square(4));
                units[wagon.Id] = wagon;
            }
        }

        private IFormationUnit Lookup(string id) => id != null && units.TryGetValue(id, out var unit) ? unit : null;

        private static int Slot(int column, int row) => row * Size + column;

        [Test]
        public void Connected_NoTints_AndClearsPrevious()
        {
            var layout = FormationLayout.CreateDefault();
            layout.SetUnitId(Slot(20, 24), "Wagon01");
            layout.SetUnitId(Slot(24, 24), "Wagon02");
            var tints = new Dictionary<int, Color> { [0] = Color.white };

            FormationDisconnectedCells.CollectTints(FormationArea.Compute(layout, Lookup), FormationDisconnectedCells.DefaultTint, tints);

            Assert.AreEqual(0, tints.Count);
        }

        [Test]
        public void TwoComponents_TintsOnlySmallerComponent()
        {
            // Wagon01·Wagon02는 한 덩어리(가장 큼), Wagon03은 따로.
            var layout = FormationLayout.CreateDefault();
            layout.SetUnitId(Slot(10, 24), "Wagon01");
            layout.SetUnitId(Slot(14, 24), "Wagon02");
            layout.SetUnitId(Slot(40, 10), "Wagon03");
            var area = FormationArea.Compute(layout, Lookup);
            var tints = new Dictionary<int, Color>();

            FormationDisconnectedCells.CollectTints(area, FormationDisconnectedCells.DefaultTint, tints);

            Assert.IsTrue(tints.ContainsKey(Slot(40, 10)));
            Assert.IsTrue(tints.ContainsKey(Slot(36, 6)));   // Wagon03 영역 모서리
            Assert.IsFalse(tints.ContainsKey(Slot(10, 24)));
            Assert.IsFalse(tints.ContainsKey(Slot(14, 24)));
            Assert.IsFalse(tints.ContainsKey(Slot(0, 0)));   // 연결 칸 아님
            Assert.AreEqual(81, tints.Count);                // 9 × 9
            Assert.AreEqual(FormationDisconnectedCells.DefaultTint, tints[Slot(40, 10)]);
        }
    }
}
