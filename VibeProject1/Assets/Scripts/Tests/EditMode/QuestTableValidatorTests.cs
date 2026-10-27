using System;
using System.Collections.Generic;
using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>퀘스트 정의 임포트 검증(Docs/설계/107번 §4).</summary>
    public class QuestTableValidatorTests
    {
        private static QuestReferenceIds Refs() => new()
        {
            CityIds = new HashSet<int> { 1, 2, 3 },
            TradeGoodsIds = new HashSet<string> { "Wheat", "Herb", "QuestBox" },
            QuestCargoIds = new HashSet<string> { "QuestBox" },
            EnemyTypes = new HashSet<string> { "Marauder" },
            EventIds = new HashSet<string> { "Ev1" },
        };

        private static QuestDefinition Valid(string id = "Q1") => new()
        {
            Id = id,
            Kind = QuestKind.General,
            Acceptance = QuestAcceptance.Request,
            Completion = QuestCompletion.Instant,
            BoardCityIds = new List<int> { 1 },
            Objectives = new List<QuestObjectiveDefinition>
            {
                new() { Id = "o1", Role = QuestObjectiveRole.Core, Type = QuestObjectiveType.Deliver, CityId = 2, ItemId = "QuestBox", Count = 1 },
                new() { Id = "b1", Role = QuestObjectiveRole.Bonus, Type = QuestObjectiveType.Preserve },
            },
            Cargo = new List<QuestCargoDefinition> { new() { ItemId = "QuestBox", Count = 1 } },
            Rewards = new List<QuestRewardDefinition>
            {
                new() { Type = QuestRewardType.Gold, Amount = 100 },
                new() { ObjectiveId = "b1", Type = QuestRewardType.Item, ItemId = "Herb", Amount = 2 },
            },
        };

        private static void Check(params QuestDefinition[] quests) => QuestTableValidator.Validate(quests, Refs());

        [Test]
        public void ValidQuest_Passes() => Assert.DoesNotThrow(() => Check(Valid()));

        [Test]
        public void EventProgressOne_Throws()
        {
            var q = Valid();
            q.Effects.Add(new QuestEffectDefinition { Scope = QuestEffectScope.AllTrips, EventId = "Ev1", ProgressMin = 0.99f, ProgressMax = 0.99f });
            Assert.DoesNotThrow(() => Check(q));
            q.Effects[0].ProgressMax = 1f;
            Assert.Throws<FormatException>(() => Check(q), "1은 구간 끝(도착)과 겹친다 - 1 미만만 허용");
        }

        [Test]
        public void NoCoreObjective_Throws()
        {
            var q = Valid();
            q.Objectives[0].Role = QuestObjectiveRole.Bonus;
            Assert.Throws<FormatException>(() => Check(q));
        }

        [Test]
        public void UnknownCity_Throws()
        {
            var q = Valid();
            q.Objectives[0].CityId = 99;
            Assert.Throws<FormatException>(() => Check(q));
        }

        [Test]
        public void ReportDeliverCityMismatch_Throws()
        {
            var q = Valid();
            q.Completion = QuestCompletion.Report;
            q.ReportCityId = 3;
            Assert.Throws<FormatException>(() => Check(q));
            q.ReportCityId = 2;
            Assert.DoesNotThrow(() => Check(q));
        }

        [Test]
        public void ReportWithoutReportCity_Throws()
        {
            var q = Valid();
            q.Completion = QuestCompletion.Report;
            Assert.Throws<FormatException>(() => Check(q));
        }

        [Test]
        public void PreserveWithoutCargo_Throws()
        {
            var q = Valid();
            q.Cargo.Clear();
            q.Objectives[0].ItemId = "Wheat";
            Assert.Throws<FormatException>(() => Check(q));
        }

        [Test]
        public void CargoItemNotQuestCargo_Throws()
        {
            var q = Valid();
            q.Cargo[0].ItemId = "Wheat";
            Assert.Throws<FormatException>(() => Check(q));
        }

        [Test]
        public void RequestWithoutBoardCities_Throws()
        {
            var q = Valid();
            q.BoardCityIds.Clear();
            Assert.Throws<FormatException>(() => Check(q));
        }

        [Test]
        public void DeadlineBothOrNeither_Throws()
        {
            var q = Valid();
            q.Objectives.Add(new QuestObjectiveDefinition { Id = "d", Role = QuestObjectiveRole.Core, Type = QuestObjectiveType.Deadline });
            Assert.Throws<FormatException>(() => Check(q));
            q.Objectives[2].HasDeadlineDays = true;
            q.Objectives[2].HasDeadlineDate = true;
            Assert.Throws<FormatException>(() => Check(q));
            q.Objectives[2].HasDeadlineDate = false;
            Assert.DoesNotThrow(() => Check(q));
        }

        [Test]
        public void DestinationEffectWithoutCityObjective_Throws()
        {
            var q = Valid();
            q.Objectives[0] = new QuestObjectiveDefinition { Id = "o1", Role = QuestObjectiveRole.Core, Type = QuestObjectiveType.BattleWin, Count = 1 };
            q.Effects.Add(new QuestEffectDefinition { Scope = QuestEffectScope.Destination, DifficultyBonus = 5 });
            Assert.Throws<FormatException>(() => Check(q));
        }

        [Test]
        public void UnknownFollowUpOrBonusId_Throws()
        {
            var q = Valid();
            q.FollowUps.Add(new QuestFollowUpDefinition { NextQuestId = "Nope" });
            Assert.Throws<FormatException>(() => Check(q));

            var q2 = Valid("Q2");
            var q1 = Valid();
            q1.FollowUps.Add(new QuestFollowUpDefinition { NextQuestId = "Q2", RequiredBonusId = "o1" });
            Assert.Throws<FormatException>(() => Check(q1, q2), "핵심 목표는 후속 조건이 될 수 없다");
        }

        [Test]
        public void UnknownEventId_Throws()
        {
            var q = Valid();
            q.Effects.Add(new QuestEffectDefinition { Scope = QuestEffectScope.AllTrips, EventId = "Nope" });
            Assert.Throws<FormatException>(() => Check(q));
        }

        [Test]
        public void RepeatableWithoutRepostDays_Throws()
        {
            var q = Valid();
            q.Repeat = QuestRepeat.Repeatable;
            Assert.Throws<FormatException>(() => Check(q));
        }

        [Test]
        public void DateFormat_Parses()
        {
            Assert.IsTrue(QuestDateFormat.TryParse("1-2-3", out var days));
            Assert.AreEqual(180 + 30 + 2, days);
            Assert.IsTrue(QuestDateFormat.TryParse("0-1-1", out days));
            Assert.AreEqual(0, days);
            Assert.IsFalse(QuestDateFormat.TryParse("0-7-1", out _));
            Assert.IsFalse(QuestDateFormat.TryParse("0-1-31", out _));
            Assert.IsFalse(QuestDateFormat.TryParse("abc", out _));
        }
    }
}
