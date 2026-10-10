using NUnit.Framework;
using UnityEngine;
using Game.Core;

namespace Game.Core.Tests
{
    public class InMemoryPlayerCurrencyWalletTests
    {
        private GameObject gameObject;
        private InMemoryPlayerCurrencyWallet wallet;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject(nameof(InMemoryPlayerCurrencyWalletTests));
            wallet = gameObject.AddComponent<InMemoryPlayerCurrencyWallet>();
            wallet.ResolveDependencies(null); // 다른 의존성을 조회하지 않으므로 null로 충분하다.
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void ResolveDependencies_StartsWithStartingAmount_SeparateFromLimit()
        {
            Assert.AreEqual(10000, wallet.CurrentAmount);
            Assert.AreEqual(500, wallet.PersonalLimit);
        }

        [Test]
        public void Add_HasNoCap_ReturnsFullAmount()
        {
            var applied = wallet.Add(500);

            Assert.AreEqual(500, applied);
            Assert.AreEqual(10500, wallet.CurrentAmount);
        }

        [Test]
        public void Add_NonPositive_IgnoredWithoutEvent()
        {
            var fired = false;
            wallet.OnAmountChanged += _ => fired = true;

            Assert.AreEqual(0, wallet.Add(0));
            Assert.AreEqual(0, wallet.Add(-5));
            Assert.AreEqual(10000, wallet.CurrentAmount);
            Assert.IsFalse(fired);
        }

        [Test]
        public void TrySpend_ReturnsFalseWhenInsufficient()
        {
            Assert.IsFalse(wallet.TrySpend(10001));
            Assert.AreEqual(10000, wallet.CurrentAmount);
        }

        [Test]
        public void TrySpend_ReturnsTrueAndDeducts()
        {
            Assert.IsTrue(wallet.TrySpend(200));
            Assert.AreEqual(9800, wallet.CurrentAmount);
        }

        [Test]
        public void OnAmountChanged_FiresWithNewAmount_OnSpendAndAdd()
        {
            int? notified = null;
            wallet.OnAmountChanged += amount => notified = amount;

            wallet.TrySpend(150);
            Assert.AreEqual(9850, notified);

            wallet.Add(1000);
            Assert.AreEqual(10850, notified);
        }

        [Test]
        public void OnAmountChanged_DoesNotFire_WhenSpendFails()
        {
            var fired = false;
            wallet.OnAmountChanged += _ => fired = true;

            wallet.TrySpend(10001);

            Assert.IsFalse(fired);
        }
    }
}
