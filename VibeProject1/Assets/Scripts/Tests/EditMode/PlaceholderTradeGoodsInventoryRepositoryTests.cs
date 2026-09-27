using System.Collections.Generic;
using System.Reflection;
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
        private ItemDefinitionTableAsset itemTable;
        private ItemDefinitionTableAsset miscItemTable;

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

            // 골드 상자는 기타 카테고리 테이블의 행이다(33번 §3.4, 설계 50번 §5.2). 테스트용 테이블을 만들어
            // [SerializeField]에 리플렉션으로 주입한다 - 인스펙터/임포터를 거치지 않는 EditMode 테스트 전용 배선.
            itemTable = ScriptableObject.CreateInstance<ItemDefinitionTableAsset>();
            SetPrivateField(itemTable, "entries", new List<ItemDefinitionEntry>());
            miscItemTable = ScriptableObject.CreateInstance<ItemDefinitionTableAsset>();
            SetPrivateField(miscItemTable, "entries", new List<ItemDefinitionEntry>
            {
                new() { Id = "gold-box", FootprintWidth = 1, FootprintHeight = 1, Icon = null },
            });

            repository = gameObject.AddComponent<PlaceholderTradeGoodsInventoryRepository>();
            SetPrivateField(repository, "itemTable", itemTable);
            SetPrivateField(repository, "miscItemTable", miscItemTable);
            repository.ResolveDependencies(dependencyManager);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);
            Object.DestroyImmediate(itemTable);
            Object.DestroyImmediate(miscItemTable);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(target, value);
        }

        private IInventoryItemDefinition GoldBoxDefinition
        {
            get
            {
                repository.TryGetGoldBoxDefinition(out var definition);
                return definition;
            }
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

            var placed = repository.TryPlaceItem(GoldBoxDefinition, new GridPosition(0, 0), out _);

            Assert.IsTrue(placed);
            Assert.AreEqual(startingAmount - 500, wallet.CurrentAmount);
        }

        [Test]
        public void TryPlaceItem_GoldBox_InsufficientFunds_Fails_AndDoesNotPlace()
        {
            wallet.TrySpend(wallet.CurrentAmount); // 잔액을 0으로 만든다.

            var placed = repository.TryPlaceItem(GoldBoxDefinition, new GridPosition(0, 0), out _);

            Assert.IsFalse(placed);
            Assert.AreEqual(0, repository.Items.Count);
        }

        [Test]
        public void TryPlaceItem_GoldBox_PlacementFailure_RollsBackWallet()
        {
            // 그리드 범위 밖 좌표를 지정해 배치 자체를 실패시킨다 - 이미 차감된 재화가 롤백돼야 한다.
            var startingAmount = wallet.CurrentAmount;

            var placed = repository.TryPlaceItem(GoldBoxDefinition, new GridPosition(repository.GridWidth, 0), out _);

            Assert.IsFalse(placed);
            Assert.AreEqual(startingAmount, wallet.CurrentAmount);
        }

        [Test]
        public void RemoveItem_GoldBox_RefundsWallet()
        {
            repository.TryPlaceItem(GoldBoxDefinition, new GridPosition(0, 0), out var placedItem);
            var amountAfterPlacing = wallet.CurrentAmount;

            var removed = repository.RemoveItem(placedItem.InstanceId);

            Assert.IsTrue(removed);
            Assert.AreEqual(amountAfterPlacing + 500, wallet.CurrentAmount);
        }

        [Test]
        public void ResolveDependencies_WithoutPlaceholderRows_StartsEmpty()
        {
            // SetUp 교역품 테이블은 비어 있다 - placeholder 행이 없으면 초기 배치를 건너뛴다(기획 39번 §4.4).
            Assert.AreEqual(0, repository.Items.Count);
        }

        [Test]
        public void ResolveDependencies_WithPlaceholderRows_PlacesInitialItems_WithoutTouchingWallet()
        {
            var startingAmount = wallet.CurrentAmount;
            SetPrivateField(itemTable, "entries", new List<ItemDefinitionEntry>
            {
                new() { Id = "placeholder-1x1", FootprintWidth = 1, FootprintHeight = 1, Icon = null },
                new() { Id = "placeholder-2x1", FootprintWidth = 2, FootprintHeight = 1, Icon = null },
                new() { Id = "placeholder-1x2", FootprintWidth = 1, FootprintHeight = 2, Icon = null },
                new() { Id = "placeholder-2x2", FootprintWidth = 2, FootprintHeight = 2, Icon = null },
            });

            repository.ResolveDependencies(dependencyManager);

            Assert.AreEqual(5, repository.Items.Count);
            Assert.IsTrue(repository.TryGetItemAt(new GridPosition(5, 1), out var bottomRightOf2x2));
            Assert.AreEqual("placeholder-2x2", bottomRightOf2x2.Definition.Id);
            Assert.AreEqual(startingAmount, wallet.CurrentAmount);
        }

        [Test]
        public void TryApplyPlacements_GoldBoxToStagingAndBack_DoesNotTouchWallet()
        {
            repository.TryPlaceItem(GoldBoxDefinition, new GridPosition(0, 0), out var goldBox);
            var amountAfterPlacing = wallet.CurrentAmount;

            // 재배치는 인벤토리 유입/유출이 아니다 - 임시 보관 왕복에 재화 환급/차감이 일어나면 안 된다(설계 40번 §3.2).
            Assert.IsTrue(repository.TryApplyPlacements(new[] { ItemPlacement.ToStaging(goldBox.InstanceId, 0) }));
            Assert.AreEqual(amountAfterPlacing, wallet.CurrentAmount);
            Assert.AreEqual(1, repository.StagedItems.Count);

            Assert.IsTrue(repository.TryApplyPlacements(new[] { new ItemPlacement(goldBox.InstanceId, new GridPosition(3, 2), 0) }));
            Assert.AreEqual(amountAfterPlacing, wallet.CurrentAmount);
            Assert.AreEqual(0, repository.StagedItems.Count);
        }

        [Test]
        public void TryGetDefinition_FindsTradeGoodsAndMiscItems()
        {
            SetPrivateField(itemTable, "entries", new List<ItemDefinitionEntry>
            {
                new() { Id = "placeholder-1x1", FootprintWidth = 1, FootprintHeight = 1, Icon = null },
            });
            repository.ResolveDependencies(dependencyManager);

            Assert.IsTrue(repository.TryGetDefinition("placeholder-1x1", out _));
            Assert.IsTrue(repository.TryGetDefinition("gold-box", out _));
            Assert.AreEqual(2, repository.CatalogItems.Count);
        }

        [Test]
        public void TryPlaceItem_WithQuarterTurn_OccupiesRotatedCells()
        {
            SetPrivateField(itemTable, "entries", new List<ItemDefinitionEntry>
            {
                new() { Id = "long", FootprintWidth = 2, FootprintHeight = 1, Icon = null },
            });
            repository.ResolveDependencies(dependencyManager);
            repository.TryGetDefinition("long", out var definition);

            Assert.IsTrue(repository.TryPlaceItem(definition, new GridPosition(0, 0), out var placed, quarterTurns: 1));
            Assert.AreEqual(1, placed.Width);
            Assert.AreEqual(2, placed.Height);
            Assert.IsTrue(repository.TryGetItemAt(new GridPosition(0, 1), out _));
            Assert.IsFalse(repository.TryGetItemAt(new GridPosition(1, 0), out _));
        }

        [Test]
        public void TryApplyPlacements_Success_RaisesOnChanged()
        {
            repository.TryPlaceItem(GoldBoxDefinition, new GridPosition(0, 0), out var goldBox);
            var raised = 0;
            repository.OnChanged += () => raised++;

            repository.TryApplyPlacements(new[] { new ItemPlacement(goldBox.InstanceId, new GridPosition(1, 0), 0) });

            Assert.AreEqual(1, raised);
        }
    }
}
