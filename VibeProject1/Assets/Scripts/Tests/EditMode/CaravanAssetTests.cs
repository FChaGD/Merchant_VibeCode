using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Game.Core;
using Object = UnityEngine.Object;

namespace Game.Core.Tests
{
    public class CaravanAssetTests
    {
        // 구매 서비스는 IOwnedCaravanAssetRoster 계약만 쓴다 - 종류 정보는 후보 프로필에서 받으므로 Id → 종류만 기억한다.
        private class FakeRoster : IOwnedCaravanAssetRoster
        {
            private readonly Dictionary<string, FormationUnitKind> kindById = new();
            private readonly Dictionary<string, FormationUnitKind> knownKinds = new();
            private int serial;
            public bool FailNextAdd { get; set; }
            public event Action OnOwnedChanged;

            public void Know(CaravanAssetProfile profile) => knownKinds[profile.Id] = profile.Kind;

            public int CountOwnedOfKind(FormationUnitKind kind) => kindById.Values.Count(k => k == kind);

            public IReadOnlyList<string> GetOwnedIds(FormationUnitKind kind) => kindById.Where(pair => pair.Value == kind).Select(pair => pair.Key).ToList();

            public bool TryGetOwned(string instanceId, out OwnedCaravanAsset asset)
            {
                asset = default;
                return false;
            }

            public bool TryAddOwned(string kindId, out string instanceId)
            {
                instanceId = null;
                if (FailNextAdd || !knownKinds.TryGetValue(kindId, out var kind)) return false;
                instanceId = $"{kindId}#{++serial}";
                kindById[instanceId] = kind;
                OnOwnedChanged?.Invoke();
                return true;
            }
        }

        private class FakeStock : ITownStockReader, ITownStockConsumer
        {
            public readonly Dictionary<(int, TownStockCategory, string), int> Remaining = new();
            public event Action OnStockChanged;
            public IReadOnlyList<TownStockLine> GetLines(int cityId, TownStockCategory category)
                => Remaining.Where(p => p.Key.Item1 == cityId && p.Key.Item2 == category).Select(p => new TownStockLine(p.Key.Item3, p.Value)).ToList();
            public bool TryConsume(int cityId, TownStockCategory category, string itemId)
            {
                var key = (cityId, category, itemId);
                if (!Remaining.TryGetValue(key, out var n) || n <= 0) return false;
                Remaining[key] = n - 1;
                OnStockChanged?.Invoke();
                return true;
            }
        }

        private readonly List<Object> created = new();
        private InMemoryPlayerCurrencyWallet wallet;
        private CaravanAssetTableAsset wagonTable;
        private CaravanAssetStringsTableAsset wagonStrings;
        private CaravanAssetTableAsset facilityTable;
        private CaravanAssetStringsTableAsset facilityStrings;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject(nameof(CaravanAssetTests));
            created.Add(go);
            wallet = go.AddComponent<InMemoryPlayerCurrencyWallet>();
            wallet.ResolveDependencies(null); // 기본 소지 재화 상한만큼 가득 찬 상태로 시작

