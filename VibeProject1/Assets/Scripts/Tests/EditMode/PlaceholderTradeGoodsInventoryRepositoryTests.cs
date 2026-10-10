using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Game.Core;

namespace Game.Core.Tests
{
    public class PlaceholderTradeGoodsInventoryRepositoryTests
    {
        // 보유 마차 목록(설계 64번 §5, 81번 §5.2) - 저장소는 읽기 계약만 쓴다. 개체 Id와 종류 Id를 따로 받아 같은 종류 여러 대를 흉내 낸다.
        private class FakeOwnedAssets : IOwnedCaravanAssetReader
        {
            private readonly List<string> wagonIds = new();
            private readonly Dictionary<string, string> kindIdById = new();
            private readonly ICaravanAssetCatalogReader catalog;
            public event System.Action OnOwnedChanged;

            public FakeOwnedAssets(ICaravanAssetCatalogReader catalog) => this.catalog = catalog;

            public IReadOnlyList<string> GetOwnedIds(FormationUnitKind kind) => kind == FormationUnitKind.Wagon ? wagonIds : System.Array.Empty<string>();

            public bool TryGetOwned(string instanceId, out OwnedCaravanAsset asset)
            {
                asset = default;
                if (!kindIdById.TryGetValue(instanceId, out var kindId) || !catalog.TryGet(kindId, out var profile)) return false;
                var number = 0;
                foreach (var id in wagonIds)
                {
                    if (kindIdById[id] == kindId) number++;
                    if (id == instanceId) break;
                }
                asset = new OwnedCaravanAsset(instanceId, profile, number);
                return true;
            }

            public void Add(string wagonId, string kindId = null)
            {
                wagonIds.Add(wagonId);
                kindIdById[wagonId] = kindId ?? wagonId;
                OnOwnedChanged?.Invoke();
            }

            public void Remove(string wagonId)
            {
                wagonIds.Remove(wagonId);
                kindIdById.Remove(wagonId);
                OnOwnedChanged?.Invoke();
            }
        }

        private class FakeCaravanCatalog : ICaravanAssetCatalogReader
        {
            private readonly Dictionary<string, CaravanAssetProfile> byId = new();
            private readonly List<CaravanAssetProfile> all = new();

            public IReadOnlyList<CaravanAssetProfile> All => all;
            public bool TryGet(string id, out CaravanAssetProfile profile) => byId.TryGetValue(id, out profile);
            public string GetKindLabel(FormationUnitKind kind) => "마차";

            public void AddWagon(string id, string name, InventoryShape shape)
            {
                var profile = new CaravanAssetProfile(id, name, FormationUnitKind.Wagon, "마차", 1000, default, shape);
                all.Add(profile);
                byId[id] = profile;
            }
        }

        private FakeOwnedAssets ownedAssets;
        private FakeCaravanCatalog caravanCatalog;
        private GameObject gameObject;
        private DependencyManager dependencyManager;
        private InMemoryPlayerCurrencyWallet wallet;
        private PlaceholderTradeGoodsInventoryRepository repository;
        private ItemDefinitionTableAsset itemTable;
        private ItemDefinitionTableAsset miscItemTable;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject(nameof(PlaceholderTradeGoodsInventoryRepositoryTests));
            dependencyManager = gameObject.AddComponent<DependencyManager>();

            wallet = gameObject.AddComponent<InMemoryPlayerCurrencyWallet>();
            wallet.ResolveDependencies(null);
            dependencyManager.Register<IPlayerCurrencyWallet>(wallet);

