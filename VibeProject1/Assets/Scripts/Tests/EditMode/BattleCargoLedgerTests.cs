using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Game.Core;

namespace Game.Core.Tests
{
    public class BattleCargoLedgerTests
    {
        private sealed class StubDef : IInventoryItemDefinition
        {
            public string Id => "Stub";
            public string DisplayName => Id;
            public string Description => string.Empty;
            public Sprite Icon => null;
            public int FootprintWidth => 1;
            public int FootprintHeight => 1;
        }

        // 원장은 도난 주체의 사망/도주 이벤트만 쓴다 - 나머지 멤버는 기본값.
        private sealed class FakeEnemy : IBattleCombatant
        {
            public Vector2 Position => Vector2.zero;
            public bool IsAlive => true;
            public float Defense => 0f;
            public float MaxHp => 1f;
            public float CurrentHp => 1f;
            public float Attack => 0f;
            public float Range => 0f;
            public Sprite Icon => null;
            public bool IsAlly => false;
            public bool IsFleeing => false;
            public Vector2 FleeVelocity => Vector2.zero;
            public IDamageable CurrentTarget => null;
            public string RoleGroup => null;
            public string Positioning => null;
            public IReadOnlyCollection<IDamageable> RecognizedEnemies => Array.Empty<IDamageable>();
            public Vector2? DebugMoveTarget => null;
            public event Action OnDied;
            public event Action<float> OnDamaged { add { } remove { } }
            public event Action OnFled;
            public event Action<IDamageable> OnAttacked { add { } remove { } }
            public void TakeDamage(float amount, IBattleCombatant attacker) { }
            public void ReceiveMoraleWave(float delta) { }
            public void NotifyKilledEnemy() { }
            public void Tick(float deltaTime, IReadOnlyList<IDamageable> targets, IReadOnlyList<IBattleCombatant> sameSideUnits) { }
            public void RaiseDied() => OnDied?.Invoke();
            public void RaiseFled() => OnFled?.Invoke();
        }

        private const float Hp = 200f;
        private static Queue<float> rolls;

        // 준비한 굴림을 다 쓰면 0.99(아무 일도 없음) - 굴림을 몇 번 소비했는지는 rolls.Count로 확인한다.
        private static float Next() => rolls.Count > 0 ? rolls.Dequeue() : 0.99f;

        private static InventoryItemInstance Item(string id, string wagon) => new(id, new StubDef(), new GridPosition(0, 0), 0, false, wagon);

        private static BattleProtectedUnit Wagon(string id, float hp = Hp) => new(Vector2.zero, hp, null, 0.5f, ProtectedUnitKind.Wagon, id);

        private static BattleCargoLedger Ledger(float[] preparedRolls, params InventoryItemInstance[] items)
        {
            rolls = new Queue<float>(preparedRolls);
            return new BattleCargoLedger(items, Next);
        }

        private static int Count(BattleCargoReport report, CargoItemFate fate) => report.Items.Count(item => item.Fate == fate);

        private static CargoItemFate FateOf(BattleCargoReport report, string instanceId) => report.Items.First(item => item.InstanceId == instanceId).Fate;

        [Test]
        public void Hit_EmptyWagon_NoRoll()
        {
            var ledger = Ledger(new[] { 0.0f }, Item("i1", "WagonB"));
            var wagon = Wagon("WagonA");
            ledger.RegisterWagon(wagon);

            wagon.TakeDamage(10f, new FakeEnemy());

            Assert.AreEqual(1, rolls.Count, "물품 없는 마차는 굴림을 소비하지 않는다");
            Assert.AreEqual(1, Count(ledger.BuildReport(), CargoItemFate.Intact));
        }

        [Test]
        public void Hit_RollBelowBreak_BreaksOneItem()
        {
            var ledger = Ledger(new[] { 0.05f, 0.0f }, Item("i1", "WagonA"), Item("i2", "WagonA"));
            var wagon = Wagon("WagonA");
            ledger.RegisterWagon(wagon);

            wagon.TakeDamage(10f, new FakeEnemy());

            var report = ledger.BuildReport();
            Assert.AreEqual(CargoItemFate.Broken, FateOf(report, "i1"));
            Assert.AreEqual(CargoItemFate.Intact, FateOf(report, "i2"));
        }

