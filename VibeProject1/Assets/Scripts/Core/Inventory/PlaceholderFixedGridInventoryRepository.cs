using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 고정 크기 1×1 인벤토리 3종(장비/전투 소모품/상단주 개인 물품)의 공통 구현(Docs/설계/44번 §4.2). 세 저장소가
    /// 그리드 크기·초기 배치·등록 타입만 다르고 나머지가 줄 단위로 같아, 한 곳만 고치고 나머지를 빠뜨리는 일을
    /// 막으려고 모았다. 하위 클래스와 함께 Placeholder 전용이다 - 실제 아이템 시스템이 생기면 통째로 제거한다.
    /// 직렬화 필드 이름(itemTable/itemStrings)은 기존 하위 클래스와 같게 유지한다 - 기존 씬 직렬화 값과
    /// ManagerHierarchyInstaller.WireItemCatalog(필드 이름으로 배선)가 그대로 동작한다.
    /// </summary>
    public abstract class PlaceholderFixedGridInventoryRepository : MonoBehaviour, IInventoryRepository, IItemCatalogReader, IInventoryArrangement, IManagedComponent
    {
        [SerializeField] private ItemDefinitionTableAsset itemTable;
        [SerializeField] private ItemStringTableAsset itemStrings;

        private InventoryGrid grid;
        private TableItemCatalog catalog;

        // 그리드 크기는 잠정치다(Docs/기획/31번 §3.2, 밸런싱 재검토 대상).
        protected abstract int Width { get; }
        protected abstract int Height { get; }
        // 인벤토리 팝업 드래그 이동·교환·자동 정렬 검증용 초기 배치(기획 41번 §4.2, 43번 §4.2) - 정렬 결과를
        // 눈으로 확인할 수 있게 일부러 흩어 둔다.
        protected abstract IReadOnlyList<(string itemId, int x, int y)> InitialItems { get; }

        public int GridWidth => grid.Width;
        public int GridHeight => grid.Height;
        public IReadOnlyCollection<InventoryItemInstance> Items => grid.Items;
        public IReadOnlyList<InventoryItemInstance> StagedItems => grid.StagedItems;
        public event Action OnChanged;

        public IReadOnlyList<IInventoryItemDefinition> CatalogItems => catalog.CatalogItems;
        public bool TryGetDefinition(string id, out IInventoryItemDefinition definition) => catalog.TryGetDefinition(id, out definition);

        // 카테고리 마커 타입으로 등록해야 해서(Register<T>는 타입당 인스턴스 1개) 하위 클래스가 정한다.
        public abstract void RegisterSelf(IDependencyRegistrar registrar);

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            grid = new InventoryGrid(Width, Height);
            catalog = new TableItemCatalog(itemTable, itemStrings);
            PlaceholderInventorySeeder.PlaceAll(grid, catalog, InitialItems, GetType().Name);
        }

        public bool TryGetItemAt(GridPosition position, out InventoryItemInstance item) => grid.TryGetAt(position, out item);

        public bool TryPlaceItem(IInventoryItemDefinition definition, GridPosition position, out InventoryItemInstance placed)
        {
            if (!grid.TryPlace(definition, position, out placed)) return false;

            OnChanged?.Invoke();
            return true;
        }

        public bool RemoveItem(string instanceId)
        {
            if (!grid.Remove(instanceId)) return false;

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
    }
}