            // 시작 마차 1대(5 × 4, 기획 63번 §3.1 초기값) + 구매 테스트용 마차 1종.
            caravanCatalog = new FakeCaravanCatalog();
            caravanCatalog.AddWagon("W1", "마차1", InventoryShape.Rectangle(5, 4));
            caravanCatalog.AddWagon("W2", "마차2", InventoryShape.Rectangle(2, 1));
            ownedAssets = new FakeOwnedAssets(caravanCatalog);
            ownedAssets.Add("W1");
            dependencyManager.Register<IOwnedCaravanAssetReader>(ownedAssets);

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
                repository.TryGetDefinition("gold-box", out var definition);
                return definition;
            }
        }

        [Test]
        public void ResolveDependencies_CreatesSectionPerOwnedWagon()
        {
            // 보유 마차 1대 = 섹션 1개, 모양·이름은 마차 테이블 값(기획 63번 §3.2).
            Assert.AreEqual(1, repository.Sections.Count);
            Assert.AreEqual("W1", repository.Sections[0].Id);
            Assert.AreEqual("1번 마차1", repository.Sections[0].DisplayName);
            Assert.AreEqual(5, repository.Sections[0].Shape.Width);
            Assert.AreEqual(4, repository.Sections[0].Shape.Height);
        }

        [Test]
        public void OwnedWagonAdded_AppendsSection_AndRaisesOnChanged()
        {
            var raised = 0;
            repository.OnChanged += () => raised++;

            ownedAssets.Add("W2");

            Assert.AreEqual(2, repository.Sections.Count);
            Assert.AreEqual("W2", repository.Sections[1].Id);
            Assert.AreEqual(1, raised);
        }

        [Test]
        public void TryApplyPlacements_GoldBoxAcrossWagons_DoesNotTouchWallet()
        {
            ownedAssets.Add("W2");
            repository.TryPlaceItem(GoldBoxDefinition, new GridPosition(0, 0), out var goldBox, 0, "W1");
            var amountAfterPlacing = wallet.CurrentAmount;

            // 마차 간 이동도 재배치 한 번이다 - 제거·배치가 아니므로 환급·차감이 없어야 한다(설계 64번 §1).
            Assert.IsTrue(repository.TryApplyPlacements(new[] { new ItemPlacement(goldBox.InstanceId, new GridPosition(1, 0), 0, "W2") }));
            Assert.AreEqual(amountAfterPlacing, wallet.CurrentAmount);
            Assert.IsTrue(repository.TryGetItemAt("W2", new GridPosition(1, 0), out var moved));
            Assert.AreEqual("W2", moved.SectionId);
            Assert.IsFalse(repository.TryGetItemAt("W1", new GridPosition(0, 0), out _));
        }

        [Test]
        public void TryPlaceItem_GoldBox_IsPlainItem_WalletUntouched()
        {
            // 골드 상자 차감은 변환 서비스가 한다(설계 83번 §3.4) - 저장소는 골드 상자도 일반 아이템처럼 놓기만 한다.
            wallet.TrySpend(wallet.CurrentAmount);

            Assert.IsTrue(repository.TryPlaceItem(GoldBoxDefinition, new GridPosition(0, 0), out _));
            Assert.AreEqual(0, wallet.CurrentAmount);
            Assert.AreEqual(1, repository.Items.Count);
        }

        [Test]
        public void RemoveItem_GoldBox_DoesNotRefund()
        {
            // 그리드 제거 환급 폐기(기획 82번 B7).
            repository.TryPlaceItem(GoldBoxDefinition, new GridPosition(0, 0), out var placedItem);
            var amountAfterPlacing = wallet.CurrentAmount;

            Assert.IsTrue(repository.RemoveItem(placedItem.InstanceId));
            Assert.AreEqual(amountAfterPlacing, wallet.CurrentAmount);
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
            // 2×2는 (1,2)에 놓인다 - 5 × 4 시작 마차에 맞춘 좌표(설계 64번 §5).
            Assert.IsTrue(repository.TryGetItemAt("W1", new GridPosition(2, 3), out var bottomRightOf2x2));
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
            Assert.IsTrue(repository.TryGetItemAt(null, new GridPosition(0, 1), out _));
            Assert.IsFalse(repository.TryGetItemAt(null, new GridPosition(1, 0), out _));
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

        [Test]
        public void RemoveWithoutRefund_GoldBox_WalletUnchanged()
        {
            repository.TryPlaceItem(GoldBoxDefinition, new GridPosition(0, 0), out var goldBox);
            var amountAfterPlacing = wallet.CurrentAmount;

            // 전투 손실은 환급 대상이 아니다(기획 77번 §4-19, 설계 79번 §5.1).
            Assert.IsTrue(repository.RemoveWithoutRefund(goldBox.InstanceId));
            Assert.AreEqual(amountAfterPlacing, wallet.CurrentAmount);
            Assert.AreEqual(0, repository.Items.Count);
        }

        [Test]
        public void StageWithoutCharge_GoldBox_WalletUnchanged()
        {
            var startingAmount = wallet.CurrentAmount;

            // 회수 물품은 구매가 아니다 - 골드 상자여도 차감하지 않는다(기획 77번 §4-16).
            var staged = repository.StageWithoutCharge(GoldBoxDefinition);

            Assert.AreEqual(startingAmount, wallet.CurrentAmount);
            Assert.IsTrue(staged.IsStaged);
            Assert.AreEqual(1, repository.StagedItems.Count);
            Assert.AreEqual("gold-box", repository.StagedItems[0].Definition.Id);
        }

        [Test]
        public void DiscardStaged_ClearsStaging_NoRefund()
        {
            repository.StageWithoutCharge(GoldBoxDefinition);
            repository.TryPlaceItem(GoldBoxDefinition, new GridPosition(0, 0), out var goldBox);
            repository.TryApplyPlacements(new[] { ItemPlacement.ToStaging(goldBox.InstanceId, 0) });
            var amountBeforeDiscard = wallet.CurrentAmount;
            var raised = 0;
            repository.OnChanged += () => raised++;

            repository.DiscardStaged();

            Assert.AreEqual(0, repository.StagedItems.Count);
            Assert.AreEqual(amountBeforeDiscard, wallet.CurrentAmount);
            Assert.AreEqual(1, raised);
        }

        [Test]
        public void OwnedWagonRemoved_SectionRemoved()
        {
            ownedAssets.Add("W2");
            var raised = 0;
            repository.OnChanged += () => raised++;

            ownedAssets.Remove("W1");

            Assert.AreEqual(1, repository.Sections.Count);
            Assert.AreEqual("W2", repository.Sections[0].Id);
            Assert.AreEqual(1, raised);
        }

        [Test]
        public void SectionNames_Renumber_WhenEarlierWagonRemoved()
        {
            ownedAssets.Add("W1b", "W1");
            Assert.AreEqual("2번 마차1", repository.Sections[1].DisplayName);

            ownedAssets.Remove("W1");

            Assert.AreEqual(1, repository.Sections.Count);
            Assert.AreEqual("W1b", repository.Sections[0].Id);
            Assert.AreEqual("1번 마차1", repository.Sections[0].DisplayName);
        }
    }
}
