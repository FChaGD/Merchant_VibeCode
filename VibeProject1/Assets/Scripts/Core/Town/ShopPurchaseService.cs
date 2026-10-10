using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 상점 구매 1건의 판정과 처리(Docs/설계/50번 §5.4, 83번 §5.2). 재화 지출과 그리드 배치는 한쪽만 성공하면 재화가 사라지거나
    /// 복제되므로 여기서 한 연산으로 묶는다 - 지출 후 배치가 실패하면 개인 골드로 환불한다.
    /// 지출은 소유 골드(개인 + 적재) 기준이고, 개인 골드가 부족하면 골드 상자가 인출된다(기획 82번 C1·C3). 그래서 공간 판정은 인출될
    /// 상자 자리를 빈칸으로 보고, 그래도 자리가 없으면 필요한 물품만 옮기는 계획을 세운다(기획 82번 C5). 판정과 확정이 같은 계획
    /// 계산을 쓰므로 판정에서 가능했던 구매가 확정에서 실패하지 않는다. 임시 보관은 구매품을 받는 공간이 아니다(기획 48번 §3.6).
    /// 마을 재고(설계 81번 §4.1)는 지급 뒤 마지막에 차감한다 - 실패해도 지급은 유지한다. stock이 null이면 차감하지 않는다.
    /// </summary>
    public class ShopPurchaseService
    {
        private readonly IGoldSpender gold;
        private readonly IInventoryRepository inventory;
        private readonly IInventoryArrangement arrangement;
        private readonly bool allowRotation;
        private readonly ITownStockConsumer stock;

        public ShopPurchaseService(IGoldSpender gold, IInventoryRepository inventory, IInventoryArrangement arrangement, bool allowRotation, ITownStockConsumer stock)
        {
            this.gold = gold;
            this.inventory = inventory;
            this.arrangement = arrangement;
            this.allowRotation = allowRotation;
            this.stock = stock;
        }

        // 품절 → 재화 부족 → 공간 부족 순(설계 81번 §4.1, 50번 §11-2) - 품절은 기다려도 풀리지 않으므로 먼저 알린다.
        public ShopPurchaseCheck Evaluate(ShopStockEntry entry)
        {
            if (entry.Remaining <= 0) return ShopPurchaseCheck.SoldOut;
            if (!gold.CanAfford(entry.Price)) return ShopPurchaseCheck.InsufficientFunds;
            return TryBuildPlan(entry, null, out _) ? ShopPurchaseCheck.Available : ShopPurchaseCheck.NoSpace;
        }

        /// <param name="preferredSectionId">먼저 찾을 섹션. null이면 첫 섹션부터.</param>
        /// <param name="placedSectionId">실제로 들어간 섹션 - 화면이 그 마차로 전환하는 데 쓴다(기획 63번 §3.5).</param>
        public bool TryPurchase(ShopStockEntry entry, int cityId, string preferredSectionId, out string placedSectionId)
        {
            placedSectionId = null;
            if (entry.Remaining <= 0 || !gold.CanAfford(entry.Price)) return false;
            if (!TryBuildPlan(entry, preferredSectionId, out var plan)) return false;
            if (!gold.TrySpend(entry.Price)) return false;

            // 인출로 상자가 빠진 뒤라 이동 목록이 가리키는 자리는 비어 있다. 이동은 원자적으로 한 번에 적용한다.
            if (plan.Moves.Count > 0 && !arrangement.TryApplyPlacements(plan.Moves))
            {
                gold.Refund(entry.Price);
                return false;
            }

            if (!inventory.TryPlaceItem(entry.Definition, plan.Position, out _, plan.QuarterTurns, plan.SectionId))
            {
                gold.Refund(entry.Price); // 배치 실패 롤백(인출한 상자·적용된 이동은 되돌리지 않음, 설계 83번 §5.2)
                return false;
            }

            if (stock != null && !stock.TryConsume(cityId, TownStockCategory.TradeGoods, entry.Definition.Id))
            {
                Debug.LogWarning($"{nameof(ShopPurchaseService)}: 마을 {cityId} 재고에서 '{entry.Definition.Id}'를 차감하지 못했다(지급은 유지).");
            }

            placedSectionId = plan.SectionId;
            return true;
        }

        private bool TryBuildPlan(ShopStockEntry entry, string preferredSectionId, out CargoPurchasePlan plan)
        {
            var withdrawn = new HashSet<string>(gold.PreviewWithdrawal(entry.Price));
            var remaining = new List<InventoryItemInstance>(inventory.Items.Count);
            foreach (var item in inventory.Items)
            {
                if (!withdrawn.Contains(item.InstanceId)) remaining.Add(item);
            }
            return CargoPurchasePlanner.TryPlan(inventory.Sections, preferredSectionId, remaining, entry.Definition, allowRotation, out plan);
        }
    }
}
