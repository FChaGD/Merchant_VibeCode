using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 그리드 좌표/점유 칸 계산을 전담하는 자료구조(Docs/설계/32번 §2). FormationLayout(슬롯 1칸=유닛
    /// 1개, 1차원 인덱스 배열)과 달리 아이템이 여러 칸(WxH)을 동시에 차지할 수 있어 좌표 기반 점유
    /// 배열로 겹침/범위를 검사한다.
    ///
    /// 임시 보관은 별도 컬렉션이 아니라 "같은 인벤토리 안에서 칸을 점유하지 않는 상태"로 둔다(설계 40번 §3.1) -
    /// Resize가 이미 "목록엔 있지만 점유 칸에선 빠진 아이템"을 다루는 것과 같은 개념이고, 그리드↔임시 보관
    /// 이동이 TryApplyPlacements 한 번의 원자적 조작이 된다. 재배치는 인벤토리 유입/유출이 아니므로
    /// 골드 상자 지갑 연동 같은 부수 효과는 저장소의 TryPlace/Remove 경로에만 남는다.
    /// </summary>
    public class InventoryGrid
    {
        private readonly Dictionary<string, InventoryItemInstance> placedById = new();
        private readonly List<InventoryItemInstance> stagedItems = new();
        private string[,] occupancy;

        public int Width { get; private set; }
        public int Height { get; private set; }
        public IReadOnlyCollection<InventoryItemInstance> Items => placedById.Values;
        public IReadOnlyList<InventoryItemInstance> StagedItems => stagedItems;

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

            placed = new InventoryItemInstance(Guid.NewGuid().ToString("N"), definition, position);
            placedById[placed.InstanceId] = placed;
            Mark(placed, placed.InstanceId);
            return true;
        }

        public bool Remove(string instanceId)
        {
            if (placedById.Remove(instanceId, out var item))
            {
                Unmark(item);
                return true;
            }

            return stagedItems.RemoveAll(staged => staged.InstanceId == instanceId) > 0;
        }

        public bool TryGetAt(GridPosition position, out InventoryItemInstance item)
        {
            if (!InBounds(position) || occupancy[position.X, position.Y] is not { } instanceId)
            {
                item = default;
                return false;
            }

            return placedById.TryGetValue(instanceId, out item);
        }

        public bool TryFind(string instanceId, out InventoryItemInstance item)
        {
            if (placedById.TryGetValue(instanceId, out item)) return true;

            var index = IndexOfStaged(instanceId);
            item = index >= 0 ? stagedItems[index] : default;
            return index >= 0;
        }

        /// <summary>
        /// 목록의 아이템들을 한꺼번에 새 위치(또는 임시 보관)로 옮긴다. 하나라도 범위를 벗어나거나 겹치면
        /// 아무것도 바꾸지 않고 false - 교환·자동 정렬이 중간 실패로 반쯤 적용되는 상태가 생기지 않는다.
        /// </summary>
        public bool TryApplyPlacements(IReadOnlyList<ItemPlacement> placements) => ApplyPlacements(placements, commit: true);

        /// <summary>TryApplyPlacements와 같은 검사만 하고 상태는 바꾸지 않는다(드래그 미리보기용).</summary>
        public bool CanApplyPlacements(IReadOnlyList<ItemPlacement> placements) => ApplyPlacements(placements, commit: false);

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

            foreach (var item in placedById.Values)
            {
                if (Fits(item.Position, item.Width, item.Height))
                {
                    Mark(item, item.InstanceId);
                }
            }
        }

        private bool ApplyPlacements(IReadOnlyList<ItemPlacement> placements, bool commit)
        {
            if (placements == null || placements.Count == 0) return false;

            // 1) 대상 아이템을 전부 찾고 중복 지정을 거부한다.
            var originals = new List<InventoryItemInstance>(placements.Count);
            var seen = new HashSet<string>();
            foreach (var placement in placements)
            {
                if (!seen.Add(placement.InstanceId) || !TryFind(placement.InstanceId, out var original)) return false;
                originals.Add(original);
            }

            // 2) 대상 아이템의 현재 점유를 해제한 상태에서 새 위치를 하나씩 검사·기록한다(서로 간 겹침도 검출).
            foreach (var original in originals)
            {
                if (!original.IsStaged) Unmark(original);
            }

            var updated = new List<InventoryItemInstance>(placements.Count);
            var valid = true;
            for (var i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                var next = new InventoryItemInstance(
                    placement.InstanceId,
                    originals[i].Definition,
                    placement.Position ?? default,
                    placement.QuarterTurns,
                    isStaged: placement.Position == null);

                if (!next.IsStaged)
                {
                    if (!FitsAndFree(next.Position, next.Width, next.Height))
                    {
                        valid = false;
                        break;
                    }
                    Mark(next, next.InstanceId);
                }
                updated.Add(next);
            }

            // 3) 실패했거나 검사만 하는 경우 기록한 새 점유를 지우고 원래 점유를 복구한다.
            if (!valid || !commit)
            {
                foreach (var next in updated)
                {
                    if (!next.IsStaged) Unmark(next);
                }
                foreach (var original in originals)
                {
                    if (!original.IsStaged && Fits(original.Position, original.Width, original.Height)) Mark(original, original.InstanceId);
                }
                return valid;
            }

            // 4) 확정: 임시 보관에 남는 아이템은 순서를 유지하고, 새로 들어오는 아이템은 끝에 붙인다.
            for (var i = 0; i < updated.Count; i++)
            {
                var original = originals[i];
                var next = updated[i];
                if (original.IsStaged && next.IsStaged)
                {
                    stagedItems[IndexOfStaged(next.InstanceId)] = next;
                    continue;
                }

                if (original.IsStaged) stagedItems.RemoveAt(IndexOfStaged(original.InstanceId));
                else placedById.Remove(original.InstanceId);

                if (next.IsStaged) stagedItems.Add(next);
                else placedById[next.InstanceId] = next;
            }

            return true;
        }

        private int IndexOfStaged(string instanceId) => stagedItems.FindIndex(staged => staged.InstanceId == instanceId);

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

        // 축소(Resize)로 아이템이 부분/전체적으로 범위 밖일 수 있어 클램프한다 - 배치 시에는 FitsAndFree가
        // 이미 전체 포함을 확인했으므로 클램프가 항상 무해하다.
        private void Mark(InventoryItemInstance item, string instanceId)
        {
            var maxX = Math.Min(item.Position.X + item.Width, Width);
            var maxY = Math.Min(item.Position.Y + item.Height, Height);
            for (var x = Math.Max(item.Position.X, 0); x < maxX; x++)
            {
                for (var y = Math.Max(item.Position.Y, 0); y < maxY; y++)
                {
                    occupancy[x, y] = instanceId;
                }
            }
        }

        // 자기 Id가 기록된 칸만 지운다 - 축소로 겹침 검사에서 빠졌던 아이템을 치울 때 그 칸을 차지한
        // 다른 아이템의 점유까지 지우지 않기 위함이다.
        private void Unmark(InventoryItemInstance item)
        {
            var maxX = Math.Min(item.Position.X + item.Width, Width);
            var maxY = Math.Min(item.Position.Y + item.Height, Height);
            for (var x = Math.Max(item.Position.X, 0); x < maxX; x++)
            {
                for (var y = Math.Max(item.Position.Y, 0); y < maxY; y++)
                {
                    if (occupancy[x, y] == item.InstanceId) occupancy[x, y] = null;
                }
            }
        }
    }
}
