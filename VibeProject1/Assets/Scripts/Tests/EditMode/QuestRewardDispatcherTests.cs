using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    /// <summary>퀘스트 보상·불이익(Docs/설계/107번 §7).</summary>
    public class QuestRewardDispatcherTests
    {
        internal sealed class FakeIncome : IGoldIncome
        {
            public int Total;
            public int Earn(int amount) { if (amount <= 0) return 0; Total += amount; return amount; }
        }

        internal sealed class FakeReputation : IReputationAdjuster
        {
            public int Value { get; private set; }
            public int Min => -100;
            public int Max => 100;
            public event System.Action Changed { add { } remove { } }
            public int Adjust(int delta) { Value += delta; return delta; }
        }

        private static readonly GoldTestItem Herb = new("herb", 1, 1);
        private GoldTestInventory inventory;
        private FakeIncome income;
        private FakeReputation reputation;
        private GoldTestWallet wallet;
        private readonly List<string> warnings = new();

        [SetUp]
        public void SetUp()
        {
            inventory = new GoldTestInventory(GoldTestInventory.Section("A", 1, 1));
            inventory.AddDefinition(Herb);
            income = new FakeIncome();
            reputation = new FakeReputation();
            wallet = new GoldTestWallet(50);
            warnings.Clear();
        }

        private QuestRewardDispatcher Create(IQuestUnitRewardGranter units = null)
            => new(income, reputation, new QuestCargoService(inventory, new CargoTestSettlement(inventory)), units, wallet, warnings.Add);

        private static QuestRewardDefinition R(QuestRewardType type, int amount, string item = null) => new() { Type = type, Amount = amount, ItemId = item };

        [Test]
        public void Grant_GoldReputationItem()
        {
            var staged = Create().Grant(new[] { R(QuestRewardType.Gold, 300), R(QuestRewardType.Reputation, 5), R(QuestRewardType.Item, 1, "herb") });
            Assert.AreEqual(0, staged);
            Assert.AreEqual(300, income.Total);
            Assert.AreEqual(5, reputation.Value);
            Assert.AreEqual(1, inventory.Items.Count);
        }

        [Test]
        public void Grant_ItemOverflow_Staged()
        {
            var dispatcher = Create();
            Assert.IsFalse(dispatcher.CanReceiveItems(new[] { R(QuestRewardType.Item, 2, "herb") }));
            Assert.AreEqual(1, dispatcher.Grant(new[] { R(QuestRewardType.Item, 2, "herb") }));
            Assert.AreEqual(1, inventory.StagedItems.Count);
        }

        [Test]
        public void Grant_UnitWithoutGranter_WarnsAndSkips()
        {
            Create().Grant(new[] { R(QuestRewardType.Unit, 1, "Hero") });
            Assert.AreEqual(1, warnings.Count);
        }

        [Test]
        public void Penalty_GoldUpToOwned_AndReputation()
        {
            Create().ApplyFailurePenalty(new QuestDefinition { FailGold = 80, FailReputation = -5 });
            Assert.AreEqual(0, wallet.CurrentAmount);
            Assert.AreEqual(-5, reputation.Value);
        }

        [Test]
        public void Describe_Format()
        {
            var text = QuestRewardDispatcher.Describe(new[] { R(QuestRewardType.Gold, 1300), R(QuestRewardType.Reputation, -2), R(QuestRewardType.Item, 2, "herb"), R(QuestRewardType.Item, 1, "herb") }, id => id == "herb" ? "약초" : "?");
            Assert.AreEqual("골드 1,300, 평판 -2, 약초 ×2, 약초", text);
            Assert.AreEqual("없음", QuestRewardDispatcher.Describe(new QuestRewardDefinition[0], id => id));
        }
    }
}
