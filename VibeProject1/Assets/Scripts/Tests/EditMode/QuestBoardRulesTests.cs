using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    /// <summary>게시 판정(Docs/설계/107번 §5.2).</summary>
    public class QuestBoardRulesTests
    {
        private static QuestDefinition Def() => new()
        {
            Id = "Q",
            Kind = QuestKind.General,
            Acceptance = QuestAcceptance.Request,
            BoardCityIds = new List<int> { 1, 2 },
        };

        private static bool Posted(QuestDefinition def, QuestPostingState posting = null, int city = 1, int today = 0, int reputation = 0, bool active = false)
            => QuestBoardRules.IsPosted(def, posting ?? new QuestPostingState(), city, today, reputation, _ => active);

        [Test]
        public void PostedOnlyInBoardCities()
        {
            Assert.IsTrue(Posted(Def(), city: 2));
            Assert.IsFalse(Posted(Def(), city: 3));
        }

        [Test]
        public void RequiresPrerequisiteOrUnlock()
        {
            var def = Def();
            def.RequiredQuestId = "P";
            var posting = new QuestPostingState();
            Assert.IsFalse(Posted(def, posting));
            posting.CompletedIds.Add("P");
            Assert.IsTrue(Posted(def, posting));

            var unlocked = new QuestPostingState();
            unlocked.UnlockedIds.Add("Q");
            Assert.IsTrue(Posted(def, unlocked));
        }

        [Test]
        public void MinReputation()
        {
            var def = Def();
            def.HasMinReputation = true;
            def.MinReputation = 10;
            Assert.IsFalse(Posted(def, reputation: 9));
            Assert.IsTrue(Posted(def, reputation: 10));
        }

        [Test]
        public void RepostDayAndNever()
        {
            var posting = new QuestPostingState();
            posting.SetRepost("Q", 5, never: false);
            Assert.IsFalse(Posted(Def(), posting, today: 4));
            Assert.IsTrue(Posted(Def(), posting, today: 5));
            Assert.IsTrue(Posted(Def(), posting, today: 9), "날짜가 건너뛰어도 게시");
            posting.SetRepost("Q", 0, never: true);
            Assert.IsFalse(Posted(Def(), posting, today: 100));
        }

        [Test]
        public void ActiveNotPosted() => Assert.IsFalse(Posted(Def(), active: true));

        [Test]
        public void OnceCompletedNotPosted_RepeatableUsesRepost()
        {
            var posting = new QuestPostingState();
            posting.CompletedIds.Add("Q");
            Assert.IsFalse(Posted(Def(), posting));
            var repeatable = Def();
            repeatable.Repeat = QuestRepeat.Repeatable;
            Assert.IsTrue(Posted(repeatable, posting));
        }

        [Test]
        public void MainAndTriggerNotPosted()
        {
            var main = Def();
            main.Kind = QuestKind.Main;
            Assert.IsFalse(Posted(main));
            var trigger = Def();
            trigger.Acceptance = QuestAcceptance.Trigger;
            Assert.IsFalse(Posted(trigger));
        }
    }
}
