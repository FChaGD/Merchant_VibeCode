using NUnit.Framework;
using UnityEngine;
using Game.Core;

namespace Game.Core.Tests
{
    public class PlaceholderTradeGoodsInventoryRepositoryTests
    {
        private GameObject gameObject;
        private DependencyManager dependencyManager;
        private PlaceholderCaravanRosterProvider rosterProvider;
        private InMemoryPlayerCurrencyWallet wallet;
        private PlaceholderTradeGoodsInventoryRepository repository;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject(nameof(PlaceholderTradeGoodsInventoryRepositoryTests));
            dependencyManager = gameObject.AddComponent<DependencyManager>();

            rosterProvider = gameObject.AddComponent<PlaceholderCaravanRosterProvider>();
            rosterProvider.ResolveDependencies(null); // 마차 재고 5개(11번 문서 고정치)를 채운다 - 아이콘 미배선은 무해.
            dependencyManager.Register<ICaravanRosterProvider>(rosterProvider);

            wallet = gameObject.AddComponent<InMemoryPlayerCurrencyWallet>();
            wallet.ResolveDependencies(null);
            dependencyManager.Register<IPlayerCurrencyWallet>(wallet);

            repository = gameObject.AddComponent<PlaceholderTradeGoodsInventoryRepository>();
            repository.ResolveDependencies(dependencyManager);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void ResolveDependencies_ComputesGridSizeFromWagonCount()
        {
            // 마차 재고 5개(11번 문서 고정치) × 마차당 8칸 = 40칸, 가로 10칸 기준으로 접으면 세로 4칸
            // (Docs/설계/32번 §4.2).
            Assert.AreEqual(10, repository.GridWidth);
            Assert.AreEqual(4, repository.GridHeight);
        }

        [Test]
        public void TryPlaceItem_GoldBox_DeductsWallet()
        {
            var startingAmount = wallet.CurrentAmount;

            var placed = repository.TryPlaceItem(repository.GoldBoxDefinition, new GridPosition(0, 0), out _);

            Assert.IsTrue(placed);
            Assert.AreEqual(startingAmount - 500, wallet.CurrentAmount);
        }

        [Test]
        public void TryPlaceItem_GoldBox_InsufficientFunds_Fails_AndDoesNotPlace()
        {
            wallet.TrySpend(wallet.CurrentAmount); // 잔액을 0으로 만든다.

            var placed = repository.TryPlaceItem(repository.GoldBoxDefinition, new GridPosition(0, 0), out _);

            Assert.IsFalse(placed);
            Assert.AreEqual(0, repository.Items.Count);
        }

        [Test]
        public void TryPlaceItem_GoldBox_PlacementFailure_RollsBackWallet()
        {
            // 그리드 범위 밖 좌표를 지정해 배치 자체를 실패시킨다 - 이미 차감된 재화가 롤백돼야 한다.
            var startingAmount = wallet.CurrentAmount;

            var placed = repository.TryPlaceItem(repository.GoldBoxDefinition, new GridPosition(repository.GridWidth, 0), out _);

            Assert.IsFalse(placed);
            Assert.AreEqual(startingAmount, wallet.CurrentAmount);
        }

        [Test]
        public void RemoveItem_GoldBox_RefundsWallet()
        {
            repository.TryPlaceItem(repository.GoldBoxDefinition, new GridPosition(0, 0), out var placedItem);
            var amountAfterPlacing = wallet.CurrentAmount;

            var removed = repository.RemoveItem(placedItem.InstanceId);

            Assert.IsTrue(removed);
            Assert.AreEqual(amountAfterPlacing + 500, wallet.CurrentAmount);
        }
    }
}
