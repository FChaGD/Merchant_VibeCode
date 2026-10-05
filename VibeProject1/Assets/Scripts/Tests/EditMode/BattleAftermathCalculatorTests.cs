using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Game.Core;

namespace Game.Core.Tests
{
    public class BattleAftermathCalculatorTests
    {
        private sealed class StubDef : IInventoryItemDefinition
        {
            public StubDef(string id) => Id = id;
            public string Id { get; }
            public string DisplayName => Id;
            public string Description => string.Empty;
            public Sprite Icon => null;
            public int FootprintWidth => 1;
            public int FootprintHeight => 1;
        }

        private static int rollCount;

        // 굴림 횟수를 세는 난수 - 준비한 값을 다 쓰면 0.99(손실 없음).
        private static Func<float> Rolls(params float[] values)
        {
            rollCount = 0;
            var queue = new Queue<float>(values);
            return () =>
            {
                rollCount++;
                return queue.Count > 0 ? queue.Dequeue() : 0.99f;
            };
        }

        private static CargoItemRecord Record(string id, string wagon, CargoItemFate fate) => new(id, wagon, new StubDef(id), fate);

        private static InventoryItemInstance Placed(string id, string wagon) => new(id, new StubDef(id), new GridPosition(0, 0), 0, false, wagon);

        private static InventoryItemInstance Staged(string id) => new(id, new StubDef(id), new GridPosition(0, 0), 0, true);

        private static BattleResult Victory(BattleCargoReport report) => new(BattleOutcome.Victory, BattleDefeatCause.None, report);

        private static BattleResult Defeat(BattleDefeatCause cause, BattleCargoReport report) => new(BattleOutcome.Defeat, cause, report);

        private static BattleCargoReport Report(string[] destroyedWagons, params CargoItemRecord[] records) => new(records, destroyedWagons);

        [Test]
        public void CapturedOrDeath_EmptyPlan()
        {
            var report = Report(new[] { "W1" }, Record("i1", "W1", CargoItemFate.Broken), Record("i2", "W1", CargoItemFate.Unprotected));
            var items = new[] { Placed("i3", "W2") };

            foreach (var consequence in new[] { DefeatConsequence.Captured, DefeatConsequence.Death })
            {
                var plan = BattleAftermathCalculator.Plan(Defeat(BattleDefeatCause.NoCombatants, report), consequence, items, Rolls(0f));

                Assert.That(plan.RemoveInstanceIds, Is.Empty);
                Assert.That(plan.Recover, Is.Empty);
                Assert.That(plan.DestroyedWagonIds, Is.Empty);
                Assert.That(plan.ExtraSeconds, Is.EqualTo(0f));
                Assert.That(plan.Broken + plan.StolenLost + plan.Recoverable + plan.DefeatLost, Is.EqualTo(0));
                Assert.That(rollCount, Is.EqualTo(0));
            }
        }

        [Test]
        public void Victory_RecoversUnprotectedAndDeadThiefItems_StolenLostRemoved()
        {
            var report = Report(new[] { "W1" },
                Record("unprotected", "W1", CargoItemFate.Unprotected),
                Record("recoverable", "W2", CargoItemFate.Recoverable),
                Record("stolenLost", "W2", CargoItemFate.StolenLost),
                Record("broken", "W1", CargoItemFate.Broken),
                Record("intact", "W2", CargoItemFate.Intact));

            var plan = BattleAftermathCalculator.Plan(Victory(report), null, Array.Empty<InventoryItemInstance>(), Rolls());

            Assert.That(plan.RemoveInstanceIds, Is.EquivalentTo(new[] { "unprotected", "recoverable", "stolenLost", "broken" }));
            Assert.That(plan.Recover.Count, Is.EqualTo(2));
            Assert.That(new[] { plan.Recover[0].Id, plan.Recover[1].Id }, Is.EquivalentTo(new[] { "unprotected", "recoverable" }));
            Assert.That(plan.Recoverable, Is.EqualTo(2));
            Assert.That(plan.StolenLost, Is.EqualTo(1));
            Assert.That(plan.Broken, Is.EqualTo(1));
            Assert.That(plan.DefeatLost, Is.EqualTo(0));
        }

        [Test]
        public void Defeat_NoRecovery_AllNonIntactRemoved()
        {
            var report = Report(new[] { "W1" },
                Record("stolen", "W2", CargoItemFate.Stolen),
                Record("unprotected", "W1", CargoItemFate.Unprotected),
                Record("recoverable", "W2", CargoItemFate.Recoverable),
                Record("stolenLost", "W2", CargoItemFate.StolenLost),
                Record("broken", "W1", CargoItemFate.Broken),
                Record("intact", "W2", CargoItemFate.Intact));

            var plan = BattleAftermathCalculator.Plan(Defeat(BattleDefeatCause.AllWagonsDestroyed, report), DefeatConsequence.Flee, Array.Empty<InventoryItemInstance>(), Rolls());

            Assert.That(plan.RemoveInstanceIds, Is.EquivalentTo(new[] { "stolen", "unprotected", "recoverable", "stolenLost", "broken" }));
            Assert.That(plan.Recover, Is.Empty);
            Assert.That(plan.Recoverable, Is.EqualTo(0));
            Assert.That(plan.StolenLost, Is.EqualTo(4));
            Assert.That(plan.Broken, Is.EqualTo(1));
            Assert.That(plan.DefeatLost, Is.EqualTo(0));
        }

