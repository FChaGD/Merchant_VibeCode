using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 마을별 판매 차별화가 아직 없어(Docs/기획/49번 §3.2) 모든 마을에서 테이블에 등록된 판매 대상을 공용 판매가로
    /// 파는 임시 제공자. 마을 Id는 받지만 무시한다. 실제 시스템 설계 후 대체/제거 대상이다 - 마을별 세부 데이터
    /// (Item.xlsx ItemTownPrice)를 읽는 구현으로 교체한다(설계 50번 §5.3).
    /// 시설 → 판매 대상 매핑은 코드 상수다. 판매 시설 화면이 생길 때마다 한 줄씩 추가한다(특산물 구매·대장간·잡화 상점).
    /// </summary>
    public class PlaceholderTownShopStockProvider : MonoBehaviour, ITownShopStockReader, IManagedComponent
    {
        [SerializeField] private ItemDefinitionTableAsset tradeGoodsItemTable;
        [SerializeField] private ItemStringTableAsset tradeGoodsItemStrings;
        [SerializeField] private TradeGoodsKindTableAsset tradeGoodsKindTable;

        private readonly Dictionary<string, IReadOnlyList<ShopStockEntry>> stockByFacilityId = new();
        private TableItemCatalog tradeGoodsCatalog;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<ITownShopStockReader>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            // 다른 매니저에 대한 의존성이 없다.
        }

        public IReadOnlyList<ShopStockEntry> GetStock(int cityId, string facilityId)
        {
            if (stockByFacilityId.TryGetValue(facilityId, out var cached)) return cached;

            var stock = BuildStock(facilityId);
            stockByFacilityId[facilityId] = stock;
            return stock;
        }

        private IReadOnlyList<ShopStockEntry> BuildStock(string facilityId)
        {
            switch (facilityId)
            {
                case TownFacilityIds.TradeGoodsMarket:
                    return BuildTradeGoodsStock(TradeGoodsKind.General);
                default:
                    Debug.LogWarning($"{nameof(PlaceholderTownShopStockProvider)}: '{facilityId}' 시설의 판매 대상이 정해지지 않았다.");
                    return Array.Empty<ShopStockEntry>();
            }
        }

        // 테이블 행 순서를 그대로 표시 순서로 쓴다(기획 48번 §4.2).
        private IReadOnlyList<ShopStockEntry> BuildTradeGoodsStock(TradeGoodsKind kind)
        {
            if (tradeGoodsItemTable == null || tradeGoodsKindTable == null)
            {
                Debug.LogWarning($"{nameof(PlaceholderTownShopStockProvider)}: 교역품 테이블이 배선되지 않았다(Tools > Game > Build Bootstrap Scene).");
                return Array.Empty<ShopStockEntry>();
            }

            tradeGoodsCatalog ??= new TableItemCatalog(tradeGoodsItemTable, tradeGoodsItemStrings);
            var stock = new List<ShopStockEntry>();
            foreach (var entry in tradeGoodsItemTable.Entries)
            {
                if (!tradeGoodsKindTable.TryGetKind(entry.Id, out var entryKind) || entryKind != kind) continue;
                if (!tradeGoodsCatalog.TryGetDefinition(entry.Id, out var definition)) continue;

                stock.Add(new ShopStockEntry(definition, entry.Price));
            }
            return stock;
        }
    }
}
