using NUnit.Framework;
using UnityEngine;
using Game.Core;

namespace Game.Core.Tests
{
    public class FormationExtentTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void DefaultGrid_AllyPositionsMatchLegacyFormula()
        {
            // 1단계(Docs/설계/60번 §11.1) 전환 전 공식 - 열은 판 중앙 기준 (column - (열수-1)/2), 행은 "2행 고정" (row - 0.5).
            // 간격 상수는 internal이라 이웃 칸 좌표 차이로 역산한다.
            var layout = new BattleFieldLayout();
            var columns = FormationLayout.DefaultColumnCount;
            var extent = FormationExtent.FromGrid(columns, 2);
            var origin = layout.ComputeAllyPosition(0, 0, extent);
            var columnSpacing = layout.ComputeAllyPosition(1, 0, extent).y - origin.y;
            var rowSpacing = layout.ComputeAllyPosition(0, 1, extent).x - origin.x;

            for (var column = 0; column < columns; column++)
            {
                for (var row = 0; row < 2; row++)
                {
                    var actual = layout.ComputeAllyPosition(column, row, extent);
                    Assert.AreEqual((row - 0.5f) * rowSpacing, actual.x, Tolerance);
                    Assert.AreEqual((column - (columns - 1) / 2f) * columnSpacing, actual.y, Tolerance);
                }
            }
        }

        [Test]
        public void Radii_GrowWithBothAxes()
        {
            // 예전엔 행 방향 폭이 고정이라 행을 늘려도 반경이 그대로였다.
            var layout = new BattleFieldLayout();
            var twoRows = layout.ComputeSpawnRadius(FormationExtent.FromGrid(8, 2));
            var fiveRows = layout.ComputeSpawnRadius(FormationExtent.FromGrid(8, 5));
            Assert.Greater(fiveRows, twoRows);
            Assert.AreEqual(layout.ComputeFieldRadius(FormationExtent.FromGrid(8, 5)), layout.ComputeFleeTravelDistance(FormationExtent.FromGrid(8, 5)), Tolerance);
        }

        [Test]
        public void TallerGrid_IsCenteredOnBothAxes()
        {
            // 행 2개 가정이 사라졌는지 - 5행 격자에서 가운데 칸이 전장 원점에 와야 한다.
            var layout = new BattleFieldLayout();
            var extent = FormationExtent.FromGrid(5, 5);

            var center = layout.ComputeAllyPosition(2, 2, extent);
            Assert.AreEqual(0f, center.x, Tolerance);
            Assert.AreEqual(0f, center.y, Tolerance);

            var top = layout.ComputeAllyPosition(2, 0, extent);
            var bottom = layout.ComputeAllyPosition(2, 4, extent);
            Assert.AreEqual(-top.x, bottom.x, Tolerance);
        }

        [Test]
        public void BattleTestLayout_DelegatesToSameFormulas()
        {
            var testLayout = new BattleTestFieldLayout { ColumnCount = 6, RowCount = 3 };
            var production = new BattleFieldLayout();

            Assert.AreEqual(production.ComputeSpawnRadius(testLayout.Extent), testLayout.ComputeSpawnRadius(testLayout.Extent), Tolerance);
            Assert.AreEqual(production.ComputeAllyPosition(1, 2, testLayout.Extent), testLayout.ComputeAllyPosition(1, 2, testLayout.Extent));
        }
    }
}