        [Test]
        public void Hit_RollInStealBand_StolenByAttacker()
        {
            var ledger = Ledger(new[] { 0.12f, 0.0f }, Item("i1", "WagonA"), Item("i2", "WagonA"));
            var wagon = Wagon("WagonA");
            ledger.RegisterWagon(wagon);

            wagon.TakeDamage(10f, new FakeEnemy());

            var report = ledger.BuildReport();
            Assert.AreEqual(1, Count(report, CargoItemFate.Stolen));
            Assert.AreEqual(1, Count(report, CargoItemFate.Intact));
        }

        [Test]
        public void Hit_RollAbove_Nothing()
        {
            var ledger = Ledger(new[] { 0.5f }, Item("i1", "WagonA"), Item("i2", "WagonA"));
            var wagon = Wagon("WagonA");
            ledger.RegisterWagon(wagon);

            wagon.TakeDamage(10f, new FakeEnemy());

            Assert.AreEqual(2, Count(ledger.BuildReport(), CargoItemFate.Intact));
        }

        [Test]
        public void Destroy_SplitsBrokenAndUnprotected_NoTheft()
        {
            // 한 번에 파괴 - 피격 굴림 0.5(없음) 뒤 물품별 파괴 굴림 0.1(파손), 0.8(무방비).
            var ledger = Ledger(new[] { 0.5f, 0.1f, 0.8f }, Item("i1", "WagonA"), Item("i2", "WagonA"));
            var wagon = Wagon("WagonA");
            ledger.RegisterWagon(wagon);

            wagon.TakeDamage(Hp, new FakeEnemy());

            var report = ledger.BuildReport();
            Assert.AreEqual(CargoItemFate.Broken, FateOf(report, "i1"));
            Assert.AreEqual(CargoItemFate.Unprotected, FateOf(report, "i2"));
            Assert.AreEqual(0, Count(report, CargoItemFate.Stolen));
        }

        [Test]
        public void KillingBlow_RollsHitBeforeDestruction()
        {
            // 피격 판정이 먼저면 i1 파손(0.05, 선택 0.0) 뒤 i2만 파괴 굴림(0.8 → 무방비).
            // 파괴 판정이 먼저였다면 0.05·0.0이 둘 다 파손으로 소비돼 i2도 파손이 된다.
            var ledger = Ledger(new[] { 0.05f, 0.0f, 0.8f }, Item("i1", "WagonA"), Item("i2", "WagonA"));
            var wagon = Wagon("WagonA", hp: 1f);
            ledger.RegisterWagon(wagon);

            wagon.TakeDamage(5f, new FakeEnemy());

            var report = ledger.BuildReport();
            Assert.IsFalse(wagon.IsAlive);
            Assert.AreEqual(CargoItemFate.Broken, FateOf(report, "i1"));
            Assert.AreEqual(CargoItemFate.Unprotected, FateOf(report, "i2"));
        }

        [Test]
        public void StolenOwner_Dies_BecomesRecoverable()
        {
            var ledger = Ledger(new[] { 0.12f, 0.0f }, Item("i1", "WagonA"));
            var wagon = Wagon("WagonA");
            var thief = new FakeEnemy();
            ledger.RegisterWagon(wagon);

            wagon.TakeDamage(10f, thief);
            thief.RaiseDied();

            Assert.AreEqual(CargoItemFate.Recoverable, FateOf(ledger.BuildReport(), "i1"));
        }

        [Test]
        public void StolenOwner_Flees_BecomesStolenLost()
        {
            var ledger = Ledger(new[] { 0.12f, 0.0f }, Item("i1", "WagonA"));
            var wagon = Wagon("WagonA");
            var thief = new FakeEnemy();
            ledger.RegisterWagon(wagon);

            wagon.TakeDamage(10f, thief);
            thief.RaiseFled();

            Assert.AreEqual(CargoItemFate.StolenLost, FateOf(ledger.BuildReport(), "i1"));
        }

