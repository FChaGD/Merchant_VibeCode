using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 여러 카탈로그를 하나로 보이게 한다(Docs/설계/50번 §5.2). 교역품 그리드에는 교역품과 기타 카테고리(골드 상자)가
    /// 함께 놓이는데, 카테고리별 자산은 따로라서 저장소가 두 카탈로그를 합쳐 조회한다. 앞 카탈로그가 우선한다 -
    /// Id는 임포트 단계에서 전체 중복이 금지되므로 실제로 겹치지 않는다.
    /// </summary>
    public class CompositeItemCatalog : IItemCatalogReader
    {
        private readonly IItemCatalogReader[] catalogs;
        private List<IInventoryItemDefinition> catalogItems;

        public CompositeItemCatalog(params IItemCatalogReader[] catalogs)
        {
            this.catalogs = catalogs;
        }

        public IReadOnlyList<IInventoryItemDefinition> CatalogItems
        {
            get
            {
                if (catalogItems != null) return catalogItems;

                catalogItems = new List<IInventoryItemDefinition>();
                foreach (var catalog in catalogs) catalogItems.AddRange(catalog.CatalogItems);
                return catalogItems;
            }
        }

        public bool TryGetDefinition(string id, out IInventoryItemDefinition definition)
        {
            foreach (var catalog in catalogs)
            {
                if (catalog.TryGetDefinition(id, out definition)) return true;
            }

            definition = null;
            return false;
        }
    }
}
