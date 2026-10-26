using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 마구간 매각 Placeholder(기획 94 4-19~4-21) - 판정(화물·마지막 마차)과 매각가는 실제로 계산하고, 매각 실행은 아무것도 하지 않는다.
    /// 실제 시스템 설계 후 대체/제거 대상(지급·보유 제거·비율 밸런싱은 system 브랜치).
    /// 보유·화물 저장소는 쓰기 타입으로만 DI에 등록되므로 등록된 타입(IOwnedCaravanAssetRoster, ITradeGoodsInventoryRepository)으로 찾는다 -
    /// 읽기 타입으로 찾으면 조용히 null이다. 없으면 각각 미보유·화물 없음으로 본다.
    /// </summary>
    public class PlaceholderCaravanAssetSaleService : MonoBehaviour, ICaravanAssetSaleService, IManagedComponent
    {
        [SerializeField] private float saleRatio = 0.5f;

        private IOwnedCaravanAssetRoster owned;
        private IInventoryReader cargo;

        public void RegisterSelf(IDependencyRegistrar registrar) => registrar.Register<ICaravanAssetSaleService>(this);

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            owned = null;
            cargo = null;
            if (registrar == null) return;

            registrar.TryResolve(out owned);
            if (registrar.TryResolve(out ITradeGoodsInventoryRepository tradeGoods)) cargo = tradeGoods;
        }

        /// <summary>테스트용 - DI 없이 의존성을 넣는다.</summary>
        public void Initialize(IOwnedCaravanAssetRoster ownedRoster, IInventoryReader cargoReader)
        {
            owned = ownedRoster;
            cargo = cargoReader;
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
            return owned != null && instanceId != null && owned.TryGetOwned(instanceId, out var asset)
                ? CaravanAssetSaleRules.SalePrice(asset.Profile.Price, saleRatio)
                : 0;
        }

        public bool TrySell(string instanceId) => false;

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
