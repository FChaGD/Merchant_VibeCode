using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 마구간 매각 1건의 판정·매각가·처리(Docs/설계/104번). 처리 순서: 판정 → 매각가 계산 → 대열 정리·보유 제거 → 판 마을 재고 반환 → 개인 골드 지급.
    /// 매각가를 제거 **전에** 계산하는 이유: 보유에서 빠지면 내구도 기록과 개체 정보가 함께 사라진다. 대열 정리·보유 제거는 전투 파괴와 같은
    /// 공용 파괴 처리(CaravanAssetDestroyer)를 쓴다. 제거 계약이나 골드 수입이 없으면 실행을 거부한다 - 보유만 빠지거나 대열만 바뀌는 경로를 막는다.
    /// 재고 반환 실패는 매각을 되돌리지 않고 경고만 남긴다(구매의 재고 차감 실패와 같은 방침, 설계 81번 §4.1).
    /// 의존성은 등록 타입으로 조회한다 - 읽기 타입으로 찾으면 조용히 null이다.
    /// </summary>
    public class CaravanAssetSaleService : MonoBehaviour, ICaravanAssetSaleService, IManagedComponent
    {
        [SerializeField] private float saleRatio = 0.5f;

        private IOwnedCaravanAssetRoster owned;
        private IInventoryReader cargo;
        private ICaravanAssetDurabilityReader durability;
        private IOwnedCaravanAssetRemover remover;
        private CaravanAssetDestroyer destroyer;
        private ITripCurrentLocationReader location;
        private ITownStockReturner stock;
        private IGoldIncome income;

        public void RegisterSelf(IDependencyRegistrar registrar) => registrar.Register<ICaravanAssetSaleService>(this);

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            if (registrar == null)
            {
                Initialize(null, null, null, null, null, null, null, null, null, null);
                return;
            }

            registrar.TryResolve(out IOwnedCaravanAssetRoster ownedRoster);
            registrar.TryResolve(out ITradeGoodsInventoryRepository tradeGoods);
            registrar.TryResolve(out ICaravanAssetDurabilityReader durabilityReader);
            registrar.TryResolve(out IFormationRepository formation);
            registrar.TryResolve(out ICaravanRosterProvider roster);
            registrar.TryResolve(out IFieldFormationActivityRepository activities);
            registrar.TryResolve(out IOwnedCaravanAssetRemover assetRemover);
            registrar.TryResolve(out ITripCurrentLocationRepository currentLocation);
            registrar.TryResolve(out ITownStockReturner stockReturner);
            registrar.TryResolve(out IGoldIncome goldIncome);
            Initialize(ownedRoster, tradeGoods, durabilityReader, formation, roster, activities, assetRemover, currentLocation, stockReturner, goldIncome);
        }

        /// <summary>DI 없이 의존성을 넣는다(테스트용 겸 ResolveDependencies 공용 경로).</summary>
        public void Initialize(IOwnedCaravanAssetRoster ownedRoster, IInventoryReader cargoReader, ICaravanAssetDurabilityReader durabilityReader,
            IFormationRepository formation, ICaravanRosterProvider roster, IFieldFormationActivityRepository activities, IOwnedCaravanAssetRemover assetRemover,
            ITripCurrentLocationReader currentLocation, ITownStockReturner stockReturner, IGoldIncome goldIncome)
        {
            owned = ownedRoster;
            cargo = cargoReader;
            durability = durabilityReader;
            remover = assetRemover;
            destroyer = new CaravanAssetDestroyer(formation, roster, activities, assetRemover);
            location = currentLocation;
            stock = stockReturner;
            income = goldIncome;
        }

        public CaravanAssetSaleCheck Evaluate(string instanceId)
        {
            if (owned == null || instanceId == null || !owned.TryGetOwned(instanceId, out var asset))
            {
                return CaravanAssetSaleRules.Evaluate(false, false, FormationUnitKind.Wagon, 0);
            }

            return CaravanAssetSaleRules.Evaluate(true, HasCargo(instanceId), asset.Profile.Kind, owned.CountOwnedOfKind(FormationUnitKind.Wagon));
        }

        public int SalePriceOf(string instanceId)
        {
            return owned != null && instanceId != null && owned.TryGetOwned(instanceId, out var asset) ? PriceOf(asset) : 0;
        }

        public bool TrySell(string instanceId)
        {
            if (Evaluate(instanceId) != CaravanAssetSaleCheck.Available) return false;
            if (remover == null || income == null)
            {
                Debug.LogWarning($"{nameof(CaravanAssetSaleService)}: 보유 제거 또는 골드 수입 계약이 없어 매각하지 않는다(Tools > Game > Build Bootstrap Scene).");
                return false;
            }

            owned.TryGetOwned(instanceId, out var asset);
            var price = PriceOf(asset);
            var kindId = asset.Profile.Id;
            var category = TownStockCategories.Of(asset.Profile.Kind);

            destroyer.Destroy(new[] { instanceId });
            if (owned.TryGetOwned(instanceId, out _))
            {
                Debug.LogWarning($"{nameof(CaravanAssetSaleService)}: '{instanceId}'를 보유 목록에서 빼지 못해 매각을 중단한다.");
                return false;
            }

            ReturnToStock(category, kindId);
            income.Earn(price);
            return true;
        }

        private int PriceOf(OwnedCaravanAsset asset)
        {
            var hasRecord = false;
            float current = 0f;
            var max = 0;
            if (durability != null) hasRecord = durability.TryGet(asset.InstanceId, out current, out max);
            return CaravanAssetSaleRules.SalePrice(asset.Profile.Price, saleRatio, CaravanAssetSaleRules.DurabilityRatio(hasRecord, current, max));
        }

        private void ReturnToStock(TownStockCategory category, string kindId)
        {
            if (location == null || stock == null)
            {
                Debug.LogWarning($"{nameof(CaravanAssetSaleService)}: 현재 위치 또는 재고 반환 계약이 없어 '{kindId}'를 마을 재고에 돌려놓지 않았다(매각은 유지).");
                return;
            }

            if (!stock.TryReturn(location.CurrentCityId, category, kindId))
            {
                Debug.LogWarning($"{nameof(CaravanAssetSaleService)}: 마을 {location.CurrentCityId} 재고에 '{kindId}'를 돌려놓지 못했다(매각은 유지).");
            }
        }

        // 무역품 인벤토리의 섹션 Id = 마차 개체 Id(설계 64번) - 그 섹션에 아이템이 하나라도 있으면 화물이 있다.
        private bool HasCargo(string instanceId)
        {
            if (cargo == null) return false;
            foreach (var item in cargo.Items)
            {
                if (item.SectionId == instanceId) return true;
            }
            return false;
        }
    }
}