            (wagonTable, wagonStrings) = CreateTables("Wagon", "마차이름", 3);
            (facilityTable, facilityStrings) = CreateTables("Facility", "시설이름", 3);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in created) Object.DestroyImmediate(obj);
            created.Clear();
        }

        private TableCaravanAssetCatalog CreateCatalog() => new(wagonTable, wagonStrings, facilityTable, facilityStrings);

        [Test]
        public void Catalog_JoinsNameKindAndPrice_WagonsBeforeFacilities()
        {
            var catalog = CreateCatalog();

            CollectionAssert.AreEqual(new[] { "Wagon01", "Wagon02", "Wagon03", "Facility01", "Facility02", "Facility03" }, catalog.All.Select(p => p.Id).ToArray());
            Assert.IsTrue(catalog.TryGet("Facility02", out var profile));
            Assert.AreEqual("시설이름2", profile.Name);
            Assert.AreEqual(FormationUnitKind.Facility, profile.Kind);
            Assert.AreEqual("시설", profile.KindLabel);
            Assert.AreEqual(1002, profile.Price);
        }

        [Test]
        public void PlaceholderRoster_StartsWithOnePerKind_AndPurchaseKeepsTableOrder()
        {
            var go = new GameObject("Roster");
            created.Add(go);
            var dependencyManager = go.AddComponent<DependencyManager>();
            dependencyManager.Register<ICaravanAssetCatalogReader>(CreateCatalog());
            var roster = go.AddComponent<PlaceholderCaravanRosterProvider>();
            roster.ResolveDependencies(dependencyManager);

            CollectionAssert.AreEqual(new[] { "Wagon01#1", "Facility01#1" }, roster.GetRoster().Select(u => u.Id).ToArray());
            Assert.AreEqual(1, roster.CountOwnedOfKind(FormationUnitKind.Wagon));

            var changed = 0;
            roster.OnOwnedChanged += () => changed++;
            // 같은 종류도 여러 대 보유한다(기획 80번 §3-3) - 개체 Id는 발급 Id다(설계 81번 §5.1).
            Assert.IsTrue(roster.TryAddOwned("Wagon03", out var first));
            Assert.IsTrue(roster.TryAddOwned("Wagon03", out var second));
            Assert.IsFalse(roster.TryAddOwned("Missing", out _));
            Assert.AreEqual(2, changed);
            Assert.AreEqual("Wagon03#1", first);
            Assert.AreEqual("Wagon03#2", second);

            CollectionAssert.AreEqual(new[] { "Wagon01#1", "Wagon03#1", "Wagon03#2", "Facility01#1" }, roster.GetRoster().Select(u => u.Id).ToArray());
            // 정비창 팔레트는 개체 이름이 아니라 종류명을 표시한다(기획 55번 §3). 개체 이름은 정보 패널용 InstanceName.
            var wagon = roster.GetRoster().First(u => u.Id == second);
            Assert.AreEqual("마차", wagon.DisplayName);
            Assert.AreEqual(FormationUnitKind.Wagon, wagon.Kind);
            Assert.AreEqual("2번 마차이름3", ((IInstanceNamedUnit)wagon).InstanceName);

            Assert.IsTrue(roster.TryGetOwned(second, out var asset));
            Assert.AreEqual("Wagon03", asset.Profile.Id);
            Assert.AreEqual(2, asset.Number);

            // 앞 개체가 빠지면 번호가 당겨진다(기획 80번 §4-1).
            Assert.IsTrue(roster.TryRemoveOwned(first));
            Assert.AreEqual("1번 마차이름3", ((IInstanceNamedUnit)wagon).InstanceName);
        }

        [Test]
        public void TryPurchase_Success_DeductsAndAddsToRoster()
        {
            var (service, roster) = CreateService();
            var candidate = Profile(roster, "Wagon02", FormationUnitKind.Wagon, 1000);
            var startingAmount = wallet.CurrentAmount;

            Assert.AreEqual(CaravanAssetPurchaseCheck.Available, service.Evaluate(candidate));
            Assert.IsTrue(service.TryPurchase(candidate));
            Assert.AreEqual(startingAmount - 1000, wallet.CurrentAmount);
            Assert.AreEqual(1, roster.CountOwnedOfKind(FormationUnitKind.Wagon));
        }

        [Test]
        public void Evaluate_InsufficientFunds_BlocksPurchase()
        {
            var (service, roster) = CreateService();
            wallet.TrySpend(wallet.CurrentAmount - 999);
            var candidate = Profile(roster, "Wagon02", FormationUnitKind.Wagon, 1000);

            Assert.AreEqual(CaravanAssetPurchaseCheck.InsufficientFunds, service.Evaluate(candidate));
            Assert.IsFalse(service.TryPurchase(candidate));
            Assert.AreEqual(999, wallet.CurrentAmount);
        }

        [Test]
        public void Evaluate_KindFull_ReportsOwnedFull_OtherKindUnaffected()
        {
            var (service, roster) = CreateService();
            for (var i = 1; i <= CaravanAssetPurchaseService.MaxOwnedPerKind; i++)
            {
                Assert.IsTrue(service.TryPurchase(Profile(roster, $"Wagon0{i}", FormationUnitKind.Wagon, 1)));
            }

            Assert.AreEqual(CaravanAssetPurchaseCheck.OwnedFull, service.Evaluate(Profile(roster, "Wagon06", FormationUnitKind.Wagon, 1)));
            Assert.AreEqual(CaravanAssetPurchaseCheck.Available, service.Evaluate(Profile(roster, "Facility01", FormationUnitKind.Facility, 1)));
        }

        [Test]
        public void TryPurchase_RosterAddFails_RefundsCurrency()
        {
            var (service, roster) = CreateService();
            var candidate = Profile(roster, "Wagon02", FormationUnitKind.Wagon, 1000);
            var startingAmount = wallet.CurrentAmount;
            roster.FailNextAdd = true;

            Assert.IsFalse(service.TryPurchase(candidate));
            Assert.AreEqual(startingAmount, wallet.CurrentAmount);
            Assert.AreEqual(0, roster.CountOwnedOfKind(FormationUnitKind.Wagon));
        }

        private (CaravanAssetPurchaseService, FakeRoster) CreateService()
        {
            var roster = new FakeRoster();
            return (new CaravanAssetPurchaseService(wallet, roster), roster);
        }

        private static CaravanAssetProfile Profile(FakeRoster roster, string id, FormationUnitKind kind, int price)
        {
            var profile = new CaravanAssetProfile(id, id, kind, kind.ToString(), price);
            roster.Know(profile);
            return profile;
        }

        // 행마다 가격을 달리해(1001, 1002, ...) 개체별 행이 따로 조인되는지 확인한다.
        private (CaravanAssetTableAsset, CaravanAssetStringsTableAsset) CreateTables(string idPrefix, string namePrefix, int count)
        {
            var entries = new List<CaravanAssetEntry>();
            var names = new List<SlugLocalizedStringEntry>();
            for (var i = 1; i <= count; i++)
            {
                var id = $"{idPrefix}0{i}";
                entries.Add(new CaravanAssetEntry { Id = id, Price = 1000 + i });
                names.Add(new SlugLocalizedStringEntry { Id = id, Ko = $"{namePrefix}{i}" });
            }

            var table = ScriptableObject.CreateInstance<CaravanAssetTableAsset>();
            created.Add(table);
            SetPrivateField(table, "entries", entries);
            var strings = ScriptableObject.CreateInstance<CaravanAssetStringsTableAsset>();
            created.Add(strings);
            SetPrivateField(strings, "strings", names);
            return (table, strings);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }

        [Test]
        public void Candidates_ExpandPerUnit_DecreaseByOne()
        {
            var stock = new FakeStock();
            stock.Remaining[(4, TownStockCategory.Wagon, "Wagon01")] = 2;
            stock.Remaining[(4, TownStockCategory.Facility, "Facility02")] = 1;
            var go = new GameObject("candidates");
            created.Add(go);
            var provider = go.AddComponent<CaravanAssetCandidateProvider>();
            provider.Bind(stock, CreateCatalog());
            var changed = 0;
            provider.OnCandidatesChanged += () => changed++;

            CollectionAssert.AreEqual(new[] { "Wagon01", "Wagon01", "Facility02" }, provider.GetCandidates(4, TownFacilityIds.Stable).Select(p => p.Id).ToArray());

            stock.TryConsume(4, TownStockCategory.Wagon, "Wagon01");

            CollectionAssert.AreEqual(new[] { "Wagon01", "Facility02" }, provider.GetCandidates(4, TownFacilityIds.Stable).Select(p => p.Id).ToArray());
            Assert.AreEqual(1, changed);
            Assert.AreEqual(0, provider.GetCandidates(4, TownFacilityIds.TradeGoodsMarket).Count);
            Assert.AreEqual(0, provider.GetCandidates(1, TownFacilityIds.Stable).Count);
        }
    }
}