        [Test]
        public void Wreck_TargetableOnlyWithUnprotected()
        {
            // WagonA: 피격 없음 → 무방비(0.8) → 잔해 생김. WagonB: 피격 없음 → 파손(0.1) → 무방비 0개라 잔해 없음.
            var ledger = Ledger(new[] { 0.5f, 0.8f, 0.5f, 0.1f }, Item("i1", "WagonA"), Item("i2", "WagonB"));
            var wagonA = Wagon("WagonA");
            var wagonB = Wagon("WagonB");
            ledger.RegisterWagon(wagonA);
            ledger.RegisterWagon(wagonB);

            wagonA.TakeDamage(Hp, new FakeEnemy());
            wagonB.TakeDamage(Hp, new FakeEnemy());

            Assert.AreEqual(1, ledger.WreckTargets.Count);
            Assert.IsTrue(ledger.WreckTargets[0].IsAlive);
            Assert.AreEqual(wagonA.Position, ledger.WreckTargets[0].Position);
        }

        [Test]
        public void Wreck_Hit_StealsAt25Percent()
        {
            var ledger = Ledger(new[] { 0.5f, 0.8f, 0.8f }, Item("i1", "WagonA"), Item("i2", "WagonA"));
            var wagon = Wagon("WagonA");
            ledger.RegisterWagon(wagon);
            wagon.TakeDamage(Hp, new FakeEnemy());
            var wreck = ledger.WreckTargets[0];

            rolls = new Queue<float>(new[] { 0.3f });
            wreck.TakeDamage(10f, new FakeEnemy());
            Assert.AreEqual(0, Count(ledger.BuildReport(), CargoItemFate.Stolen), "0.3은 도난 확률(0.25) 밖");

            rolls = new Queue<float>(new[] { 0.2f, 0.0f });
            wreck.TakeDamage(10f, new FakeEnemy());
            var report = ledger.BuildReport();
            Assert.AreEqual(1, Count(report, CargoItemFate.Stolen));
            Assert.AreEqual(1, Count(report, CargoItemFate.Unprotected));
            Assert.IsTrue(wreck.IsAlive);
        }

        [Test]
        public void Wreck_EmptyAfterLastTheft_NotTargetable()
        {
            var ledger = Ledger(new[] { 0.5f, 0.8f }, Item("i1", "WagonA"));
            var wagon = Wagon("WagonA");
            ledger.RegisterWagon(wagon);
            wagon.TakeDamage(Hp, new FakeEnemy());
            var wreck = ledger.WreckTargets[0];
            var diedCount = 0;
            wreck.OnDied += () => diedCount++;

            rolls = new Queue<float>(new[] { 0.2f, 0.0f });
            wreck.TakeDamage(10f, new FakeEnemy());
            rolls = new Queue<float>(new[] { 0.0f, 0.0f });
            wreck.TakeDamage(10f, new FakeEnemy());

            Assert.IsFalse(wreck.IsAlive);
            Assert.AreEqual(1, diedCount);
            Assert.AreEqual(2, rolls.Count, "빈 잔해는 굴림을 소비하지 않는다");
            Assert.AreEqual(CargoItemFate.Stolen, FateOf(ledger.BuildReport(), "i1"));
        }

        [Test]
        public void Report_ListsDestroyedWagonIds()
        {
            var ledger = Ledger(Array.Empty<float>());
            var wagonA = Wagon("WagonA");
            var wagonB = Wagon("WagonB");
            ledger.RegisterWagon(wagonA);
            ledger.RegisterWagon(wagonB);

            wagonA.TakeDamage(Hp, new FakeEnemy());

            CollectionAssert.AreEqual(new[] { "WagonA" }, ledger.BuildReport().DestroyedWagonIds);
        }

        [Test]
        public void StagedItems_AreNotTracked()
        {
            var staged = new InventoryItemInstance("s1", new StubDef(), new GridPosition(0, 0), 0, true);
            var ledger = Ledger(Array.Empty<float>(), staged, Item("i1", "WagonA"));

            var report = ledger.BuildReport();

            Assert.AreEqual(1, report.Items.Count);
            Assert.AreEqual("i1", report.Items[0].InstanceId);
        }
    }
}
