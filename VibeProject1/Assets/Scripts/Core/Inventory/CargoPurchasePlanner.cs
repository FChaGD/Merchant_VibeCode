using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public readonly struct CargoPurchasePlan
    {
        public string SectionId { get; }
        public GridPosition Position { get; }
        public int QuarterTurns { get; }
        /// <summary>구매품을 놓기 전에 한 번에 적용할 기존 물품 이동(IInventoryArrangement.TryApplyPlacements). 비어 있으면 이동 없음.</summary>
        public IReadOnlyList<ItemPlacement> Moves { get; }

        public CargoPurchasePlan(string sectionId, GridPosition position, int quarterTurns, IReadOnlyList<ItemPlacement> moves)
        {
            SectionId = sectionId;
            Position = position;
            QuarterTurns = quarterTurns;
            Moves = moves;
        }
    }

    /// <summary>
    /// 무역품 구매의 자리 계획(Docs/설계/83번 §5.1). 바로 놓을 자리가 없으면 "필요한 물품만" 옮겨 자리를 만든다 - 최소 이동을 보장하는
    /// 전체 탐색은 조합 문제라 비용이 커서, 후보 자리마다 그 자리를 덮는 물품만 1단계로 옮겨 보는 탐욕 방식을 쓴다(연쇄 이동 없음, §10).
    /// 물품 이동 대상은 같은 마차 → 다른 마차(보유 순서)이고, 구매품 마차는 보고 있는 마차부터 찾는다(사용자 결정 2026-10-10).
    /// 상태를 바꾸지 않는다 - 판정과 확정이 같은 계획을 쓰게 하기 위함이다.
    /// </summary>
    public static class CargoPurchasePlanner
    {
        public static bool TryPlan(IReadOnlyList<InventorySection> sectionsInOwnedOrder, string preferredSectionId, IReadOnlyCollection<InventoryItemInstance> placedItems, IInventoryItemDefinition definition, bool allowRotation, out CargoPurchasePlan plan)
        {
            plan = default;
            if (definition == null || sectionsInOwnedOrder == null || sectionsInOwnedOrder.Count == 0) return false;

            var searchOrder = InventoryAutoSorter.OrderFrom(sectionsInOwnedOrder, preferredSectionId);
            if (InventoryAutoSorter.TryFindSlot(searchOrder, placedItems, definition, allowRotation, out var sectionId, out var position, out var turns))
            {
                plan = new CargoPurchasePlan(sectionId, position, turns, Array.Empty<ItemPlacement>());
                return true;
            }

            foreach (var section in searchOrder)
            {
                if (TryPlanInSection(section, sectionsInOwnedOrder, placedItems, definition, allowRotation, out plan)) return true;
            }
            return false;
        }

        private static bool TryPlanInSection(InventorySection section, IReadOnlyList<InventorySection> sectionsInOwnedOrder, IReadOnlyCollection<InventoryItemInstance> placedItems, IInventoryItemDefinition definition, bool allowRotation, out CargoPurchasePlan plan)
        {
            plan = default;
            var found = false;
            var bestMoveCount = int.MaxValue;
            var bestMovedCells = int.MaxValue;

            foreach (var turns in CandidateTurns(definition, allowRotation))
            {
                var width = turns % 2 == 1 ? definition.FootprintHeight : definition.FootprintWidth;
                var height = turns % 2 == 1 ? definition.FootprintWidth : definition.FootprintHeight;
                for (var y = 0; y + height <= section.Shape.Height; y++)
                {
                    for (var x = 0; x + width <= section.Shape.Width; x++)
                    {
                        var target = new GridPosition(x, y);
                        if (!section.Shape.ContainsRect(target, width, height)) continue;

                        var displaced = placedItems.Where(item => item.SectionId == section.Id && Overlaps(item, x, y, width, height)).ToList();
                        if (displaced.Count == 0) continue; // 비어 있는 자리는 바로 놓기 단계에서 이미 찾았다.

                        var movedCells = displaced.Sum(item => item.Width * item.Height);
                        // 위 행·왼쪽부터 훑으므로 같은 점수면 먼저 찾은 자리를 유지한다.
                        if (displaced.Count > bestMoveCount || (displaced.Count == bestMoveCount && movedCells >= bestMovedCells)) continue;
                        if (!TryRelocate(section, sectionsInOwnedOrder, placedItems, displaced, target, width, height, allowRotation, out var moves)) continue;

                        found = true;
                        bestMoveCount = displaced.Count;
                        bestMovedCells = movedCells;
                        plan = new CargoPurchasePlan(section.Id, target, turns, moves);
                    }
                }
            }
            return found;
        }

        // 밀려나는 물품을 같은 마차 → 다른 마차(보유 순서)의 빈자리로 큰 것부터 옮긴다. 구매품 자리는 미리 막아 둔다.
        private static bool TryRelocate(InventorySection section, IReadOnlyList<InventorySection> sectionsInOwnedOrder, IReadOnlyCollection<InventoryItemInstance> placedItems, List<InventoryItemInstance> displaced, GridPosition target, int width, int height, bool allowRotation, out List<ItemPlacement> moves)
        {
            moves = new List<ItemPlacement>();
            var destinations = new List<InventorySection> { section };
            foreach (var other in sectionsInOwnedOrder)
            {
                if (other.Id != section.Id) destinations.Add(other);
            }

            var displacedIds = new HashSet<string>(displaced.Select(item => item.InstanceId));
            var occupancies = InventoryAutoSorter.CreateOccupancies(destinations, placedItems.Where(item => !displacedIds.Contains(item.InstanceId)));
            InventoryAutoSorter.MarkRect(occupancies[0], target, width, height);

            foreach (var item in InventoryAutoSorter.Order(displaced))
            {
                if (!InventoryAutoSorter.TryFindSlot(destinations, occupancies, item.Definition, item.QuarterTurns, allowRotation, out var index, out var position, out var turns))
                {
                    moves = null;
                    return false;
                }

                InventoryAutoSorter.MarkFootprint(occupancies[index], item.Definition, position, turns);
                moves.Add(new ItemPlacement(item.InstanceId, position, turns, destinations[index].Id));
            }
            return true;
        }

        private static IEnumerable<int> CandidateTurns(IInventoryItemDefinition definition, bool allowRotation)
        {
            yield return 0;
            if (allowRotation && definition.FootprintWidth != definition.FootprintHeight) yield return 1;
        }

        private static bool Overlaps(InventoryItemInstance item, int x, int y, int width, int height)
            => item.Position.X < x + width && x < item.Position.X + item.Width
            && item.Position.Y < y + height && y < item.Position.Y + item.Height;
    }
}
