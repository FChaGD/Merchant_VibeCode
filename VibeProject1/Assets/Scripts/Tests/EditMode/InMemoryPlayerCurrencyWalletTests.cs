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
        public void ResolveDependencies_StartsFullyFilled()
        {
            Assert.AreEqual(wallet.Capacity, wallet.CurrentAmount);
        }

        [Test]
        public void Add_ClampsAtCapacity_ReturnsActuallyAppliedAmount()
        {
            var applied = wallet.Add(500); // 이미 가득 찬 상태

            Assert.AreEqual(0, applied);
            Assert.AreEqual(wallet.Capacity, wallet.CurrentAmount);
        }

        [Test]
        public void TrySpend_ThenAdd_ClampsOnlyExcess()
        {
            wallet.TrySpend(300);

            var applied = wallet.Add(1000);

            Assert.AreEqual(300, applied);
            Assert.AreEqual(wallet.Capacity, wallet.CurrentAmount);
        }

        [Test]
        public void TrySpend_ReturnsFalseWhenInsufficient()
        {
            var result = wallet.TrySpend(wallet.Capacity + 1);

            Assert.IsFalse(result);
            Assert.AreEqual(wallet.Capacity, wallet.CurrentAmount);
        }

        [Test]
        public void TrySpend_ReturnsTrueAndDeducts()
        {
            var result = wallet.TrySpend(200);

            Assert.IsTrue(result);
            Assert.AreEqual(wallet.Capacity - 200, wallet.CurrentAmount);
        }

        [Test]
        public void OnAmountChanged_FiresWithNewAmount_OnSpend()
        {
            int? notified = null;
            wallet.OnAmountChanged += amount => notified = amount;

            wallet.TrySpend(150);

            Assert.AreEqual(wallet.CurrentAmount, notified);
        }

        [Test]
        public void OnAmountChanged_DoesNotFire_WhenSpendFails()
        {
            var fired = false;
            wallet.OnAmountChanged += _ => fired = true;

            wallet.TrySpend(wallet.Capacity + 1);

            Assert.IsFalse(fired);
        }

        [Test]
        public void OnAmountChanged_DoesNotFire_WhenAddHasNoRoom()
        {
            var fired = false;
            wallet.OnAmountChanged += _ => fired = true;

            wallet.Add(10); // 이미 가득 찬 상태라 반영될 여지가 없다.

            Assert.IsFalse(fired);
        }
    }
}
