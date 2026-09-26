using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 장비 인벤토리 - 그리드 크기 6×4(24칸)는 잠정치다(Docs/기획/31번 §3.2, 밸런싱 재검토 대상).
    /// 실제 아이템 데이터 시스템이 생기면 대체/제거 대상.
    /// </summary>
    public class PlaceholderEquipmentInventoryRepository : MonoBehaviour, IEquipmentInventoryRepository, IManagedComponent
    {
        private const int Width = 6;
        private const int Height = 4;

        // 전투 장비 팝업의 드래그 이동·교환·자동 정렬 검증용 초기 배치(Docs/기획/41번 §4.2, 설계 42번 §5.2). 품목은
        // 실제 테이블(Equipment.xlsx)의 "placeholder-equipment-*" 행 - 실제 장비 획득 시스템이 생기면 이 목록과
        // 테이블 행을 함께 제거한다. 정렬 결과를 눈으로 확인할 수 있게 일부러 흩어 둔다.
        private static readonly (string itemId, int x, int y)[] PlaceholderInitialItems =
        {
            ("placeholder-equipment-1", 0, 0),
            ("placeholder-equipment-2", 3, 1),
            ("placeholder-equipment-3", 5, 3),
            ("placeholder-equipment-4", 1, 3),
        };

        [SerializeField] private ItemDefinitionTableAsset itemTable;
        [SerializeField] private ItemStringTableAsset itemStrings;

        private InventoryGrid grid;
        private TableItemCatalog catalog;

        public int GridWidth => grid.Width;
        public int GridHeight => grid.Height;
        public IReadOnlyCollection<InventoryItemInstance> Items => grid.Items;
        public IReadOnlyList<InventoryItemInstance> StagedItems => grid.StagedItems;
        public event Action OnChanged;

        public IReadOnlyList<IInventoryItemDefinition> CatalogItems => catalog.CatalogItems;
        public bool TryGetDefinition(string id, out IInventoryItemDefinition definition) => catalog.TryGetDefinition(id, out definition);

        public void RegisterSelf(IDependencyRegistrar registrar) => registrar.Register<IEquipmentInventoryRepository>(this);

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            grid = new InventoryGrid(Width, Height);
            catalog = new TableItemCatalog(itemTable, itemStrings);
            PlaceInitialItems();
        }

        // 테이블에 행이 없으면(임포트 전) 건너뛴다. 초기화 중이라 OnChanged는 발행하지 않는다.
        private void PlaceInitialItems()
        {
            foreach (var (itemId, x, y) in PlaceholderInitialItems)
            {
                if (!catalog.TryGetDefinition(itemId, out var definition)) continue;
                if (!grid.TryPlace(definition, new GridPosition(x, y), out _))
                {
                    Debug.LogWarning($"{nameof(PlaceholderEquipmentInventoryRepository)}: 초기 아이템 '{itemId}'을(를) ({x}, {y})에 배치하지 못했다.");
                }
            }
        }

        public bool TryGetItemAt(GridPosition position, out InventoryItemInstance item) => grid.TryGetAt(position, out item);

        public bool TryPlaceItem(IInventoryItemDefinition definition, GridPosition position, out InventoryItemInstance placed)
        {
            if (!grid.TryPlace(definition, position, out placed)) return false;

            OnChanged?.Invoke();
            return true;
        }

        public bool TryApplyPlacements(IReadOnlyList<ItemPlacement> placements)
        {
            if (!grid.TryApplyPlacements(placements)) return false;

            OnChanged?.Invoke();
            return true;
        }

        public bool CanApplyPlacements(IReadOnlyList<ItemPlacement> placements) => grid.CanApplyPlacements(placements);

        public bool RemoveItem(string instanceId)
        {
            if (!grid.Remove(instanceId)) return false;

            OnChanged?.Invoke();
            return true;
        }
    }
}
