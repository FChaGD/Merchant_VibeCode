using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>
    /// 파괴 마차 제거 후 재배치(설계 79번 §5.3). 기본 배치: Wagon01(20,24) 영역 열 16~24, Wagon02(28,24) 영역 열 24~32 - Wagon02가 파괴되면
    /// Wagon02 영역에만 있던 칸(25~32)이 대열 밖이 된다. 활동은 테스트 전용 목록(ActivityList)으로 직접 만든다 - Unity 밖에서도 실행되도록 GameObject를 쓰지 않는다.
    /// </summary>
    public class FormationWreckRelocatorTests
    {
        private const int Size = 50;

        private readonly Dictionary<string, IFormationUnit> units = new();
        private ActivityList activities;

        // 재배치기는 활동 목록만 읽으므로 저장소(MonoBehaviour) 대신 같은 이름의 시작 메서드만 흉내 낸다.
        private sealed class ActivityList
        {
            private readonly List<FormationActivity> items = new();
            public IReadOnlyList<FormationActivity> ActiveActivities => items;
            public void BeginAdd(string unitId, int target, float seconds) => items.Add(new FormationActivity(unitId, FormationActivityKind.Adding, target, FormationActivity.NoSlot, System.Array.Empty<int>(), seconds));
            public void BeginMove(string unitId, int origin, int target, IReadOnlyList<int> path, float seconds) => items.Add(new FormationActivity(unitId, FormationActivityKind.Moving, target, origin, path, seconds));
        }

        [SetUp]
        public void SetUp()
        {
            units.Clear();
            for (var i = 1; i <= 3; i++) Add(new PlaceholderFormationUnit($"Wagon0{i}", "마차", null, FormationUnitKind.Wagon, FormationAreaShape.Square(4)));
            Add(new PlaceholderFormationUnit("WagonSingle", "마차", null, FormationUnitKind.Wagon, FormationAreaShape.Single));
            Add(new PlaceholderFormationUnit("Facility01", "시설", null, FormationUnitKind.Facility, FormationAreaShape.Square(4)));
            Add(new PlaceholderFormationUnit("FacilitySmall", "시설", null, FormationUnitKind.Facility, FormationAreaShape.Single));
            Add(new PlaceholderMercenaryUnit("Warrior01", "전사", null, "Warrior"));
            Add(new PlaceholderMercenaryUnit("Warrior02", "전사", null, "Warrior"));

            activities = new ActivityList();
        }

        private void Add(IFormationUnit unit) => units[unit.Id] = unit;

        private IFormationUnit Lookup(string id) => id != null && units.TryGetValue(id, out var unit) ? unit : null;

        private static int Slot(int column, int row) => row * Size + column;

        private static FormationLayout TwoWagons()
        {
            var layout = FormationLayout.CreateDefault();
            layout.SetUnitId(Slot(20, 24), "Wagon01");
            layout.SetUnitId(Slot(28, 24), "Wagon02");
            return layout;
        }

        private WreckRelocationResult Relocate(FormationLayout layout, params string[] removed)
            => FormationWreckRelocator.Relocate(layout, removed, Lookup, activities.ActiveActivities);

        [Test]
        public void RemovedWagonCleared()
        {
            var layout = TwoWagons();
            var result = Relocate(layout, "Wagon02");

            Assert.IsNull(result.Layout.GetUnitId(Slot(28, 24)));
            Assert.AreEqual("Wagon01", result.Layout.GetUnitId(Slot(20, 24)));
            Assert.AreEqual("Wagon02", layout.GetUnitId(Slot(28, 24))); // 원본은 그대로
            Assert.AreEqual(1, result.ComponentCount);
            CollectionAssert.IsEmpty(result.Relocated);
            CollectionAssert.IsEmpty(result.Released);
        }

        [Test]
        public void OutsideCharacter_MovedToNearestFreeHostCell()
        {
            var layout = TwoWagons();
            layout.SetUnitId(Slot(30, 24), "Warrior01");
            var result = Relocate(layout, "Wagon02");

            Assert.AreEqual(1, result.Relocated.Count);
            Assert.AreEqual(("Warrior01", Slot(24, 24)), result.Relocated[0]);
            Assert.AreEqual("Warrior01", result.Layout.GetUnitId(Slot(24, 24)));
            Assert.IsNull(result.Layout.GetUnitId(Slot(30, 24)));
        }

        [Test]
        public void FacilitiesRelocatedBeforeCharacters()
        {
            // 둘 다 (24,24)가 가장 가깝지만 캐릭터(25)가 칸 번호도 작고 더 가깝다 - 시설이 먼저 처리돼 그 칸을 차지한다.
            var layout = TwoWagons();
            layout.SetUnitId(Slot(25, 24), "Warrior01");
            layout.SetUnitId(Slot(26, 24), "FacilitySmall");
            var result = Relocate(layout, "Wagon02");

            Assert.AreEqual(2, result.Relocated.Count);
            Assert.AreEqual(("FacilitySmall", Slot(24, 24)), result.Relocated[0]);
            Assert.AreEqual(("Warrior01", Slot(24, 23)), result.Relocated[1]);
        }

        [Test]
        public void TieBreak_LowerSlotIndex()
        {
            // (24,24)는 점유 - (24,23)과 (24,25)가 같은 거리, 칸 번호가 작은 (24,23).
            var layout = TwoWagons();
            layout.SetUnitId(Slot(24, 24), "Warrior02");
            layout.SetUnitId(Slot(25, 24), "Warrior01");
            var result = Relocate(layout, "Wagon02");

            CollectionAssert.AreEqual(new[] { ("Warrior01", Slot(24, 23)) }, result.Relocated);
            Assert.AreEqual("Warrior02", result.Layout.GetUnitId(Slot(24, 24)));
        }

        [Test]
        public void NoFreeCell_ReleasedToPalette()
        {
            // 남은 대열은 WagonSingle 자기 칸 하나뿐.
            var layout = FormationLayout.CreateDefault();
            layout.SetUnitId(Slot(10, 10), "WagonSingle");
            layout.SetUnitId(Slot(30, 30), "Wagon02");
            layout.SetUnitId(Slot(30, 31), "Warrior01");
            var result = Relocate(layout, "Wagon02");

            CollectionAssert.AreEqual(new[] { "Warrior01" }, result.Released);
            CollectionAssert.IsEmpty(result.Relocated);
            Assert.IsNull(result.Layout.GetUnitId(Slot(30, 31)));
        }

        [Test]
        public void NoWagonsLeft_AllReleased()
        {
            var layout = FormationLayout.CreateDefault();
            layout.SetUnitId(Slot(24, 24), "Wagon01");
            layout.SetUnitId(Slot(22, 24), "Facility01");
            layout.SetUnitId(Slot(26, 24), "Warrior01");
            layout.SetUnitId(Slot(24, 25), "Warrior02");
            var result = Relocate(layout, "Wagon01");

            CollectionAssert.AreEquivalent(new[] { "Facility01", "Warrior01", "Warrior02" }, result.Released);
            CollectionAssert.IsEmpty(result.Relocated);
            for (var slot = 0; slot < result.Layout.SlotCount; slot++) Assert.IsNull(result.Layout.GetUnitId(slot));
            Assert.AreEqual(1, result.ComponentCount);
        }

        [Test]
        public void AddingTargetOutside_RelocatedAndCancelled()
        {
            activities.BeginAdd("Warrior01", Slot(30, 24), 5f);
            var result = Relocate(TwoWagons(), "Wagon02");

            CollectionAssert.AreEqual(new[] { "Warrior01" }, result.CancelledActivityUnitIds);
            CollectionAssert.AreEqual(new[] { ("Warrior01", Slot(24, 24)) }, result.Relocated);
            Assert.AreEqual("Warrior01", result.Layout.GetUnitId(Slot(24, 24)));
            Assert.IsNull(result.Layout.GetUnitId(Slot(30, 24)));
        }

        [Test]
        public void MovingTargetOutside_CancelledStaysAtOrigin()
        {
            var layout = TwoWagons();
            layout.SetUnitId(Slot(22, 24), "Warrior01");
            activities.BeginMove("Warrior01", Slot(22, 24), Slot(30, 24), Enumerable.Range(22, 9).Select(c => Slot(c, 24)).ToList(), 5f);
            var result = Relocate(layout, "Wagon02");

            CollectionAssert.AreEqual(new[] { "Warrior01" }, result.CancelledActivityUnitIds);
            CollectionAssert.IsEmpty(result.Relocated);
            Assert.AreEqual("Warrior01", result.Layout.GetUnitId(Slot(22, 24)));
        }

        [Test]
        public void OtherActivityTargetCell_NotUsed()
        {
            // Warrior02의 배치 목표 (24,24)는 대열 안이라 유지 - Warrior01은 그 칸을 피한다.
            var layout = TwoWagons();
            layout.SetUnitId(Slot(30, 24), "Warrior01");
            activities.BeginAdd("Warrior02", Slot(24, 24), 5f);
            var result = Relocate(layout, "Wagon02");

            CollectionAssert.AreEqual(new[] { ("Warrior01", Slot(24, 23)) }, result.Relocated);
            Assert.IsNull(result.Layout.GetUnitId(Slot(24, 24)));
            CollectionAssert.IsEmpty(result.CancelledActivityUnitIds);
        }

        [Test]
        public void DisconnectedResult_NotRejected_ComponentCountReported()
        {
            // Wagon01(6~14)·Wagon02(14~22)·Wagon03(22~30) - 가운데가 파괴되면 양쪽이 끊어진다. 캐릭터는 (14,24)·(22,24) 동률 → 칸 번호 작은 쪽.
            var layout = FormationLayout.CreateDefault();
            layout.SetUnitId(Slot(10, 24), "Wagon01");
            layout.SetUnitId(Slot(18, 24), "Wagon02");
            layout.SetUnitId(Slot(26, 24), "Wagon03");
            layout.SetUnitId(Slot(18, 25), "Warrior01");
            var result = Relocate(layout, "Wagon02");

            Assert.AreEqual(2, result.ComponentCount);
            Assert.AreEqual("Wagon01", result.Layout.GetUnitId(Slot(10, 24)));
            Assert.AreEqual("Wagon03", result.Layout.GetUnitId(Slot(26, 24)));
            CollectionAssert.AreEqual(new[] { ("Warrior01", Slot(14, 25)) }, result.Relocated);
        }
    }
}
