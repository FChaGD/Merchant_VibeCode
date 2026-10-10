using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>
    /// 골드 소지량 계산 규칙(Docs/설계/83번 §4). 상태를 바꾸지 않는 순수 계산이다 - 장부(GoldLedger)의 조회·인출 미리보기·실제 지출이
    /// 같은 함수를 써야 "판정에서 가능했던 구매가 확정에서 실패"하는 어긋남이 생기지 않는다.
    /// 변환 개수 계산은 1×1 골드 상자 전제다(빈 칸 수 = 놓을 자리 수). 다른 크기의 골드 아이템이 생기면 자리 탐색으로 바꾼다(§10).
    /// </summary>
    public static class GoldHoldingsCalculator
    {
        public static int CountFreeCells(IReadOnlyList<InventorySection> sections, IEnumerable<InventoryItemInstance> placedItems)
        {
            var usable = 0;
            var sectionIds = new HashSet<string>();
            foreach (var section in sections)
            {
                usable += section.Shape.UsableCount;
                sectionIds.Add(section.Id);
            }

            var occupied = 0;
            foreach (var item in placedItems)
            {
                if (!item.IsStaged && item.SectionId != null && sectionIds.Contains(item.SectionId)) occupied += item.Width * item.Height;
            }
            return Math.Max(0, usable - occupied);
        }

        public static int CountItems(IEnumerable<InventoryItemInstance> placedItems, IEnumerable<InventoryItemInstance> stagedItems, string itemId)
            => placedItems.Count(item => item.Definition.Id == itemId) + stagedItems.Count(item => item.Definition.Id == itemId);

        public static float OccupancyRatio(InventorySection section, IEnumerable<InventoryItemInstance> placedItems)
        {
            if (section.Shape.UsableCount == 0) return 0f;
            var occupied = placedItems.Where(item => item.SectionId == section.Id).Sum(item => item.Width * item.Height);
            return (float)occupied / section.Shape.UsableCount;
        }

        /// <summary>
        /// 인출할 골드 아이템 Id를 순서대로 count개까지 고른다(설계 83번 §4.2). 임시 보관 → 점유율 높은 마차(동률이면 뒤쪽) → 마차 안 아래 행·오른쪽.
        /// 점유율은 처음 한 번만 계산한다(사용자 결정 2026-10-10) - 1위 마차의 상자를 다 쓴 뒤 다음 마차로 넘어간다.
        /// 골드 아이템이 count보다 적으면 있는 만큼만 돌려준다.
        /// </summary>
        public static List<string> SelectWithdrawal(IReadOnlyList<InventorySection> sectionsInOwnedOrder, IEnumerable<InventoryItemInstance> placedItems, IEnumerable<InventoryItemInstance> stagedItems, string goldItemId, int count)
        {
            var selected = new List<string>();
            if (count <= 0) return selected;

            foreach (var staged in stagedItems)
            {
                if (selected.Count >= count) return selected;
                if (staged.Definition.Id == goldItemId) selected.Add(staged.InstanceId);
            }

            var placed = placedItems.ToList();
            var sectionOrder = Enumerable.Range(0, sectionsInOwnedOrder.Count)
                .Select(index => (index, ratio: OccupancyRatio(sectionsInOwnedOrder[index], placed)))
                .OrderByDescending(entry => entry.ratio)
                .ThenByDescending(entry => entry.index)
                .Select(entry => sectionsInOwnedOrder[entry.index].Id)
                .ToList();

            foreach (var sectionId in sectionOrder)
            {
                var boxes = placed
                    .Where(item => item.SectionId == sectionId && item.Definition.Id == goldItemId)
                    .OrderByDescending(item => item.Position.Y)
                    .ThenByDescending(item => item.Position.X);
                foreach (var box in boxes)
                {
                    if (selected.Count >= count) return selected;
                    selected.Add(box.InstanceId);
                }
            }
            return selected;
        }

        public static int CeilDiv(int value, int divisor) => value <= 0 || divisor <= 0 ? 0 : (value + divisor - 1) / divisor;

        public static int MaxConvertible(int personalGold, int goldValue, int freeCells)
            => goldValue <= 0 ? 0 : Math.Max(0, Math.Min(personalGold / goldValue, freeCells));

        public static int ExcessBoxes(int personalGold, int personalLimit, int goldValue, int maxConvertible)
            => Math.Min(CeilDiv(personalGold - personalLimit, goldValue), Math.Max(0, maxConvertible));
    }
}
