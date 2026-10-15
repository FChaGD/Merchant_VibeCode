using System.Collections.Generic;

namespace Game.Core
{
    public enum CaravanAssetRepairCheck
    {
        Available,
        NotDamaged,
        InsufficientFunds,
    }

    /// <summary>
    /// 마구간 수리 판정·실행(Docs/설계/91번 §5.1, 기획 90번 §3-10·§4.3). 구매 서비스(CaravanAssetPurchaseService)와 같이 판정과 실행을 나눠
    /// 버튼 상태와 실제 처리가 같은 기준을 쓰게 한다. 지출 → 복원 순이고 복원이 실패하면 환급한다.
    /// 전체 수리는 합계를 한 번에 지출한다 - 합계가 부족하면 아무것도 고치지 않는다(부분 수리 없음, §3-10).
    /// 지출 순서(개인 → 적재 골드 상자, 기획 82번)는 IGoldSpender가 처리한다.
    /// </summary>
    public sealed class CaravanAssetRepairService
    {
        private static readonly FormationUnitKind[] Kinds = { FormationUnitKind.Wagon, FormationUnitKind.Facility };

        private readonly IGoldSpender gold;
        private readonly IOwnedCaravanAssetReader owned;
        private readonly ICaravanAssetDurabilityReader durability;
        private readonly ICaravanAssetDurabilityWriter writer;

        public CaravanAssetRepairService(IGoldSpender gold, IOwnedCaravanAssetReader owned, ICaravanAssetDurabilityReader durability, ICaravanAssetDurabilityWriter writer)
        {
            this.gold = gold;
            this.owned = owned;
            this.durability = durability;
            this.writer = writer;
        }

        public int CostOf(string instanceId)
        {
            if (!owned.TryGetOwned(instanceId, out var asset) || !durability.TryGet(instanceId, out var current, out var max)) return 0;
            return CaravanAssetDurabilityRules.RepairCost(asset.Profile.Price, current, max, CaravanAssetDurabilityRules.CoefficientOf(asset.Profile));
        }

        public int TotalCost()
        {
            var total = 0;
            foreach (var (_, cost) in DamagedTargets()) total += cost;
            return total;
        }

        public CaravanAssetRepairCheck Evaluate(string instanceId) => Check(CostOf(instanceId));

        public CaravanAssetRepairCheck EvaluateAll() => Check(TotalCost());

        public bool TryRepair(string instanceId)
        {
            var cost = CostOf(instanceId);
            if (Check(cost) != CaravanAssetRepairCheck.Available || !gold.TrySpend(cost)) return false;
            if (writer.TryRestore(instanceId)) return true;

            gold.Refund(cost); // 복원 실패 롤백(구매의 발급 실패 롤백과 같음)
            return false;
        }

        public bool TryRepairAll()
        {
            var targets = DamagedTargets();
            var total = 0;
            foreach (var (_, cost) in targets) total += cost;
            if (Check(total) != CaravanAssetRepairCheck.Available || !gold.TrySpend(total)) return false;

            var failed = 0;
            foreach (var (id, cost) in targets)
            {
                if (!writer.TryRestore(id)) failed += cost;
            }
            if (failed > 0) gold.Refund(failed);
            return failed < total;
        }

        private CaravanAssetRepairCheck Check(int cost)
        {
            if (cost <= 0) return CaravanAssetRepairCheck.NotDamaged;
            return gold.CanAfford(cost) ? CaravanAssetRepairCheck.Available : CaravanAssetRepairCheck.InsufficientFunds;
        }

        // 보유 순서(마차 → 시설) - 전체 수리 합계와 개별 비용이 같은 계산을 거치게 한다(기획 90번 §4-15).
        private List<(string id, int cost)> DamagedTargets()
        {
            var targets = new List<(string, int)>();
            foreach (var kind in Kinds)
            {
                foreach (var id in owned.GetOwnedIds(kind))
                {
                    var cost = CostOf(id);
                    if (cost > 0) targets.Add((id, cost));
                }
            }
            return targets;
        }
    }
}
