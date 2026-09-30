using System.Collections.Generic;
using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    public class FormationAreaTests
    {
        private const int Size = 50;
        private const int Anchor = 24 * Size + 24; // (열 24, 행 24)

        private readonly Dictionary<string, IFormationUnit> units = new();

        [SetUp]
        public void SetUp()
        {
            units.Clear();
            for (var i = 1; i <= 5; i++) Add(new PlaceholderFormationUnit($"Wagon0{i}", "마차", null, FormationUnitKind.Wagon, FormationAreaShape.Square(4)));
            Add(new PlaceholderFormationUnit("Facility01", "시설", null, FormationUnitKind.Facility, FormationAreaShape.Square(4)));
            Add(new PlaceholderFormationUnit("Facility02", "시설", null, FormationUnitKind.Facility, FormationAreaShape.Square(4)));
            // 마스크 모양(기획 65번 §3.1) - 가로로 긴 직사각형, 기준 칸이 맨 아래 줄인 모양, 십자, 떨어진 조각.
            Add(new PlaceholderFormationUnit("WagonWide", "마차", null, FormationUnitKind.Wagon, Mask("111111111/111121111/111111111")));
            Add(new PlaceholderFormationUnit("WagonUpOnly", "마차", null, FormationUnitKind.Wagon, Mask("11111/11111/11111/11211")));
            Add(new PlaceholderFormationUnit("WagonCross", "마차", null, FormationUnitKind.Wagon, Mask("010/121/010")));
            Add(new PlaceholderFormationUnit("WagonIsland", "마차", null, FormationUnitKind.Wagon, Mask("100002")));
            Add(new PlaceholderMercenaryUnit("Warrior01", "전사", null, "Warrior"));
            Add(new PlaceholderMercenaryUnit("Warrior02", "전사", null, "Warrior"));
        }

        private void Add(IFormationUnit unit) => units[unit.Id] = unit;

        private static FormationAreaShape Mask(string mask)
        {
            Assert.IsTrue(FormationAreaShape.TryParse(mask, out var shape, out var error), error);
            return shape;
        }

        private IFormationUnit Lookup(string id) => id != null && units.TryGetValue(id, out var unit) ? unit : null;

        private static int Slot(int column, int row) => row * Size + column;

        private static FormationLayout Empty() => FormationLayout.CreateDefault();

        [Test]
        public void NoWagon_AreaIsAnchorOnly_AndOnlyWagonMayBePlaced()
        {
            var layout = Empty();
            var area = FormationArea.Compute(layout, Lookup);

            Assert.AreEqual(Anchor, layout.AnchorSlotIndex);
            CollectionAssert.AreEquivalent(new[] { Anchor }, area.Cells);
            Assert.AreEqual(FormationEditRejection.WagonOnly, FormationAreaRules.CanPlaceAt(area, units["Warrior01"], Anchor));
            Assert.AreEqual(FormationEditRejection.None, FormationAreaRules.CanPlaceAt(area, units["Wagon01"], Anchor));
            Assert.AreEqual(FormationEditRejection.OutsideArea, FormationAreaRules.CanPlaceAt(area, units["Wagon01"], Anchor + 1));
        }

        [Test]
        public void Wagon_OpensSquareOfRadius_ClippedAtBoardEdge()
        {
            var layout = Empty();
            layout.SetUnitId(Anchor, "Wagon01");
            var area = FormationArea.Compute(layout, Lookup);

            Assert.AreEqual(81, area.Cells.Count); // 반경 4 정사각형 = 9 × 9
            Assert.IsTrue(area.Contains(Slot(28, 24)));
            Assert.IsTrue(area.Contains(Slot(24, 20)));
            Assert.IsTrue(area.Contains(Slot(28, 28))); // 대각선 모서리 포함
            Assert.IsFalse(area.Contains(Slot(29, 24)));

            var edge = Empty();
            edge.SetUnitId(Slot(0, 0), "Wagon01");
            Assert.AreEqual(25, FormationArea.Compute(edge, Lookup).Cells.Count); // 판 모서리에서 잘려 5 × 5
        }

        [Test]
        public void Wagon_OpensMaskShape_RectangleAndOffsetAnchor()
        {
            var wide = Empty();
            wide.SetUnitId(Anchor, "WagonWide"); // 9 × 3, 가운데 기준
            var wideArea = FormationArea.Compute(wide, Lookup);
            Assert.AreEqual(27, wideArea.Cells.Count); // 9 × 3
            Assert.IsTrue(wideArea.Contains(Slot(20, 23)));
            Assert.IsTrue(wideArea.Contains(Slot(28, 25)));
            Assert.IsFalse(wideArea.Contains(Slot(24, 22)));
            Assert.IsFalse(wideArea.Contains(Slot(24, 26)));

            var upOnly = Empty();
            upOnly.SetUnitId(Anchor, "WagonUpOnly"); // 5 × 4, 기준 칸이 맨 아래 줄 가운데 - 위 줄 = 행 감소
            var upArea = FormationArea.Compute(upOnly, Lookup);
            Assert.AreEqual(20, upArea.Cells.Count); // 5 × 4
            Assert.IsTrue(upArea.Contains(Slot(24, 21)));
            Assert.IsFalse(upArea.Contains(Slot(24, 25))); // 중심 칸이 맨 아래 줄
            Assert.AreEqual(new UnityEngine.RectInt(22, 21, 5, 4), upArea.Bounds);
        }

        [Test]
        public void MaskShape_ClippedOnlyOnEdgeSide()
        {
            var layout = Empty();
            layout.SetUnitId(Slot(1, 24), "WagonWide"); // 왼쪽 4칸 중 3칸이 판 밖
            Assert.AreEqual(18, FormationArea.Compute(layout, Lookup).Cells.Count); // 열 0~5(6) × 행 3
        }

        [Test]
        public void Wagon_CrossMask_OpensFiveCells()
        {
            var layout = Empty();
            layout.SetUnitId(Anchor, "WagonCross");
            var area = FormationArea.Compute(layout, Lookup);

            CollectionAssert.AreEquivalent(new[] { Anchor, Slot(24, 23), Slot(24, 25), Slot(23, 24), Slot(25, 24) }, area.Cells);
        }

        [Test]
        public void Island_IsPartOfArea_ButDoesNotConnectItsOwnWagon()
        {
            // "100002": 기준 칸 왼쪽 5칸 떨어진 조각 1칸(기획 65번 §3.5 - 허용).
            var layout = Empty();
            layout.SetUnitId(Slot(8, 30), "Wagon01");      // 영역 열 4~12
            layout.SetUnitId(Slot(18, 30), "WagonIsland"); // 조각 (13,30)이 Wagon01 영역에 맞닿음, 기준 칸과 조각 사이 14~17은 비어 있음
            var area = FormationArea.Compute(layout, Lookup);

            Assert.IsTrue(area.Contains(Slot(13, 30)));
            Assert.IsFalse(area.Contains(Slot(15, 30)));
            // 연결은 마차 칸 기준 칸 단위 판정이라, 조각만 맞닿고 기준 칸이 떨어져 있으면 그 마차는 이어지지 않는다.
            Assert.IsFalse(area.WagonsConnected);
        }

        [Test]
        public void AreaShapeParse_ValidAndInvalid()
        {
            var shape = Mask("111/111/121");
            Assert.AreEqual(9, shape.Offsets.Count);
            Assert.AreEqual(2, shape.MaxReach);
            Assert.AreEqual("111/111/121", shape.ToString());
            Assert.AreEqual(4, FormationAreaShape.Square(4).MaxReach);
            Assert.AreEqual(1, FormationAreaShape.Single.Offsets.Count);

            foreach (var invalid in new[] { "", "111/11", "1a2", "111/111", "212" })
            {
                Assert.IsFalse(FormationAreaShape.TryParse(invalid, out _, out var error), invalid);
                Assert.IsFalse(string.IsNullOrEmpty(error), invalid);
            }
        }

        [Test]
        public void MiddleWagonRemoval_Rejected_EndRemoval_ReleasesOutsideUnits()
        {
            // 마차 네 대를 4칸 간격 일렬로 놓고, 가운데를 차례로 빼며 끊기는 순간을 확인한다.
            var wide = Empty();
            wide.SetUnitId(Slot(14, 24), "Wagon01");
            wide.SetUnitId(Slot(18, 24), "Wagon02");
            wide.SetUnitId(Slot(22, 24), "Wagon03");
            wide.SetUnitId(Slot(26, 24), "Wagon04");
            var middle = FormationAreaRules.Remove(wide, Slot(18, 24), Lookup, null);
            Assert.IsTrue(middle.Accepted); // Wagon01(10~18)과 Wagon03(18~26)이 (18,24)에서 맞닿음
            var afterMiddle = middle.Layout;
            var cut = FormationAreaRules.Remove(afterMiddle, Slot(22, 24), Lookup, null);
            Assert.AreEqual(FormationEditRejection.Disconnected, cut.Rejection); // Wagon01(10~18)과 Wagon04(22~30)는 떨어짐

            // 끝 마차 제거는 허용, 그 마차 영역에만 있던 캐릭터는 자동 해제.
            var ends = Empty();
            ends.SetUnitId(Slot(20, 24), "Wagon01");
            ends.SetUnitId(Slot(24, 24), "Wagon02");
            ends.SetUnitId(Slot(27, 24), "Warrior01"); // Wagon02 영역(20~28)에만 있음 - Wagon01 영역은 16~24
            ends.SetUnitId(Slot(22, 24), "Warrior02"); // 두 마차 모두의 영역
            var endRemoval = FormationAreaRules.Remove(ends, Slot(24, 24), Lookup, null);
            Assert.IsTrue(endRemoval.Accepted);
            CollectionAssert.AreEquivalent(new[] { "Warrior01" }, endRemoval.ReleasedUnitIds);
            Assert.AreEqual("Warrior02", endRemoval.Layout.GetUnitId(Slot(22, 24)));
        }

        [Test]
        public void Facility_InsideWagonArea_ConnectsWagons_OutsideIsIgnored()
        {
            // 마차 영역 밖(15)에 놓인 시설은 대열·연결 어디에도 기여하지 않는다.
            var outside = Empty();
            outside.SetUnitId(Slot(10, 24), "Wagon01");
            outside.SetUnitId(Slot(20, 24), "Wagon02");   // Wagon01 영역(6~14)과 Wagon02 영역(16~24)은 떨어져 있음
            outside.SetUnitId(Slot(15, 24), "Facility01");
            var ignored = FormationArea.Compute(outside, Lookup);
            Assert.IsFalse(ignored.Contains(Slot(15, 24)));
            Assert.IsFalse(ignored.WagonsConnected);

            // 마차 영역 안(14)의 시설은 영역(10~18)으로 두 마차 영역(6~14, 17~25) 사이 틈(15~16)을 이어 준다(기획 59번 §3.3).
            var bridged = Empty();
            bridged.SetUnitId(Slot(10, 24), "Wagon01");
            bridged.SetUnitId(Slot(21, 24), "Wagon02");
            bridged.SetUnitId(Slot(14, 24), "Facility01");
            Assert.IsTrue(FormationArea.Compute(bridged, Lookup).WagonsConnected);
        }

        [Test]
        public void BridgeFacility_ReleaseThatDisconnects_IsRejected()
        {
            // Wagon01 영역 6~14, Wagon02 영역 17~25 - Facility01(14, 영역 10~18)이 둘을 잇는다.
            var layout = Empty();
            layout.SetUnitId(Slot(10, 24), "Wagon01");
            layout.SetUnitId(Slot(21, 24), "Wagon02");
            layout.SetUnitId(Slot(14, 24), "Facility01");

            // (a) Wagon01을 6으로 옮기면(영역 2~10) 시설이 마차 영역 밖이 되어 해제 대상 - 해제 후 연결이 끊기므로 이동 거부.
            Assert.AreEqual(FormationEditRejection.Disconnected, FormationAreaRules.Move(layout, Slot(10, 24), Slot(6, 24), Lookup, null).Rejection);
            // (b) 잇고 있는 시설 제거 거부.
            Assert.AreEqual(FormationEditRejection.Disconnected, FormationAreaRules.Remove(layout, Slot(14, 24), Lookup, null).Rejection);
            // (c) 시설을 (10,28)로 옮기면 영역(6~14 × 24~32)이 틈을 메우지 못해 끊김 - 거부.
            Assert.AreEqual(FormationEditRejection.Disconnected, FormationAreaRules.Move(layout, Slot(14, 24), Slot(10, 28), Lookup, null).Rejection);

            // (d) 가운데 마차가 잇던 대열에서, 시설이 연결을 유지해 주면 가운데 마차 제거 허용(시설은 해제되지 않음).
            var withMiddle = layout.Clone();
            withMiddle.SetUnitId(Slot(15, 20), "Wagon03");
            withMiddle.Clear(Slot(14, 24));
            Assert.AreEqual(FormationEditRejection.Disconnected, FormationAreaRules.Remove(withMiddle, Slot(15, 20), Lookup, null).Rejection);
            withMiddle.SetUnitId(Slot(14, 24), "Facility01");
            var removed = FormationAreaRules.Remove(withMiddle, Slot(15, 20), Lookup, null);
            Assert.IsTrue(removed.Accepted);
            CollectionAssert.IsEmpty(removed.ReleasedUnitIds);
        }

        [Test]
        public void Facility_MayOnlyStayInsideWagonArea()
        {
            // 마차 영역 20~28. 시설을 끝(28)에 두면 대열이 32까지 넓어지지만, 시설은 그 넓힌 칸(29~32)으로 옮길 수 없다.
            var layout = Empty();
            layout.SetUnitId(Slot(24, 24), "Wagon01");
            layout.SetUnitId(Slot(28, 24), "Facility01");
            Assert.AreEqual(FormationEditRejection.OutsideArea, FormationAreaRules.Move(layout, Slot(28, 24), Slot(30, 24), Lookup, null).Rejection);
            Assert.AreEqual(FormationEditRejection.None, FormationAreaRules.CanPlaceAt(FormationArea.Compute(layout, Lookup), units["Warrior01"], Slot(30, 24)));

            // 캐릭터와 교환해 시설이 마차 영역 밖으로 밀려나는 경우도 거부.
            layout.SetUnitId(Slot(30, 24), "Warrior01");
            Assert.AreEqual(FormationEditRejection.OutsideArea, FormationAreaRules.Move(layout, Slot(30, 24), Slot(28, 24), Lookup, null).Rejection);

            // 마차를 옮겨 시설이 마차 영역 밖이 되면 시설과 그 영역에만 있던 캐릭터를 해제(마차가 하나라 연결은 유지, 기획 59번 §3.3).
            var moved = FormationAreaRules.Move(layout, Slot(24, 24), Slot(20, 24), Lookup, null);
            Assert.IsTrue(moved.Accepted);
            CollectionAssert.AreEquivalent(new[] { "Facility01", "Warrior01" }, moved.ReleasedUnitIds);
            // 시설이 계속 마차 영역 안에 있는 이동은 허용(마차 영역 21~29 - 시설 28 포함).
            Assert.IsTrue(FormationAreaRules.Move(layout, Slot(24, 24), Slot(25, 24), Lookup, null).Accepted);

            // 마차 제거로 시설이 마차 영역 밖이 되는 경우도 자동 해제(시설 영역 안 캐릭터도 함께).
            layout.SetUnitId(Slot(20, 24), "Wagon02");
            var removed = FormationAreaRules.Remove(layout, Slot(24, 24), Lookup, null);
            Assert.IsTrue(removed.Accepted);
            CollectionAssert.AreEquivalent(new[] { "Facility01", "Warrior01" }, removed.ReleasedUnitIds);
        }

        [Test]
        public void Wagon_Anywhere_RequiresConnectionOnlyWhenOtherWagonsExist()
        {
            var anywhere = WagonPlacement.Anywhere;

            // 마차 없음: 기준 칸에서 먼 칸에 첫 마차 - 마을(Anywhere)은 허용, 상행(WithinArea)은 거부.
            Assert.IsTrue(FormationAreaRules.Place(Empty(), units["Wagon01"], Slot(5, 5), Lookup, null, anywhere).Accepted);
            Assert.AreEqual(FormationEditRejection.OutsideArea, FormationAreaRules.Place(Empty(), units["Wagon01"], Slot(5, 5), Lookup, null).Rejection);

            // 마차 1대(영역 20~28): 대열 밖(33)이지만 영역(29~37)이 맞닿음 → 허용 / 떨어진 칸(35, 영역 31~39) → 끊김.
            var one = Empty();
            one.SetUnitId(Slot(24, 24), "Wagon01");
            Assert.IsTrue(FormationAreaRules.Place(one, units["Wagon02"], Slot(33, 24), Lookup, null, anywhere).Accepted);
            Assert.AreEqual(FormationEditRejection.Disconnected, FormationAreaRules.Place(one, units["Wagon02"], Slot(35, 24), Lookup, null, anywhere).Rejection);

            // 격자 이동: 두 번째 마차를 대열 밖 연결 가능 칸으로 옮김 → 허용.
            var two = one.Clone();
            two.SetUnitId(Slot(28, 24), "Wagon02"); // 대열 20~32
            Assert.IsTrue(FormationAreaRules.Move(two, Slot(28, 24), Slot(33, 24), Lookup, null, anywhere).Accepted);

            // 캐릭터·시설은 Anywhere여도 기존 규칙.
            Assert.AreEqual(FormationEditRejection.OutsideArea, FormationAreaRules.Place(one, units["Warrior01"], Slot(33, 24), Lookup, null, anywhere).Rejection);
            Assert.AreEqual(FormationEditRejection.OutsideArea, FormationAreaRules.Place(one, units["Facility01"], Slot(33, 24), Lookup, null, anywhere).Rejection);
        }

        [Test]
        public void Pins_AllowCharactersWithoutWagon_AndCountForConnectivity()
        {
            var pins = new[] { new FormationAreaPin(Slot(10, 10), FormationAreaShape.Square(2)) };
            var area = FormationArea.Compute(Empty(), Lookup, pins);
            Assert.AreEqual(FormationEditRejection.None, FormationAreaRules.CanPlaceAt(area, units["Warrior01"], Slot(11, 10)));

            var layout = Empty();
            layout.SetUnitId(Slot(10, 24), "Wagon01");
            layout.SetUnitId(Slot(20, 24), "Wagon02");
            var bridge = new[] { new FormationAreaPin(Slot(15, 24), FormationAreaShape.Square(1)) };
            Assert.IsTrue(FormationArea.Compute(layout, Lookup, bridge).WagonsConnected);

            // 비대칭 핀: 마차 영역(4~12, 18~26) 사이 틈(13~17)을 가로로 뻗은 핀은 잇고, 세로로만 뻗은 핀은 잇지 못한다.
            var apart = Empty();
            apart.SetUnitId(Slot(8, 30), "Wagon01");
            apart.SetUnitId(Slot(22, 30), "Wagon02");
            var horizontal = new[] { new FormationAreaPin(Slot(15, 30), Mask("1112111")) };
            var vertical = new[] { new FormationAreaPin(Slot(15, 30), Mask("1/1/1/2/1/1/1")) };
            Assert.IsTrue(FormationArea.Compute(apart, Lookup, horizontal).WagonsConnected);
            Assert.IsFalse(FormationArea.Compute(apart, Lookup, vertical).WagonsConnected);
        }

        [Test]
        public void Move_ThatBreaksConnection_IsRejected()
        {
            // Wagon02가 Wagon01(2~10)과 Wagon03(12~20)을 잇는 다리 역할(Wagon01·Wagon03 영역은 서로 맞닿지 않음).
            var layout = Empty();
            layout.SetUnitId(Slot(6, 24), "Wagon01");
            layout.SetUnitId(Slot(11, 24), "Wagon02");
            layout.SetUnitId(Slot(16, 24), "Wagon03");
            // 자기 영역 아래 끝(11,28)으로 옮기면 새 정사각형(7~15 × 24~32)이 양쪽 영역과 겹쳐 여전히 이어진다.
            Assert.IsTrue(FormationAreaRules.Move(layout, Slot(11, 24), Slot(11, 28), Lookup, null).Accepted);
            // Wagon03 쪽 영역(16,20)으로 옮기면 Wagon01과 이어줄 칸이 사라진다.
            Assert.AreEqual(FormationEditRejection.Disconnected, FormationAreaRules.Move(layout, Slot(11, 24), Slot(16, 20), Lookup, null).Rejection);
        }

        [Test]
        public void Extent_IsCenteredOnWagonAverage()
        {
            var layout = Empty();
            layout.SetUnitId(Slot(20, 24), "Wagon01");
            layout.SetUnitId(Slot(24, 24), "Wagon02");
            var extent = FormationArea.Compute(layout, Lookup).ToExtent();

            Assert.AreEqual(22f, extent.CenterColumn, 1e-4f);
            Assert.AreEqual(24f, extent.CenterRow, 1e-4f);
            Assert.AreEqual(6f, extent.HalfColumnSpan, 1e-4f); // 영역 16~28, 중심 22
            Assert.AreEqual(4f, extent.HalfRowSpan, 1e-4f);
        }
    }
}
