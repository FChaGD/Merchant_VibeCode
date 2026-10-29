using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>숙소 휴식 하루 처리(Docs/설계/105번 §5, 109번 §4.3). 결제 성공일만 날짜 +1·회복, 결제 부족일은 상태 변경 없음.</summary>
    public class InnRestServiceTests
    {
        // 개인 골드 → 골드 상자(가치 boxValue) 순 지출 가짜.
        private sealed class FakeSpender : IGoldSpender
        {
            public int Personal;
            public int Boxes;
            public int BoxValue = 50;
            public int OwnedGold => Personal + Boxes * BoxValue;
            public bool CanAfford(int amount) => amount <= OwnedGold;
            public IReadOnlyList<string> PreviewWithdrawal(int amount) => Array.Empty<string>();
            public bool TrySpend(int amount)
            {
                if (!CanAfford(amount)) return false;
                while (Personal < amount) { Boxes--; Personal += BoxValue; }
                Personal -= amount;
                return true;
            }
            public void Refund(int amount) => Personal += amount;
            public event Action Changed { add { } remove { } }
        }

        private sealed class FakeDate : IDateAdvancer
        {
            public int Days;
            public void AdvanceDays(int days) => Days += days;
        }

        private sealed class FakeRecovery : IRestDayRecovery
        {
            public int Count;
            public void RecoverOneDay() => Count++;
        }

        private readonly List<GameObject> created = new();
        private FakeSpender spender;
        private FakeDate date;
        private FakeRecovery recovery;

        [SetUp]
        public void SetUp()
        {
            spender = new FakeSpender { Personal = 100 };
            date = new FakeDate();
            recovery = new FakeRecovery();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created) UnityEngine.Object.DestroyImmediate(go);
            created.Clear();
        }

        private InnRestService Create(bool withDependencies = true, bool withRecovery = true)
        {
            var go = new GameObject(nameof(InnRestService));
            created.Add(go);
            var rest = go.AddComponent<InnRestService>();
            if (withDependencies) rest.Initialize(spender, date, withRecovery ? recovery : null);
            else rest.Initialize(null, null, null);
            return rest;
        }

        [Test]
        public void DefaultCostIsTen()
        {
            Assert.AreEqual(10, Create().CostPerDay);
        }

        [Test]
        public void SettleDay_Paid_AdvancesDateAndRecovers()
        {
            var rest = Create();
            rest.BeginRest(3);
            for (var i = 0; i < 3; i++) Assert.AreEqual(RestDaySettlement.Paid, rest.SettleDay());
            rest.EndRest(3);

            Assert.AreEqual(3, date.Days);
            Assert.AreEqual(70, spender.Personal);
            Assert.AreEqual(3, recovery.Count);
        }

        [Test]
        public void SettleDay_UsesSpender()
        {
            spender.Personal = 5;
            spender.Boxes = 1;
            var rest = Create();
            rest.BeginRest(1);

            Assert.AreEqual(RestDaySettlement.Paid, rest.SettleDay(), "개인 골드가 모자라도 상자를 꺼내 낸다");
            Assert.AreEqual(0, spender.Boxes);
            Assert.AreEqual(45, spender.Personal);
        }

        [Test]
        public void SettleDay_Insufficient_NoDateNoRecovery()
        {
            spender.Personal = 5;
            var rest = Create();
            rest.BeginRest(2);

            Assert.AreEqual(RestDaySettlement.InsufficientGold, rest.SettleDay());
            Assert.AreEqual(0, date.Days);
            Assert.AreEqual(0, recovery.Count);
            Assert.AreEqual(5, spender.Personal);
        }

        [Test]
        public void SettleDay_NoRecovery_Ok()
        {
            var rest = Create(withRecovery: false);
            rest.BeginRest(1);
            Assert.AreEqual(RestDaySettlement.Paid, rest.SettleDay());
            Assert.AreEqual(1, date.Days);
        }

        [Test]
        public void SettleDay_NoSession_Throws()
        {
            var rest = Create();
            Assert.IsFalse(rest.CanSettleDay);
            Assert.Throws<System.InvalidOperationException>(() => rest.SettleDay());
            Assert.AreEqual(0, date.Days);
            Assert.AreEqual(100, spender.Personal);
        }

        [Test]
        public void SettleDay_BeyondPlan_Throws()
        {
            var rest = Create();
            rest.BeginRest(1);
            Assert.IsTrue(rest.CanSettleDay);
            rest.SettleDay();
            Assert.IsFalse(rest.CanSettleDay, "예정 일수를 다 채웠다");
            Assert.Throws<System.InvalidOperationException>(() => rest.SettleDay());

            Assert.AreEqual(1, date.Days);
            Assert.AreEqual(90, spender.Personal);
        }

        [Test]
        public void NoDependencies_PaidWithoutCharge()
        {
            var rest = Create(withDependencies: false);
            rest.BeginRest(2);
            Assert.AreEqual(RestDaySettlement.Paid, rest.SettleDay());
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
