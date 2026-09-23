using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 전투 소모품 인벤토리 - 그리드 크기 5×4(20칸)는 잠정치다(Docs/기획/31번 §3.2, 밸런싱 재검토
    /// 대상). 실제 아이템 데이터 시스템이 생기면 대체/제거 대상.
    /// </summary>
    public class PlaceholderConsumableInventoryRepository : MonoBehaviour, IConsumableInventoryRepository, IManagedComponent
    {
        private const int Width = 5;
        private const int Height = 4;

        private InventoryGrid grid;

        public int GridWidth => grid.Width;
        public int GridHeight => grid.Height;
        public IReadOnlyCollection<InventoryItemInstance> Items => grid.Items;
        public event Action OnChanged;

        public void RegisterSelf(IDependencyRegistrar registrar) => registrar.Register<IConsumableInventoryRepository>(this);

        public void ResolveDependencies(IDependencyResolver registrar) => grid = new InventoryGrid(Width, Height);

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
