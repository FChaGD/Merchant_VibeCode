using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    public class TripDepartureWarningsTests
    {
        private const string GoldText700 = "개인 소유 가능량 500을 넘는 700골드가 버려집니다. 마차에 적재하거나 은행에 보관하지 않은 골드는 버려집니다. 그래도 출발하시겠습니까?";

        [Test]
        public void Collect_NothingToWarn_Empty()
        {
            Assert.AreEqual(0, TripDepartureWarnings.Collect(true, 0, 0, 500).Count);
        }

        [Test]
        public void Collect_AtLimit_NoGoldWarning()
        {
            // ExcessGold는 개인 골드가 정확히 500이면 0이다.
            Assert.AreEqual(0, TripDepartureWarnings.Collect(true, 0, 0, 500).Count);
        }

        [Test]
        public void Collect_GoldOnly_ExactText()
        {
            CollectionAssert.AreEqual(new[] { GoldText700 }, TripDepartureWarnings.Collect(true, 0, 700, 500));
        }

        [Test]
        public void Collect_Both_EmptyFormationFirst()
        {
            CollectionAssert.AreEqual(new[] { TripDepartureWarnings.EmptyFormation, GoldText700 }, TripDepartureWarnings.Collect(false, 0, 700, 500));
        }
        [Test]
        public void Collect_Staged_ExactText()
        {
            CollectionAssert.AreEqual(new[] { "임시 보관소의 물류품 3개가 버려집니다. 그래도 출발하시겠습니까?" }, TripDepartureWarnings.Collect(true, 3, 0, 500));
        }

        [Test]
        public void Collect_All_OrderIsFormationThenStagedThenGold()
        {
            var warnings = TripDepartureWarnings.Collect(false, 2, 700, 500);
            CollectionAssert.AreEqual(new[] { TripDepartureWarnings.EmptyFormation, TripDepartureWarnings.FormatStaged(2), GoldText700 }, warnings);
        }


        [Test]
        public void DepartureWarning_ListsQuestCargo()
        {
            var warnings = TripDepartureWarnings.Collect(true, 2, 0, 500, new[] { "약초 운송", "급송" });
            CollectionAssert.AreEqual(new[] { "임시 보관소의 물류품 2개가 버려집니다. 그래도 출발하시겠습니까?\n의뢰 화물입니다(약초 운송, 급송). 퀘스트 수행이 불가능해질 수 있습니다." }, warnings);
            CollectionAssert.AreEqual(new[] { TripDepartureWarnings.FormatStaged(2) }, TripDepartureWarnings.Collect(true, 2, 0, 500, new string[0]), "해당 없으면 줄 없음");
        }

        [Test]
        public void DiscardWarning_QuestCargoLine()
        {
            var item = new InventoryItemInstance("i1", new GoldTestItem("box", 1, 1), new GridPosition(0, 0));
            var withQuest = InventoryDiscardWarning.Compose(new[] { item }, null, new FakeQuestWarnings("상자 운송"));
            StringAssert.EndsWith("\n의뢰 화물입니다(상자 운송). 퀘스트 수행이 불가능해질 수 있습니다.", withQuest);
            var none = InventoryDiscardWarning.Compose(new[] { item }, null, new FakeQuestWarnings());
            Assert.AreEqual(InventoryDiscardWarning.Header + "\nbox", none);
        }

        private sealed class FakeQuestWarnings : IQuestCargoWarnings
        {
            private readonly string[] names;
            public FakeQuestWarnings(params string[] names) => this.names = names;
            public System.Collections.Generic.IReadOnlyList<string> Describe(System.Collections.Generic.IEnumerable<string> itemIds) => names;
        }

        [Test]
        public void FormatGold_UsesGivenLimit_WithThousandsSeparator()
        {
            StringAssert.StartsWith("개인 소유 가능량 1,000을 넘는 1,200골드가", TripDepartureWarnings.FormatGold(1000, 1200));
        }
    }
}
