using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;

namespace Game.Core.Tests
{
    // 골드 관련 테스트(설계 83번 §8) 공용 가짜 객체 - MonoBehaviour 없이 순수 로직만 검증하기 위함이다.
    internal sealed class GoldTestItem : IInventoryItemDefinition
    {
        public string Id { get; }
        public string DisplayName => Id;
        public string Description => string.Empty;
        public Sprite Icon => null;
        public int FootprintWidth { get; }
        public int FootprintHeight { get; }

        public GoldTestItem(string id, int width, int height)
        {
            Id = id;
            FootprintWidth = width;
            FootprintHeight = height;
        }
    }

    internal sealed class GoldTestInventory : ITradeGoodsInventoryRepository
    {
        private readonly InventoryGrid grid;
        private readonly Dictionary<string, IInventoryItemDefinition> catalog = new();

        public bool FailNextPlacement { get; set; }
        public event Action OnChanged;

        public GoldTestInventory(int width, int height) => grid = new InventoryGrid(width, height);

        public GoldTestInventory(params InventorySection[] sections)
        {
            grid = new InventoryGrid();
            foreach (var section in sections) grid.AddSection(section);
        }

        public static InventorySection Section(string id, int width, int height) => new(id, id, InventoryShape.Rectangle(width, height));

        public IReadOnlyList<InventorySection> Sections => grid.Sections;
        public IReadOnlyCollection<InventoryItemInstance> Items => grid.Items;
        public IReadOnlyList<InventoryItemInstance> StagedItems => grid.StagedItems;
        public IReadOnlyList<IInventoryItemDefinition> CatalogItems => new List<IInventoryItemDefinition>(catalog.Values);

        public void AddDefinition(IInventoryItemDefinition definition) => catalog[definition.Id] = definition;
        public bool TryGetDefinition(string id, out IInventoryItemDefinition definition) => catalog.TryGetValue(id, out definition);
        public bool TryGetItemAt(string sectionId, GridPosition position, out InventoryItemInstance item) => grid.TryGetAt(sectionId, position, out item);
        public bool TryGetItemAt(GridPosition position, out InventoryItemInstance item) => grid.TryGetAt(null, position, out item);

        public bool TryPlaceItem(IInventoryItemDefinition definition, GridPosition position, out InventoryItemInstance placed, int quarterTurns = 0, string sectionId = null)
        {
            if (FailNextPlacement)
            {
                placed = default;
                return false;
            }

            if (!grid.TryPlace(definition, position, out placed, quarterTurns, sectionId)) return false;
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

        public InventoryItemInstance Place(IInventoryItemDefinition definition, int x, int y, string sectionId = null, int quarterTurns = 0)
        {
            if (!grid.TryPlace(definition, new GridPosition(x, y), out var placed, quarterTurns, sectionId))
            {
                throw new InvalidOperationException($"테스트 준비 실패: {definition.Id}를 ({x},{y})에 놓을 수 없다.");
            }
            OnChanged?.Invoke();
            return placed;
        }

        public InventoryItemInstance Stage(IInventoryItemDefinition definition)
        {
            var staged = grid.StageNew(definition);
            OnChanged?.Invoke();
            return staged;
        }
    }
}
