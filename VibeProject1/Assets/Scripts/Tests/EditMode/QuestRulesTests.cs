using System.Collections.Generic;
using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>목표 판정 규칙(Docs/설계/107번 §6.1).</summary>
    public class QuestRulesTests
    {
        private readonly Dictionary<string, int> items = new();
        private int Count(string id) => id != null && items.TryGetValue(id, out var n) ? n : 0;

        [SetUp]
        public void SetUp() => items.Clear();

        private static QuestObjectiveDefinition Obj(string id, QuestObjectiveType type, QuestObjectiveRole role = QuestObjectiveRole.Core, int city = 0, string item = null, int count = 0)
            => new() { Id = id, Role = role, Type = type, CityId = city, ItemId = item, Count = count };

        private static QuestDefinition Def(QuestCompletion completion, params QuestObjectiveDefinition[] objectives)
            => new() { Id = "Q", Completion = completion, Objectives = new List<QuestObjectiveDefinition>(objectives) };

        [Test]
        public void Arrive_MetOnTargetCity()
        {
            var def = Def(QuestCompletion.Instant, Obj("a", QuestObjectiveType.Arrive, city: 2));
            var q = QuestRules.Create(def, 0, 1, 0);
            Assert.IsFalse(QuestRules.OnArrive(def, q, 3, Count));
            Assert.IsTrue(QuestRules.OnArrive(def, q, 2, Count));
            Assert.AreEqual(QuestVerdict.CoreMet, QuestRules.Judge(def, q));
        }

        [Test]
        public void Deliver_InstantMetWhenCountEnough_NotOnOtherCity()
        {
            var def = Def(QuestCompletion.Instant, Obj("d", QuestObjectiveType.Deliver, city: 2, item: "Wheat", count: 3));
            var q = QuestRules.Create(def, 0, 1, 0);
            items["Wheat"] = 2;
            Assert.IsFalse(QuestRules.OnArrive(def, q, 2, Count));
            items["Wheat"] = 3;
            Assert.IsFalse(QuestRules.OnArrive(def, q, 5, Count));
            Assert.IsTrue(QuestRules.OnArrive(def, q, 2, Count));
        }

        [Test]
        public void Deliver_ReportTracksCountBothWays()
        {
            var def = Def(QuestCompletion.Report, Obj("d", QuestObjectiveType.Deliver, city: 2, item: "Wheat", count: 3));
            var q = QuestRules.Create(def, 0, 1, 0);
            items["Wheat"] = 3;
            Assert.IsTrue(QuestRules.OnCargoCountChanged(def, q, Count));
            Assert.AreEqual(QuestVerdict.CoreMet, QuestRules.Judge(def, q));
            items["Wheat"] = 1;
            Assert.IsTrue(QuestRules.OnCargoCountChanged(def, q, Count));
            Assert.AreEqual(QuestVerdict.InProgress, QuestRules.Judge(def, q));
        }

        [Test]
        public void Deliver_GivenCargoBelowCount_Impossible()
        {
            var def = Def(QuestCompletion.Instant, Obj("d", QuestObjectiveType.Deliver, city: 2, item: "Box", count: 2));
            def.Cargo.Add(new QuestCargoDefinition { ItemId = "Box", Count = 2 });
            var q = QuestRules.Create(def, 0, 1, 2);
            items["Box"] = 2;
            Assert.IsFalse(QuestRules.OnCargoCountChanged(def, q, Count));
            items["Box"] = 1;
            Assert.IsTrue(QuestRules.OnCargoCountChanged(def, q, Count));
            Assert.AreEqual(QuestVerdict.Failed, QuestRules.Judge(def, q));
        }

        [Test]
        public void Deliver_ProcuredBelowCount_NotImpossible()
        {
            var def = Def(QuestCompletion.Instant, Obj("d", QuestObjectiveType.Deliver, city: 2, item: "Wheat", count: 2));
            var q = QuestRules.Create(def, 0, 1, 0);
            Assert.IsFalse(QuestRules.OnCargoCountChanged(def, q, Count));
            Assert.AreEqual(QuestVerdict.InProgress, QuestRules.Judge(def, q));
        }

        [Test]
        public void Deadline_CoreExpired_Fails()
        {
            var deadline = Obj("t", QuestObjectiveType.Deadline);
            deadline.HasDeadlineDays = true;
            deadline.DeadlineDays = 5;
            var def = Def(QuestCompletion.Instant, Obj("a", QuestObjectiveType.Arrive, city: 2), deadline);
            var q = QuestRules.Create(def, 10, 1, 0);
            Assert.AreEqual(15, q.Progress("t").DeadlineDay);
            Assert.IsFalse(QuestRules.OnDateChanged(def, q, 15));
            Assert.IsTrue(QuestRules.OnDateChanged(def, q, 16));
            Assert.AreEqual(QuestVerdict.Failed, QuestRules.Judge(def, q));
        }

        [Test]
        public void Deadline_BonusExpired_NotMetButNotFail()
        {
            var deadline = Obj("t", QuestObjectiveType.Deadline, QuestObjectiveRole.Bonus);
            deadline.HasDeadlineDays = true;
            deadline.DeadlineDays = 1;
            var def = Def(QuestCompletion.Instant, Obj("a", QuestObjectiveType.Arrive, city: 2), deadline);
            var q = QuestRules.Create(def, 0, 1, 0);
            Assert.Contains("t", QuestRules.BonusIdsAtSuccess(def, q));
            QuestRules.OnDateChanged(def, q, 5);
            QuestRules.OnArrive(def, q, 2, Count);
            Assert.AreEqual(QuestVerdict.CoreMet, QuestRules.Judge(def, q));
            Assert.IsEmpty(QuestRules.BonusIdsAtSuccess(def, q));
            Assert.IsEmpty(QuestRules.NewlyMetBonusIds(def, q), "기한 보조는 즉시 지급 대상이 아니다");
        }

        [Test]
        public void Deadline_ByDate()
        {
            var deadline = Obj("t", QuestObjectiveType.Deadline);
            deadline.HasDeadlineDate = true;
            deadline.DeadlineTotalDays = 40;
            var def = Def(QuestCompletion.Instant, Obj("a", QuestObjectiveType.Arrive, city: 2), deadline);
            var q = QuestRules.Create(def, 10, 1, 0);
            Assert.AreEqual(40, q.Progress("t").DeadlineDay);
        }

        [Test]
        public void BattleWin_FiltersEnemyTypeAndQuestEvent()
        {
            var typed = Obj("w", QuestObjectiveType.BattleWin, count: 2);
            typed.EnemyType = "Marauder";
            var questOnly = Obj("e", QuestObjectiveType.BattleWin, QuestObjectiveRole.Bonus, count: 1);
            questOnly.QuestBattleOnly = true;
            var def = Def(QuestCompletion.Instant, typed, questOnly);
            var q = QuestRules.Create(def, 0, 1, 0);

            Assert.IsFalse(QuestRules.OnBattleWon(def, q, "Monster", false));
            Assert.IsTrue(QuestRules.OnBattleWon(def, q, "Marauder", false));
            Assert.AreEqual(1, q.Progress("w").Value);
            Assert.IsFalse(q.Progress("e").Met);
            QuestRules.OnBattleWon(def, q, "Marauder", true);
            Assert.IsTrue(q.Progress("w").Met);
            Assert.IsTrue(q.Progress("e").Met);
        }

        [Test]
        public void Preserve_LossMakesImpossible()
        {
            var def = Def(QuestCompletion.Instant, Obj("a", QuestObjectiveType.Arrive, city: 2), Obj("p", QuestObjectiveType.Preserve, QuestObjectiveRole.Bonus));
            def.Cargo.Add(new QuestCargoDefinition { ItemId = "Box", Count = 2 });
            var q = QuestRules.Create(def, 0, 1, 2);
            items["Box"] = 1;
            Assert.IsTrue(QuestRules.OnCargoCountChanged(def, q, Count));
            Assert.IsTrue(q.Progress("p").Impossible);
            Assert.AreEqual(QuestVerdict.InProgress, QuestRules.Judge(def, q), "보조 불가는 실패가 아니다");
        }

        [Test]
        public void NewlyMetBonus_ExcludesPaid()
        {
            var def = Def(QuestCompletion.Instant, Obj("a", QuestObjectiveType.Arrive, city: 2), Obj("w", QuestObjectiveType.BattleWin, QuestObjectiveRole.Bonus, count: 1));
            var q = QuestRules.Create(def, 0, 1, 0);
            QuestRules.OnBattleWon(def, q, "Marauder", false);
            CollectionAssert.AreEqual(new[] { "w" }, QuestRules.NewlyMetBonusIds(def, q));
            q.PaidBonusIds.Add("w");
            Assert.IsEmpty(QuestRules.NewlyMetBonusIds(def, q));
            Assert.IsEmpty(QuestRules.BonusIdsAtSuccess(def, q));
        }
    }
}
