using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 무역품 구매 화면의 판매 목록(Docs/설계/81번 §3.5). 마을 재고의 무역품 행을 아이템 카탈로그와 조인한다 - 판매 여부·순서는 재고 행이,
    /// 이름·크기·가격은 아이템 테이블이 정한다. 판매가는 공용 가격이다(마을별 판매가는 범위 밖, 기획 80번 §2).
    /// </summary>
    public class TownShopStockProvider : MonoBehaviour, ITownShopStockReader, IManagedComponent
    {
        [SerializeField] private ItemDefinitionTableAsset tradeGoodsItemTable;
        [SerializeField] private ItemStringTableAsset tradeGoodsItemStrings;

        private readonly Dictionary<string, int> priceById = new();
        private ITownStockReader stockReader;
        private TableItemCatalog catalog;

        public event Action OnStockChanged;

        public void RegisterSelf(IDependencyRegistrar registrar) => registrar.Register<ITownShopStockReader>(this);

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            ITownStockReader reader = null;
            registrar?.TryResolve(out reader);
            Bind(reader);
        }

        // 테스트가 DI 없이 재고를 연결하는 진입점이기도 하다.
        public void Bind(ITownStockReader reader)
        {
            if (stockReader != null) stockReader.OnStockChanged -= ForwardChanged;
            stockReader = reader;
            if (stockReader != null) stockReader.OnStockChanged += ForwardChanged;
        }

        private void OnDestroy()
        {
            if (stockReader != null) stockReader.OnStockChanged -= ForwardChanged;
        }

        public IReadOnlyList<ShopStockEntry> GetStock(int cityId, string facilityId)
        {
            if (stockReader == null || tradeGoodsItemTable == null || facilityId != TownStockCategories.FacilityOf(TownStockCategory.TradeGoods))
            {
                return Array.Empty<ShopStockEntry>();
            }

            EnsureCatalog();
            var stock = new List<ShopStockEntry>();
            foreach (var line in stockReader.GetLines(cityId, TownStockCategory.TradeGoods))
            {
                // 임포터가 없는 Id를 막으므로 실행 중에는 건너뛰기만 한다.
                if (!catalog.TryGetDefinition(line.ItemId, out var definition) || !priceById.TryGetValue(line.ItemId, out var price)) continue;
                stock.Add(new ShopStockEntry(definition, price, line.Remaining));
            }
            return stock;
        }

        private void EnsureCatalog()
        {
            if (catalog != null) return;
            catalog = new TableItemCatalog(tradeGoodsItemTable, tradeGoodsItemStrings);
            foreach (var entry in tradeGoodsItemTable.Entries) priceById[entry.Id] = entry.Price;
        }

        private void ForwardChanged() => OnStockChanged?.Invoke();
    }
}
