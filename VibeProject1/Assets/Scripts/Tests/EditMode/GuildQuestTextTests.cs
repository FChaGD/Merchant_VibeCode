using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    /// <summary>게시판 상세 문구(Docs/설계/107번 §11.2).</summary>
    public class GuildQuestTextTests
    {
        private static string City(int id) => $"도시{id}";
        private static string Item(string id) => id == "herb" ? "약초" : id;

        private static QuestDefinition Def()
        {
            var deadline = new QuestObjectiveDefinition { Id = "t", Role = QuestObjectiveRole.Core, Type = QuestObjectiveType.Deadline, HasDeadlineDays = true, DeadlineDays = 5 };
            return new QuestDefinition
            {
                Id = "Q",
                FailReputation = -5,
                FailGold = 100,
                Objectives = new List<QuestObjectiveDefinition>
                {
                    new() { Id = "d", Role = QuestObjectiveRole.Core, Type = QuestObjectiveType.Deliver, CityId = 2, ItemId = "herb", Count = 3, Label = PlaceholderText.Missing },
                    new() { Id = "w", Role = QuestObjectiveRole.Bonus, Type = QuestObjectiveType.BattleWin, Count = 2, Label = "도적 소탕" },
                    deadline,
                },
                Rewards = new List<QuestRewardDefinition>
                {
                    new() { Type = QuestRewardType.Gold, Amount = 300 },
                    new() { ObjectiveId = "w", Type = QuestRewardType.Reputation, Amount = 2 },
                },
            };
        }

        [Test]
        public void Objectives_BeforeAndAfterAccept()
        {
            var def = Def();
            var before = GuildQuestText.Objectives(def, null, _ => 0, City, Item, 0);
            Assert.AreEqual("[핵심] 약초 3개 납품(도시2)\n[보조] 도적 소탕\n[핵심] 기한 준수", before);

            var instance = QuestRules.Create(def, 10, 1, 0);
            instance.Progress("w").Value = 1;
            var after = GuildQuestText.Objectives(def, instance, _ => 5, City, Item, 10);
            Assert.AreEqual("[핵심] 약초 3개 납품(도시2) (3/3)\n[보조] 도적 소탕 (1/2)\n[핵심] 기한 준수 (1월 16일까지)", after);
        }

        [Test]
        public void Deadline_Text()
        {
            var def = Def();
            Assert.AreEqual("수락 후 5일 이내", GuildQuestText.Deadline(def, null, 0));
            var instance = QuestRules.Create(def, 10, 1, 0);
            Assert.AreEqual("1월 16일까지 (3일 남음)", GuildQuestText.Deadline(def, instance, 12));
            instance.Progress("t").Impossible = true;
            Assert.AreEqual("기한 초과", GuildQuestText.Deadline(def, instance, 20));
        }

        [Test]
        public void Reward_IncludesBonusLines()
        {
            Assert.AreEqual("골드 300\n(보조 · 도적 소탕) 평판 +2", GuildQuestText.Reward(Def(), Item, City));
        }

        [Test]
        public void Penalty_TextOrNull()
        {
            Assert.AreEqual("평판 -5, 위약금 100골드", GuildQuestText.Penalty(Def()));
            Assert.IsNull(GuildQuestText.Penalty(new QuestDefinition()));
        }
    }
}
