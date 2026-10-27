using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 퀘스트의 무역품 출입(Docs/설계/107번 §8.1) - 지급 화물·아이템 보상 배치, 납품 차감, 실패 시 회수. 배치·제거 규칙은 이벤트 무역품 효과(EventCargoGoods)를
    /// 그대로 쓴다(빈자리 계획 → 못 놓으면 임시 보관, 제거는 환급 없이 임시 보관 → 적재 순) - 같은 인벤토리에 유입 규칙이 둘이 되지 않게.
    /// "전부 들어가야 하는" 경로(수락 지급·보고 보상)는 사전 판정(CargoFitSimulator)으로 먼저 막는다.
    /// </summary>
    public sealed class QuestCargoService
    {
        private readonly ITradeGoodsInventoryRepository inventory;
        private readonly EventCargoGoods goods;

        public QuestCargoService(ITradeGoodsInventoryRepository inventory, ITradeGoodsCargoSettlement settlement)
        {
            this.inventory = inventory;
            goods = new EventCargoGoods(inventory, settlement, null, null);
        }

        /// <summary>적재 + 임시 보관 개수.</summary>
        public int Count(string itemId) => itemId == null ? 0 : goods.CountOf(itemId);

        /// <summary>removals = 먼저 빠질 물품(보고 납품). 실제 제거(Remove)와 같은 순서로 빠질 개체를 골라 그 자리를 비운 채 판정한다.</summary>
        public bool CanFit(IReadOnlyList<(string itemId, int count)> items, IReadOnlyList<(string itemId, int count)> removals = null)
        {
            var definitions = new List<IInventoryItemDefinition>();
            foreach (var (itemId, count) in items)
            {
                if (count <= 0) continue;
                if (itemId == null || !inventory.TryGetDefinition(itemId, out var definition)) return false;
                for (var i = 0; i < count; i++) definitions.Add(definition);
            }
            if (definitions.Count == 0) return true;
            return CargoFitSimulator.CanFitAll(inventory.Sections, RemainingAfter(removals), definitions, InventoryPopupSpecs.TradeGoods.AllowsRotation);
        }

        private IReadOnlyCollection<InventoryItemInstance> RemainingAfter(IReadOnlyList<(string itemId, int count)> removals)
        {
            if (removals == null || removals.Count == 0) return inventory.Items;
            var removed = new HashSet<string>();
            foreach (var (itemId, count) in removals)
            {
                if (itemId == null || count <= 0) continue;
                var taken = 0;
                foreach (var instanceId in goods.RemovalOrder(itemId))
                {
                    if (taken >= count) break;
                    if (removed.Add(instanceId)) taken++;
                }
            }
            var remaining = new List<InventoryItemInstance>();
            foreach (var item in inventory.Items)
            {
                if (!removed.Contains(item.InstanceId)) remaining.Add(item);
            }
            return remaining;
        }

        /// <summary>전부 들어갈 때만 배치한다. 하나라도 안 들어가면 아무것도 바꾸지 않고 false.</summary>
        public bool TryGiveAll(IReadOnlyList<(string itemId, int count)> items)
        {
            if (!CanFit(items)) return false;
            foreach (var (itemId, count) in items) GiveOrStage(itemId, count);
            return true;
        }

        /// <summary>놓을 수 있는 만큼 놓고 나머지는 임시 보관. 반환 = 임시 보관으로 간 개수.</summary>
        public int GiveOrStage(string itemId, int count)
        {
            if (count <= 0) return 0;
            return goods.TryAdd(itemId, count, out _, out var staged) ? staged : 0;
        }

        /// <summary>임시 보관 → 적재 순으로 제거. 반환 = 실제 제거 개수.</summary>
        public int Remove(string itemId, int count)
        {
            if (count <= 0) return 0;
            return goods.TryRemove(itemId, count, out _, out var removed) ? removed : 0;
        }

        public string NameOf(string itemId)
            => itemId != null && inventory.TryGetDefinition(itemId, out var definition) ? definition.DisplayName : PlaceholderText.Missing;
    }
}
