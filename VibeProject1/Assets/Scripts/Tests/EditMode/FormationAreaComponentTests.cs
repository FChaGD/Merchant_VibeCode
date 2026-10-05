using System.Collections.Generic;
using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>대열 덩어리 라벨링과 덩어리 수 기준 연결 규칙(설계 79번 §5.5). 영역 수치는 반경 4 정사각형 마차(9 × 9) 기준.</summary>
    public class FormationAreaComponentTests
    {
        private const int Size = 50;

        private readonly Dictionary<string, IFormationUnit> units = new();

        [SetUp]
        public void SetUp()
        {
            units.Clear();
            for (var i = 1; i <= 5; i++) Add(new PlaceholderFormationUnit($"Wagon0{i}", "마차", null, FormationUnitKind.Wagon, FormationAreaShape.Square(4)));
            Add(new PlaceholderFormationUnit("Facility01", "시설", null, FormationUnitKind.Facility, FormationAreaShape.Square(4)));
            Add(new PlaceholderMercenaryUnit("Warrior01", "전사", null, "Warrior"));
        }

        private void Add(IFormationUnit unit) => units[unit.Id] = unit;

        private IFormationUnit Lookup(string id) => id != null && units.TryGetValue(id, out var unit) ? unit : null;

        private static int Slot(int column, int row) => row * Size + column;

        private static FormationLayout Empty() => FormationLayout.CreateDefault();

        [Test]
        public void Connected_OneComponent()
        {
            var noWagon = FormationArea.Compute(Empty(), Lookup);
            Assert.AreEqual(1, noWagon.ComponentCount);
            Assert.AreEqual(-1, noWagon.LargestComponentId);
            Assert.IsTrue(noWagon.WagonsConnected);

            var layout = Empty();
            layout.SetUnitId(Slot(20, 24), "Wagon01");
            layout.SetUnitId(Slot(24, 24), "Wagon02");
            var area = FormationArea.Compute(layout, Lookup);

            Assert.AreEqual(1, area.ComponentCount);
            Assert.IsTrue(area.WagonsConnected);
            var component = area.GetComponentOf(Slot(20, 24));
            Assert.AreNotEqual(-1, component);
            Assert.AreEqual(component, area.GetComponentOf(Slot(24, 24)));
            Assert.AreEqual(component, area.LargestComponentId);
            Assert.AreEqual(-1, area.GetComponentOf(Slot(0, 0))); // 연결 칸 아님
        }

        [Test]
        public void TwoFarWagons_TwoComponents_LargestHasMoreWagons()
        {
            // Wagon01(6~14)·Wagon02(10~18)은 한 덩어리, Wagon03(36~44 × 6~14)은 따로. Wagon03 덩어리가 칸 번호가 작아 번호도 작지만,
            // 마차 수가 우선이라 가장 큰 덩어리는 두 대 쪽이다.
            var layout = Empty();
            layout.SetUnitId(Slot(10, 24), "Wagon01");
            layout.SetUnitId(Slot(14, 24), "Wagon02");
            layout.SetUnitId(Slot(40, 10), "Wagon03");
            var area = FormationArea.Compute(layout, Lookup);

            Assert.AreEqual(2, area.ComponentCount);
            Assert.IsFalse(area.WagonsConnected);
            var pair = area.GetComponentOf(Slot(10, 24));
            var single = area.GetComponentOf(Slot(40, 10));
            Assert.AreNotEqual(pair, single);
            Assert.Less(single, pair);
            Assert.AreEqual(pair, area.LargestComponentId);
        }

        [Test]
        public void ThreeComponents_Count3()
        {
            var layout = Empty();
            layout.SetUnitId(Slot(5, 5), "Wagon01");
            layout.SetUnitId(Slot(25, 25), "Wagon02");
            layout.SetUnitId(Slot(45, 45), "Wagon03");
            var area = FormationArea.Compute(layout, Lookup);

            Assert.AreEqual(3, area.ComponentCount);
            Assert.IsFalse(area.WagonsConnected);
        }

        [Test]
        public void FacilityBridgesWagons_OneComponent()
        {
            // Wagon01 영역 6~14, Wagon02 영역 17~25 - 틈(15~16)을 시설(14, 영역 10~18)이 잇는다.
            var layout = Empty();
            layout.SetUnitId(Slot(10, 24), "Wagon01");
            layout.SetUnitId(Slot(21, 24), "Wagon02");
            Assert.AreEqual(2, FormationArea.Compute(layout, Lookup).ComponentCount);

            layout.SetUnitId(Slot(14, 24), "Facility01");
            var area = FormationArea.Compute(layout, Lookup);
            Assert.AreEqual(1, area.ComponentCount);
            Assert.AreEqual(area.GetComponentOf(Slot(10, 24)), area.GetComponentOf(Slot(16, 24)));
        }

        [Test]
        public void NoNewSplit_ThreeToTwo_Accepted()
        {
            // 영역 1~9, 21~29, 41~49 - 세 덩어리. 오른쪽 마차를 33(영역 29~37)으로 옮기면 가운데와 이어져 두 덩어리.
            var layout = Empty();
            layout.SetUnitId(Slot(5, 24), "Wagon01");
            layout.SetUnitId(Slot(25, 24), "Wagon02");
            layout.SetUnitId(Slot(45, 24), "Wagon03");
            Assert.AreEqual(3, FormationArea.Compute(layout, Lookup).ComponentCount);

            var relaxed = FormationAreaRules.Move(layout, Slot(45, 24), Slot(33, 24), Lookup, null, WagonPlacement.Anywhere, ConnectivityRule.NoNewSplit);
            Assert.IsTrue(relaxed.Accepted);
            Assert.AreEqual(2, FormationArea.Compute(relaxed.Layout, Lookup).ComponentCount);

            var strict = FormationAreaRules.Move(layout, Slot(45, 24), Slot(33, 24), Lookup, null, WagonPlacement.Anywhere);
            Assert.AreEqual(FormationEditRejection.Disconnected, strict.Rejection);
        }

        [Test]
        public void NoNewSplit_IncreasesComponents_Rejected()
        {
            // Wagon01(6~14)·Wagon02(14~22) 한 덩어리 + Wagon03(36~44) - 두 덩어리.
            var layout = Empty();
            layout.SetUnitId(Slot(10, 24), "Wagon01");
            layout.SetUnitId(Slot(18, 24), "Wagon02");
            layout.SetUnitId(Slot(40, 24), "Wagon03");
            Assert.AreEqual(2, FormationArea.Compute(layout, Lookup).ComponentCount);

            // Wagon02를 30(영역 26~34)으로 - 어느 쪽과도 닿지 않아 세 덩어리.
            Assert.AreEqual(FormationEditRejection.Disconnected,
                FormationAreaRules.Move(layout, Slot(18, 24), Slot(30, 24), Lookup, null, WagonPlacement.Anywhere, ConnectivityRule.NoNewSplit).Rejection);
            // 떨어진 칸에 새 마차 - 세 덩어리.
            Assert.AreEqual(FormationEditRejection.Disconnected,
                FormationAreaRules.Place(layout, units["Wagon04"], Slot(25, 5), Lookup, null, WagonPlacement.Anywhere, ConnectivityRule.NoNewSplit).Rejection);
            // 덩어리 수가 그대로인 제거는 허용(두 덩어리 → 두 덩어리).
            Assert.IsTrue(FormationAreaRules.Remove(layout, Slot(10, 24), Lookup, null, ConnectivityRule.NoNewSplit).Accepted);
        }

        [Test]
        public void NoNewSplit_WhenConnected_SameAsRequireConnected()
        {
            // Wagon02가 Wagon01(2~10)과 Wagon03(12~20)을 잇는 다리(FormationAreaTests.Move_ThatBreaksConnection_IsRejected와 같은 배치).
            var layout = Empty();
            layout.SetUnitId(Slot(6, 24), "Wagon01");
            layout.SetUnitId(Slot(11, 24), "Wagon02");
            layout.SetUnitId(Slot(16, 24), "Wagon03");

            foreach (var rule in new[] { ConnectivityRule.RequireConnected, ConnectivityRule.NoNewSplit })
            {
                Assert.AreEqual(FormationEditRejection.Disconnected,
                    FormationAreaRules.Move(layout, Slot(11, 24), Slot(16, 20), Lookup, null, WagonPlacement.WithinArea, rule).Rejection, rule.ToString());
                Assert.IsTrue(FormationAreaRules.Move(layout, Slot(11, 24), Slot(11, 28), Lookup, null, WagonPlacement.WithinArea, rule).Accepted, rule.ToString());
                Assert.AreEqual(FormationEditRejection.Disconnected,
                    FormationAreaRules.Remove(layout, Slot(11, 24), Lookup, null, rule).Rejection, rule.ToString());
                Assert.IsTrue(FormationAreaRules.Place(layout, units["Warrior01"], Slot(8, 24), Lookup, null, WagonPlacement.WithinArea, rule).Accepted, rule.ToString());
            }
        }
    }
}
