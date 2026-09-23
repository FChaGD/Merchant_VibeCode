using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 그리드 좌표/점유 칸 계산을 전담하는 자료구조(Docs/설계/32번 §2). FormationLayout(슬롯 1칸=유닛
    /// 1개, 1차원 인덱스 배열)과 달리 아이템이 여러 칸(WxH)을 동시에 차지할 수 있어 좌표 기반 점유
    /// 배열로 겹침/범위를 검사한다. 회전/자동 정렬은 다루지 않는다(기획 31번 §4).
    /// </summary>
    public class InventoryGrid
    {
        private readonly Dictionary<string, InventoryItemInstance> itemsByInstanceId = new();
        private string[,] occupancy;

        public int Width { get; private set; }
        public int Height { get; private set; }
        public IReadOnlyCollection<InventoryItemInstance> Items => itemsByInstanceId.Values;

        public InventoryGrid(int width, int height)
        {
            Resize(width, height);
        }

        public bool TryPlace(IInventoryItemDefinition definition, GridPosition position, out InventoryItemInstance placed)
        {
            placed = default;
            if (definition == null || !FitsAndFree(position, definition.FootprintWidth, definition.FootprintHeight))
            {
                return false;
            }

            var instanceId = Guid.NewGuid().ToString("N");
            placed = new InventoryItemInstance(instanceId, definition, position);
            itemsByInstanceId[instanceId] = placed;
            Mark(position, definition.FootprintWidth, definition.FootprintHeight, instanceId);
            return true;
        }

        public bool Remove(string instanceId)
        {
            if (!itemsByInstanceId.Remove(instanceId, out var item)) return false;

            Mark(item.Position, item.Definition.FootprintWidth, item.Definition.FootprintHeight, null);
            return true;
        }

        public bool TryGetAt(GridPosition position, out InventoryItemInstance item)
        {
            if (!InBounds(position) || occupancy[position.X, position.Y] is not { } instanceId)
            {
                item = default;
                return false;
            }

            return itemsByInstanceId.TryGetValue(instanceId, out item);
        }

        public bool TryFind(string instanceId, out InventoryItemInstance item) => itemsByInstanceId.TryGetValue(instanceId, out item);

        /// <summary>
        /// 마차 증감(Docs/설계/32번 §4.2)으로 총 칸 수가 바뀔 때 호출한다. 매번 처음부터 다시 계산하므로
        /// 축소 시 범위 밖으로 밀려난 아이템은 목록에는 남되 점유 칸에서는 빠지고(겹침 검사 제외),
        /// 이후 다시 그 칸을 포함하도록 커지면 자동으로 되살아난다. 아이템을 함부로 잃지 않는 쪽을
        /// 기본값으로 삼는다(31번 §4/§6 "최종 정책은 범위 밖" - 재검토 여지 명시).
        /// </summary>
        public void Resize(int newWidth, int newHeight)
        {
            Width = newWidth < 0 ? 0 : newWidth;
            Height = newHeight < 0 ? 0 : newHeight;
            occupancy = new string[Width, Height];

            foreach (var item in itemsByInstanceId.Values)
            {
                if (Fits(item.Position, item.Definition.FootprintWidth, item.Definition.FootprintHeight))
                {
                    Mark(item.Position, item.Definition.FootprintWidth, item.Definition.FootprintHeight, item.InstanceId);
                }
            }
        }

        private bool InBounds(GridPosition position) => position.X >= 0 && position.X < Width && position.Y >= 0 && position.Y < Height;

        private bool Fits(GridPosition position, int width, int height)
        {
            if (width <= 0 || height <= 0) return false;
            return position.X >= 0 && position.Y >= 0 && position.X + width <= Width && position.Y + height <= Height;
        }

        private bool FitsAndFree(GridPosition position, int width, int height)
        {
            if (!Fits(position, width, height)) return false;

            for (var x = position.X; x < position.X + width; x++)
            {
                for (var y = position.Y; y < position.Y + height; y++)
                {
                    if (occupancy[x, y] != null) return false;
                }
            }

            return true;
        }

        // 제거(null 기록) 시에는 아이템이 축소로 인해 이미 부분/전체적으로 범위 밖일 수 있어 클램프한다 -
        // 배치(점유 기록) 시에는 FitsAndFree가 이미 전체 포함을 확인했으므로 클램프가 항상 무해하다.
        private void Mark(GridPosition position, int width, int height, string instanceId)
        {
            var maxX = Math.Min(position.X + width, Width);
            var maxY = Math.Min(position.Y + height, Height);
            for (var x = Math.Max(position.X, 0); x < maxX; x++)
            {
                for (var y = Math.Max(position.Y, 0); y < maxY; y++)
                {
                    occupancy[x, y] = instanceId;
                }
            }
        }
    }
}