        [Test]
        public void NoCombatantsFlee_IntactOnSurvivingWagons_Lose30()
        {
            // W1 파괴(물품은 원장이 처리), W2 생존 - W2의 두 물품만 굴린다. 임시보관 물품은 마차에 없어 제외.
            var report = Report(new[] { "W1" }, Record("onWreck", "W1", CargoItemFate.Unprotected), Record("lost", "W2", CargoItemFate.Intact), Record("kept", "W2", CargoItemFate.Intact));
            var items = new[] { Placed("onWreck", "W1"), Placed("lost", "W2"), Placed("kept", "W2"), Staged("staged") };

            var plan = BattleAftermathCalculator.Plan(Defeat(BattleDefeatCause.NoCombatants, report), DefeatConsequence.Flee, items, Rolls(0.1f, 0.5f));

            Assert.That(rollCount, Is.EqualTo(2));
            Assert.That(plan.RemoveInstanceIds, Is.EquivalentTo(new[] { "onWreck", "lost" }));
            Assert.That(plan.DefeatLost, Is.EqualTo(1));
            Assert.That(plan.StolenLost, Is.EqualTo(1));
            Assert.That(plan.Recover, Is.Empty);
        }

        [Test]
        public void AllWagonsDestroyed_NoUnitWipeLossRoll()
        {
            // 원인이 마차 전멸이면 유닛 전멸 손실을 굴리지 않는다(도주·궤주 모두).
            var report = Report(new[] { "W1" }, Record("broken", "W1", CargoItemFate.Broken));
            var items = new[] { Placed("broken", "W1"), Placed("survivor", "W2") };

            foreach (var consequence in new[] { DefeatConsequence.Flee, DefeatConsequence.Rout })
            {
                var plan = BattleAftermathCalculator.Plan(Defeat(BattleDefeatCause.AllWagonsDestroyed, report), consequence, items, Rolls(0f, 0f));

                Assert.That(rollCount, Is.EqualTo(0));
                Assert.That(plan.DefeatLost, Is.EqualTo(0));
                Assert.That(plan.RemoveInstanceIds, Is.EquivalentTo(new[] { "broken" }));
            }

            // 유닛 전멸이어도 남은 물품이 전부 파괴 마차에 있으면 굴릴 대상이 없다.
            var allOnWreck = BattleAftermathCalculator.Plan(Defeat(BattleDefeatCause.NoCombatants, report), DefeatConsequence.Rout, new[] { Placed("broken", "W1"), Placed("other", "W1") }, Rolls(0f));
            Assert.That(rollCount, Is.EqualTo(0));
            Assert.That(allOnWreck.DefeatLost, Is.EqualTo(0));
        }

        [Test]
        public void ExtraSeconds_VictoryAndFleeOnly()
        {
            var report = Report(new[] { "W1", "W2" });

            var victory = BattleAftermathCalculator.Plan(Victory(report), null, null, Rolls());
            var flee = BattleAftermathCalculator.Plan(Defeat(BattleDefeatCause.AllWagonsDestroyed, report), DefeatConsequence.Flee, null, Rolls());
            var rout = BattleAftermathCalculator.Plan(Defeat(BattleDefeatCause.AllWagonsDestroyed, report), DefeatConsequence.Rout, null, Rolls());

            Assert.That(victory.ExtraSeconds, Is.EqualTo(40f));
            Assert.That(flee.ExtraSeconds, Is.EqualTo(40f));
            Assert.That(rout.ExtraSeconds, Is.EqualTo(0f));
            Assert.That(rout.DestroyedWagonIds, Is.EquivalentTo(new[] { "W1", "W2" }));
        }

        [Test]
        public void Formatter_OmitsZeroLines()
        {
            Assert.That(BattleAftermathSummaryFormatter.Format(default), Is.EqualTo(string.Empty));
            Assert.That(default(BattleAftermathSummary).IsEmpty, Is.True);

            var cargoAndTime = new BattleAftermathSummary(2, 0, 1, 0, 1, 0, 0, 20f, false);
            Assert.That(BattleAftermathSummaryFormatter.Format(cargoAndTime),
                Is.EqualTo("화물: 파손 2 · 회수 대상 1\n마차: 파괴 1대(보유 목록에서 제거)\n상행 시간 +20초"));

            var formationOnly = new BattleAftermathSummary(0, 0, 0, 0, 0, 3, 1, 0f, true);
            Assert.That(BattleAftermathSummaryFormatter.Format(formationOnly), Is.EqualTo("대열: 재배치 3 · 팔레트 복귀 1"));

            var all = new BattleAftermathSummary(1, 2, 3, 4, 2, 5, 6, 40f, true);
            Assert.That(BattleAftermathSummaryFormatter.Format(all), Is.EqualTo(
                "화물: 파손 1 · 도난 손실 2 · 회수 대상 3 · 패배 손실 4\n마차: 파괴 2대(보유 목록에서 제거)\n대열: 재배치 5 · 팔레트 복귀 6\n상행 시간 +40초"));
        }
    }
}
