using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// ItemDefinitionTableAsset(+ItemStringTableAsset)을 IItemCatalogReader로 감싼다 -
    /// TableBattleUnitStatProvider와 같은 어댑터 패턴(Docs/설계/35번 §6). DI 등록 없이 저장소가 직접
    /// new해서 쓰는 순수 C# 클래스다. 테이블 자산이 비어있거나 아직 배선 전이어도(초기 콘텐츠가
    /// 없는 게 정상인 단계) 예외 대신 빈 카탈로그로 취급한다 - CharacterStatsTable과 달리 "값이 있어야
    /// 정상"이 아니라 "아직 없을 수 있다"는 전제라 Debug.LogWarning으로만 드러낸다.
    /// </summary>
    public class TableItemCatalog : IItemCatalogReader
    {
        private readonly ItemDefinitionTableAsset dataTable;
        private readonly ItemStringTableAsset stringTable;

        private Dictionary<string, IInventoryItemDefinition> definitionsById;
        private List<IInventoryItemDefinition> catalogItems;

        public TableItemCatalog(ItemDefinitionTableAsset dataTable, ItemStringTableAsset stringTable)
        {
            this.dataTable = dataTable;
            this.stringTable = stringTable;
        }

        public IReadOnlyList<IInventoryItemDefinition> CatalogItems
        {
            get
            {
                EnsureBuilt();
                return catalogItems;
            }
        }

        public bool TryGetDefinition(string id, out IInventoryItemDefinition definition)
        {
            EnsureBuilt();
            return definitionsById.TryGetValue(id, out definition);
        }

        private void EnsureBuilt()
        {
            if (definitionsById != null) return;

            definitionsById = new Dictionary<string, IInventoryItemDefinition>();
            catalogItems = new List<IInventoryItemDefinition>();

            if (dataTable == null)
            {
                Debug.LogWarning($"{nameof(TableItemCatalog)}: 아이템 정의 테이블이 배선되지 않았다 - 빈 카탈로그로 취급한다.");
                return;
            }

            foreach (var entry in dataTable.Entries)
            {
                var displayName = stringTable != null && stringTable.TryGetLabel(entry.Id, out var ko) ? ko : "값 없음";
                var definition = new TableItemDefinition(entry, displayName);
                definitionsById[entry.Id] = definition;
                catalogItems.Add(definition);
            }
        }
    }
}
