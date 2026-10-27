using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>숙소 휴식 날짜·비용(Docs/설계/105번 §5). 하루 경과마다 개인 골드 −1일 비용, 날짜 +1.</summary>
    public class InnRestServiceTests
    {
        private sealed class FakeWallet : IPlayerCurrencyWallet
        {
            public int CurrentAmount { get; set; }
            public int PersonalLimit => 1000;
            public event Action<int> OnAmountChanged { add { } remove { } }
            public int Add(int amount) { if (amount <= 0) return 0; CurrentAmount += amount; return amount; }
            public bool TrySpend(int amount) { if (amount > CurrentAmount) return false; CurrentAmount -= amount; return true; }
        }

        private sealed class FakeDate : IDateAdvancer
        {
            public int Days;
            public void AdvanceDays(int days) => Days += days;
        }

        private readonly List<GameObject> created = new();
        private FakeWallet wallet;
        private FakeDate date;

        [SetUp]
        public void SetUp()
        {
            wallet = new FakeWallet { CurrentAmount = 100 };
            date = new FakeDate();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created) UnityEngine.Object.DestroyImmediate(go);
            created.Clear();
        }

        private InnRestService Create(bool withDependencies = true)
        {
            var go = new GameObject(nameof(InnRestService));
            created.Add(go);
            var rest = go.AddComponent<InnRestService>();
            if (withDependencies) rest.Initialize(wallet, date);
            else rest.Initialize(null, null);
            return rest;
        }

        [Test]
        public void DefaultCostIsTen()
        {
            Assert.AreEqual(10, Create().CostPerDay);
        }

        [Test]
        public void FullRest_AdvancesDateAndCharges()
        {
            var rest = Create();
            rest.BeginRest(3);
            for (var i = 0; i < 3; i++) rest.AdvanceDay();
            rest.EndRest(3);

            Assert.AreEqual(3, date.Days);
            Assert.AreEqual(70, wallet.CurrentAmount);
        }

        [Test]
        public void StoppedRest_OnlyElapsedDays()
        {
            var rest = Create();
            rest.BeginRest(5);
            rest.AdvanceDay();
            rest.EndRest(1);
            rest.AdvanceDay();

            Assert.AreEqual(1, date.Days);
            Assert.AreEqual(90, wallet.CurrentAmount);
        }

        [Test]
        public void AdvanceOutsideRestOrBeyondPlan_Ignored()
        {
            var rest = Create();
            rest.AdvanceDay();
            rest.BeginRest(1);
            rest.AdvanceDay();
            rest.AdvanceDay();

            Assert.AreEqual(1, date.Days);
            Assert.AreEqual(90, wallet.CurrentAmount);
        }

        [Test]
        public void ZeroDays_DoesNotStart()
        {
            var rest = Create();
            rest.BeginRest(0);
            rest.AdvanceDay();

            Assert.AreEqual(0, date.Days);
        }

        [Test]
        public void InsufficientGold_DateStillAdvances()
        {
            wallet.CurrentAmount = 5;
            var rest = Create();
            rest.BeginRest(1);
            rest.AdvanceDay();

            Assert.AreEqual(1, date.Days);
            Assert.AreEqual(5, wallet.CurrentAmount);
        }

        [Test]
        public void NoDependencies_NoException()
        {
            var rest = Create(withDependencies: false);
            rest.BeginRest(2);
            Assert.DoesNotThrow(() => rest.AdvanceDay());
            rest.EndRest(1);
        }

        [Test]
        public void Session_LimitsAndRestart()
        {
            var session = new InnRestSession();
            Assert.IsFalse(session.Begin(0));
            Assert.IsFalse(session.TryAdvance());
            Assert.IsTrue(session.Begin(2));
            Assert.IsTrue(session.TryAdvance());
            Assert.IsTrue(session.TryAdvance());
            Assert.IsFalse(session.TryAdvance());
            session.End();
            Assert.IsFalse(session.CanAdvance);
            Assert.IsTrue(session.Begin(1));
            Assert.AreEqual(0, session.ElapsedDays);
        }
    }
}
