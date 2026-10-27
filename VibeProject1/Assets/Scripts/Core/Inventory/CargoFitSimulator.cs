using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 여러 아이템이 "전부" 적재되는지 실제 배치 없이 판정한다(Docs/설계/107번 §8.2) - 퀘스트 지급 화물·보고 보상처럼 일부만 들어가면 안 되는 경로용.
    /// 실제 배치와 같은 계획기(CargoPurchasePlanner)를 하나씩 돌리며 가상 목록에 결과(기존 물품 재배치 포함)를 쌓는다 - 판정과 실제 배치 결과가 같아야 한다.
    /// 임시 보관 물품은 칸을 차지하지 않으므로 뺀다.
    /// </summary>
    public static class CargoFitSimulator
    {
        private const string VirtualIdPrefix = "__fit";

        public static bool CanFitAll(IReadOnlyList<InventorySection> sections, IReadOnlyCollection<InventoryItemInstance> placed, IReadOnlyList<IInventoryItemDefinition> toAdd, bool allowRotation)
        {
            if (toAdd == null || toAdd.Count == 0) return true;

            var items = new List<InventoryItemInstance>();
            if (placed != null)
            {
                foreach (var item in placed)
                {
                    if (!item.IsStaged) items.Add(item);
                }
            }

            for (var n = 0; n < toAdd.Count; n++)
            {
                if (!CargoPurchasePlanner.TryPlan(sections, null, items, toAdd[n], allowRotation, out var plan)) return false;
                foreach (var move in plan.Moves) ApplyMove(items, move);
                items.Add(new InventoryItemInstance(VirtualIdPrefix + n, toAdd[n], plan.Position, plan.QuarterTurns, false, plan.SectionId));
            }
            return true;
        }

        private static void ApplyMove(List<InventoryItemInstance> items, ItemPlacement move)
        {
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.InstanceId != move.InstanceId) continue;
                if (move.Position is { } position)
                {
                    items[i] = new InventoryItemInstance(item.InstanceId, item.Definition, position, move.QuarterTurns, false, move.SectionId ?? item.SectionId);
                }
                else
                {
                    items.RemoveAt(i); // 임시 보관으로 빠지는 이동 - 칸을 비운다.
                }
                return;
            }
        }
    }
}
