using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>
    /// 자동 정렬과 "팝업 닫기 시 임시 보관 아이템 자동 배치"의 배치 계산(Docs/기획/39번 §3.5~3.6, 설계 40번 §4.2).
    /// 계산만 하고 상태는 바꾸지 않는다 - 결과를 IInventoryArrangement.TryApplyPlacements로 한 번에 적용하므로
    /// "전부 못 넣으면 원래 상태 유지"가 자료구조 수준에서 보장된다.
    /// 탐욕 배치라 원래는 다 들어가 있던 구성이 정렬로는 안 들어갈 수 있다 - 그 경우 정렬 취소(기획 확정과 일치).
    ///
    /// 섹션 모양의 막힌 칸은 계산용 점유 배열을 만들 때 미리 "차 있음"으로 표시한다(설계 64번 §4.2) - 채우기 알고리즘을
    /// 바꾸지 않고 모양을 반영하기 위함이다. 여러 섹션을 보는 계산은 섹션 순서 목록(OrderFrom)을 받아 앞에서부터 채운다.
    /// 구매 재배치 계획(CargoPurchasePlanner, 설계 83번 §5.1)이 같은 점유 배열·자리 탐색 규칙을 쓰도록 보조 함수 일부를 internal로 연다.
    /// </summary>
    public static class InventoryAutoSorter
    {
        /// <summary>
        /// 섹션 하나의 아이템 + 임시 보관 전부를 그 섹션을 비운 상태에서 다시 채운다(기획 63번 §3.5 - 다른 섹션은 건드리지 않음).
        /// 전부 못 넣으면 false.
        /// </summary>
        public static bool TryBuildSortedLayout(InventorySection section, IEnumerable<InventoryItemInstance> placedItems, IEnumerable<InventoryItemInstance> stagedItems, out List<ItemPlacement> placements)
        {
            placements = new List<ItemPlacement>();
            var occupied = CreateOccupancy(section.Shape);
            var sectionItems = placedItems.Where(item => item.SectionId == section.Id);
            foreach (var item in Order(sectionItems.Concat(stagedItems)))
            {
                if (!TryFindSlot(occupied, item.Definition, item.QuarterTurns, allowRotation: true, out var position, out var quarterTurns))
                {
                    placements = null;
                    return false;
                }

                MarkFootprint(occupied, item.Definition, position, quarterTurns);
                placements.Add(new ItemPlacement(item.InstanceId, position, quarterTurns, section.Id));
            }

            return true;
        }

        /// <summary>
        /// 임시 보관 아이템만 섹션들의 빈칸에 넣는다(기존 아이템은 움직이지 않음). 큰 아이템부터, 섹션 순서 목록의 앞 섹션부터
        /// 들어가는 곳에 넣는다(기획 63번 §3.5 - 현재 마차부터). 하나라도 못 넣으면 false.
        /// </summary>
        public static bool TryBuildStagedFlush(IReadOnlyList<InventorySection> sectionsInOrder, IEnumerable<InventoryItemInstance> placedItems, IEnumerable<InventoryItemInstance> stagedItems, out List<ItemPlacement> placements)
        {
            placements = new List<ItemPlacement>();
            var occupancies = CreateOccupancies(sectionsInOrder, placedItems);
            foreach (var item in Order(stagedItems))
            {
                if (!TryFindSlot(sectionsInOrder, occupancies, item.Definition, item.QuarterTurns, allowRotation: true, out var sectionIndex, out var position, out var quarterTurns))
                {
                    placements = null;
                    return false;
                }

                MarkFootprint(occupancies[sectionIndex], item.Definition, position, quarterTurns);
                placements.Add(new ItemPlacement(item.InstanceId, position, quarterTurns, sectionsInOrder[sectionIndex].Id));
            }

            return true;
        }

        /// <summary>
        /// 새 아이템 하나가 들어갈 첫 빈자리를 섹션 순서 목록의 앞 섹션부터 찾는다(구매 자동 배치, Docs/설계/50번 §5.4, 64번 §8).
        /// 자동 정렬과 같은 "위쪽 행부터, 왼쪽부터, 안 되면 회전" 규칙을 쓴다. 임시 보관 아이템은 그리드 칸을 차지하지 않으므로 받지 않는다.
        /// </summary>
        public static bool TryFindSlot(IReadOnlyList<InventorySection> sectionsInOrder, IEnumerable<InventoryItemInstance> placedItems, IInventoryItemDefinition definition, bool allowRotation, out string sectionId, out GridPosition position, out int quarterTurns)
        {
            sectionId = null;
            position = default;
            quarterTurns = 0;
            if (definition == null) return false;

            var occupancies = CreateOccupancies(sectionsInOrder, placedItems);
            if (!TryFindSlot(sectionsInOrder, occupancies, definition, 0, allowRotation, out var sectionIndex, out position, out quarterTurns)) return false;

            sectionId = sectionsInOrder[sectionIndex].Id;
            return true;
        }

        /// <summary>startSectionId부터 시작해 끝까지 가면 처음으로 돌아가는 섹션 순서(기획 63번 §3.5 "현재 마차부터"). 없는 Id면 원래 순서.</summary>
        public static IReadOnlyList<InventorySection> OrderFrom(IReadOnlyList<InventorySection> sections, string startSectionId)
        {
            var start = 0;
            for (var i = 0; i < sections.Count; i++)
            {
                if (sections[i].Id == startSectionId) start = i;
            }

            var ordered = new List<InventorySection>(sections.Count);
            for (var i = 0; i < sections.Count; i++) ordered.Add(sections[(start + i) % sections.Count]);
            return ordered;
        }

        // 점유 칸 수 내림차순 → 정의 Id → InstanceId 순서로 결과가 매번 같게 한다.
        internal static IEnumerable<InventoryItemInstance> Order(IEnumerable<InventoryItemInstance> items)
            => items
                .OrderByDescending(item => item.Definition.FootprintWidth * item.Definition.FootprintHeight)
                .ThenBy(item => item.Definition.Id, StringComparer.Ordinal)
                .ThenBy(item => item.InstanceId, StringComparer.Ordinal);

        internal static List<bool[,]> CreateOccupancies(IReadOnlyList<InventorySection> sections, IEnumerable<InventoryItemInstance> placedItems)
        {
            var occupancies = new List<bool[,]>(sections.Count);
            var indexById = new Dictionary<string, int>();
            for (var i = 0; i < sections.Count; i++)
            {
                occupancies.Add(CreateOccupancy(sections[i].Shape));
                indexById[sections[i].Id] = i;
            }

            foreach (var item in placedItems)
            {
                if (item.SectionId != null && indexById.TryGetValue(item.SectionId, out var index))
                {
                    MarkRect(occupancies[index], item.Position, item.Width, item.Height);
                }
            }
            return occupancies;
        }

        // 막힌 칸은 처음부터 차 있는 것으로 둔다.
        private static bool[,] CreateOccupancy(InventoryShape shape)
        {
            var occupied = new bool[shape.Width, shape.Height];
            for (var x = 0; x < shape.Width; x++)
            {
                for (var y = 0; y < shape.Height; y++) occupied[x, y] = !shape.IsUsable(x, y);
            }
            return occupied;
        }

        internal static bool TryFindSlot(IReadOnlyList<InventorySection> sections, List<bool[,]> occupancies, IInventoryItemDefinition definition, int currentTurns, bool allowRotation, out int sectionIndex, out GridPosition position, out int quarterTurns)
        {
            for (sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
            {
                if (TryFindSlot(occupancies[sectionIndex], definition, currentTurns, allowRotation, out position, out quarterTurns)) return true;
            }

            sectionIndex = -1;
            position = default;
            quarterTurns = 0;
            return false;
        }

        // 위쪽 행부터, 행 안에서는 왼쪽부터 첫 빈자리를 찾고, 원래 방향으로 못 넣으면 시계방향 90° 회전해서 재시도한다.
        private static bool TryFindSlot(bool[,] occupied, IInventoryItemDefinition definition, int currentTurns, bool allowRotation, out GridPosition position, out int quarterTurns)
        {
            var baseWidth = definition.FootprintWidth;
            var baseHeight = definition.FootprintHeight;
            var candidates = baseWidth == baseHeight || !allowRotation
                ? new[] { currentTurns }
                : new[] { currentTurns, InventoryRotation.RotateClockwise(currentTurns) };

            foreach (var turns in candidates)
            {
                var width = turns % 2 == 1 ? baseHeight : baseWidth;
                var height = turns % 2 == 1 ? baseWidth : baseHeight;
                for (var y = 0; y + height <= occupied.GetLength(1); y++)
                {
                    for (var x = 0; x + width <= occupied.GetLength(0); x++)
                    {
                        if (IsFree(occupied, x, y, width, height))
                        {
                            position = new GridPosition(x, y);
                            quarterTurns = turns;
                            return true;
                        }
                    }
                }
            }

            position = default;
            quarterTurns = 0;
            return false;
        }

        private static bool IsFree(bool[,] occupied, int startX, int startY, int width, int height)
        {
            for (var x = startX; x < startX + width; x++)
            {
                for (var y = startY; y < startY + height; y++)
                {
                    if (occupied[x, y]) return false;
                }
            }
            return true;
        }

        internal static void MarkFootprint(bool[,] occupied, IInventoryItemDefinition definition, GridPosition position, int quarterTurns)
        {
            var width = quarterTurns % 2 == 1 ? definition.FootprintHeight : definition.FootprintWidth;
            var height = quarterTurns % 2 == 1 ? definition.FootprintWidth : definition.FootprintHeight;
            MarkRect(occupied, position, width, height);
        }

        // 저장소 상태가 섹션 모양과 어긋나는 일은 없지만(배치는 항상 모양 검사를 거침), 계산용 배열이라 방어적으로 자른다.
        internal static void MarkRect(bool[,] occupied, GridPosition position, int width, int height)
        {
            var maxX = Math.Min(position.X + width, occupied.GetLength(0));
            var maxY = Math.Min(position.Y + height, occupied.GetLength(1));
            for (var x = Math.Max(position.X, 0); x < maxX; x++)
            {
                for (var y = Math.Max(position.Y, 0); y < maxY; y++)
                {
                    occupied[x, y] = true;
                }
            }
        }
    }
}
