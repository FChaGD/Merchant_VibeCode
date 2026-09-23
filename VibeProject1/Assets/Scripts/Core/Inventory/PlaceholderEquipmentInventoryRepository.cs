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

        [SerializeField] private ItemDefinitionTableAsset itemTable;
        [SerializeField] private ItemStringTableAsset itemStrings;

        private InventoryGrid grid;
        private TableItemCatalog catalog;

        public int GridWidth => grid.Width;
        public int GridHeight => grid.Height;
        public IReadOnlyCollection<InventoryItemInstance> Items => grid.Items;
        public event Action OnChanged;

        public IReadOnlyList<IInventoryItemDefinition> CatalogItems => catalog.CatalogItems;
        public bool TryGetDefinition(string id, out IInventoryItemDefinition definition) => catalog.TryGetDefinition(id, out definition);

        public void RegisterSelf(IDependencyRegistrar registrar) => registrar.Register<IEquipmentInventoryRepository>(this);

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            grid = new InventoryGrid(Width, Height);
            catalog = new TableItemCatalog(itemTable, itemStrings);
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
    }
}
