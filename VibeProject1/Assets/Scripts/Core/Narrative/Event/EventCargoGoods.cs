using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>잃은 무역품 묶음 하나(결과 줄 「밀 ×1, 소금 ×2 잃음」).</summary>
    public readonly struct GoodsLoss
    {
        public string Name { get; }
        public int Count { get; }

        public GoodsLoss(string name, int count)
        {
            Name = name;
            Count = count;
        }
    }

    /// <summary>
    /// 이벤트 무역품 효과(설계 89 §5.1). 얻기는 구매와 같은 빈자리 규칙(CargoPurchasePlanner - 필요한 물품만 옮겨 자리 만들기)으로 놓고, 못 놓은 것은 임시
    /// 보관으로 보낸다(정리는 호출자가 회수 적재 화면으로). 잃기는 환급 없는 경로이고 가진 만큼만. 품목 지정 손실은 임시 보관 → 적재(보유 마차 순서, 위 행·
    /// 왼쪽부터) 순(§2 2026-10-16) - 적재 상태를 가능한 한 덜 흐트러뜨린다. 무작위 손실은 골드 상자를 뺀 무역품 개체 중 고른다. 순수 - 난수는 주입한다.
    /// </summary>
    public sealed class EventCargoGoods
    {
        public const string RandomTarget = "random";

        private readonly ITradeGoodsInventoryRepository inventory;
        private readonly ITradeGoodsCargoSettlement settlement;
        private readonly Func<string, bool> isGoldItem;
        private readonly Func<int, int> randomIndex;

        public EventCargoGoods(ITradeGoodsInventoryRepository inventory, ITradeGoodsCargoSettlement settlement, Func<string, bool> isGoldItem, Func<int, int> randomIndex)
        {
            this.inventory = inventory;
            this.settlement = settlement;
            this.isGoldItem = isGoldItem ?? (_ => false);
            this.randomIndex = randomIndex ?? (_ => 0);
        }

        /// <summary>false = 없는 품목. staged = 자리가 없어 임시 보관으로 간 개수.</summary>
        public bool TryAdd(string itemId, int count, out string name, out int staged)
        {
            name = itemId;
            staged = 0;
            if (count <= 0 || itemId == null || !inventory.TryGetDefinition(itemId, out var definition)) return false;
            name = definition.DisplayName;
            var allowRotation = InventoryPopupSpecs.TradeGoods.AllowsRotation;
            for (var i = 0; i < count; i++)
            {
                if (CargoPurchasePlanner.TryPlan(inventory.Sections, null, inventory.Items, definition, allowRotation, out var plan)
                    && (plan.Moves.Count == 0 || inventory.TryApplyPlacements(plan.Moves))
                    && inventory.TryPlaceItem(definition, plan.Position, out _, plan.QuarterTurns, plan.SectionId))
                {
                    continue;
                }
                settlement.StageWithoutCharge(definition);
                staged++;
            }
            return true;
        }

        /// <summary>false = 없는 품목. removed = 실제로 잃은 개수(0이면 「없음」 줄).</summary>
        public bool TryRemove(string itemId, int count, out string name, out int removed)
        {
            name = itemId;
            removed = 0;
            if (itemId == null || !inventory.TryGetDefinition(itemId, out var definition)) return false;
            name = definition.DisplayName;
            foreach (var instanceId in RemovalOrder(itemId))
            {
                if (removed >= count) break;
                if (settlement.RemoveWithoutRefund(instanceId)) removed++;
            }
            return true;
        }

        public IReadOnlyList<GoodsLoss> RemoveRandom(int count)
        {
            var pool = new List<InventoryItemInstance>(settlement.StagedItems);
            pool.AddRange(inventory.Items);
            pool.RemoveAll(item => isGoldItem(item.Definition.Id));

            var order = new List<string>();
            var counts = new Dictionary<string, int>();
            for (var i = 0; i < count && pool.Count > 0; i++)
            {
                var index = Math.Min(pool.Count - 1, Math.Max(0, randomIndex(pool.Count)));
                var picked = pool[index];
                pool.RemoveAt(index);
                if (!settlement.RemoveWithoutRefund(picked.InstanceId)) continue;
                var label = picked.Definition.DisplayName;
                if (!counts.ContainsKey(label))
                {
                    counts[label] = 0;
                    order.Add(label);
                }
                counts[label]++;
            }

            var result = new List<GoodsLoss>(order.Count);
            foreach (var label in order) result.Add(new GoodsLoss(label, counts[label]));
            return result;
        }

        /// <summary>보유 수량(적재 + 임시 보관) - Yarn 함수 goods(품목Id)가 쓴다.</summary>
        public int CountOf(string itemId)
        {
            var n = 0;
            foreach (var item in settlement.StagedItems) if (item.Definition.Id == itemId) n++;
            foreach (var item in inventory.Items) if (item.Definition.Id == itemId) n++;
            return n;
        }

        /// <summary>품목 지정 제거 순서 - 임시 보관 → 적재(섹션 = 보유 마차 순서, 위 행·왼쪽부터 - InventoryAutoSorter와 같은 Y 작을수록 위). 제거 결과를 미리 판정하는 쪽(퀘스트 보고 공간)도 같은 순서를 쓴다.</summary>
        public List<string> RemovalOrder(string itemId)
        {
            var ids = new List<string>();
            foreach (var item in settlement.StagedItems) if (item.Definition.Id == itemId) ids.Add(item.InstanceId);

            var placed = new List<InventoryItemInstance>();
            foreach (var item in inventory.Items) if (item.Definition.Id == itemId) placed.Add(item);
            placed.Sort((a, b) =>
            {
                var bySection = SectionIndex(a.SectionId).CompareTo(SectionIndex(b.SectionId));
                if (bySection != 0) return bySection;
                var byRow = a.Position.Y.CompareTo(b.Position.Y);
                return byRow != 0 ? byRow : a.Position.X.CompareTo(b.Position.X);
            });
            foreach (var item in placed) ids.Add(item.InstanceId);
            return ids;
        }

        private int SectionIndex(string sectionId)
        {
            var sections = inventory.Sections;
            for (var i = 0; i < sections.Count; i++) if (sections[i].Id == sectionId) return i;
            return int.MaxValue;
        }
    }
}
