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
    /// </summary>
    public static class InventoryAutoSorter
    {
        /// <summary>그리드 + 임시 보관 전부를 비운 그리드에 다시 채운다. 전부 못 넣으면 false.</summary>
        public static bool TryBuildSortedLayout(int gridWidth, int gridHeight, IEnumerable<InventoryItemInstance> placedItems, IEnumerable<InventoryItemInstance> stagedItems, out List<ItemPlacement> placements)
        {
            var occupied = new bool[Math.Max(gridWidth, 0), Math.Max(gridHeight, 0)];
            return TryFill(occupied, placedItems.Concat(stagedItems), out placements);
        }

        /// <summary>임시 보관 아이템만 기존 그리드 빈칸에 넣는다(기존 아이템은 움직이지 않음). 전부 못 넣으면 false.</summary>
        public static bool TryBuildStagedFlush(int gridWidth, int gridHeight, IEnumerable<InventoryItemInstance> placedItems, IEnumerable<InventoryItemInstance> stagedItems, out List<ItemPlacement> placements)
        {
            var occupied = new bool[Math.Max(gridWidth, 0), Math.Max(gridHeight, 0)];
            foreach (var item in placedItems)
            {
                MarkClamped(occupied, item.Position, item.Width, item.Height);
            }
            return TryFill(occupied, stagedItems, out placements);
        }

        // 점유 칸 수 내림차순 → 정의 Id → InstanceId 순서로 결과가 매번 같게 한다. 각 아이템은 위쪽 행부터,
        // 행 안에서는 왼쪽부터 첫 빈자리를 찾고, 원래 방향으로 못 넣으면 시계방향 90° 회전해서 재시도한다.
        private static bool TryFill(bool[,] occupied, IEnumerable<InventoryItemInstance> items, out List<ItemPlacement> placements)
        {
            placements = new List<ItemPlacement>();
            var ordered = items
                .OrderByDescending(item => item.Definition.FootprintWidth * item.Definition.FootprintHeight)
                .ThenBy(item => item.Definition.Id, StringComparer.Ordinal)
                .ThenBy(item => item.InstanceId, StringComparer.Ordinal);

            foreach (var item in ordered)
            {
                if (!TryFindSlot(occupied, item, out var position, out var quarterTurns))
                {
                    placements = null;
                    return false;
                }

                var width = quarterTurns % 2 == 1 ? item.Definition.FootprintHeight : item.Definition.FootprintWidth;
                var height = quarterTurns % 2 == 1 ? item.Definition.FootprintWidth : item.Definition.FootprintHeight;
                MarkClamped(occupied, position, width, height);
                placements.Add(new ItemPlacement(item.InstanceId, position, quarterTurns));
            }

            return true;
        }

        private static bool TryFindSlot(bool[,] occupied, InventoryItemInstance item, out GridPosition position, out int quarterTurns)
        {
            var baseWidth = item.Definition.FootprintWidth;
            var baseHeight = item.Definition.FootprintHeight;
            var candidates = baseWidth == baseHeight
                ? new[] { item.QuarterTurns }
                : new[] { item.QuarterTurns, InventoryRotation.RotateClockwise(item.QuarterTurns) };

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

        // 축소로 범위 밖에 걸친 아이템이 있을 수 있어 클램프한다(InventoryGrid.Mark와 같은 이유).
        private static void MarkClamped(bool[,] occupied, GridPosition position, int width, int height)
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
