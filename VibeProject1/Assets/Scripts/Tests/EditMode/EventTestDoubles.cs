using System;
using System.Collections.Generic;

namespace Game.Core.Tests
{
    // 이벤트 효과 테스트(설계 89) 공용 가짜 - 회수 적재 정산 계약을 골드 테스트 인벤토리 위에 얹는다(임시 보관 넣기·환급 없는 제거만 필요).
    internal sealed class CargoTestSettlement : ITradeGoodsCargoSettlement
    {
        private readonly GoldTestInventory inventory;

        public CargoTestSettlement(GoldTestInventory inventory) => this.inventory = inventory;

        public IReadOnlyList<InventoryItemInstance> StagedItems => inventory.StagedItems;

        public event Action OnChanged
        {
            add => inventory.OnChanged += value;
            remove => inventory.OnChanged -= value;
        }

        public void DiscardStaged() => inventory.DiscardStaged();
        public bool RemoveWithoutRefund(string instanceId) => inventory.RemoveItem(instanceId);
        public InventoryItemInstance StageWithoutCharge(IInventoryItemDefinition definition) => inventory.Stage(definition);
    }
}
