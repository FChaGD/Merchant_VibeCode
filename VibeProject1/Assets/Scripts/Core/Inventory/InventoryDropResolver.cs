using System.Collections.Generic;

namespace Game.Core
{
    public enum InventoryDropKind
    {
        Move,
        Swap,
        Invalid,
    }

    public readonly struct InventoryDropResult
    {
        private static readonly IReadOnlyList<ItemPlacement> NoPlacements = new ItemPlacement[0];

        public InventoryDropKind Kind { get; }
        public IReadOnlyList<ItemPlacement> Placements { get; }

        public InventoryDropResult(InventoryDropKind kind, IReadOnlyList<ItemPlacement> placements)
        {
            Kind = kind;
            Placements = placements ?? NoPlacements;
        }

        public static InventoryDropResult Invalid => new(InventoryDropKind.Invalid, null);
    }

    /// <summary>
    /// 그리드 위 드롭의 결과(이동/교환/불가)를 판정한다(Docs/기획/39번 §3.2~3.3·§4.1, 설계 40번 §4.1).
    /// 드래그 중 미리보기 색과 실제 드롭이 같은 판정을 쓰도록 한 곳에 둔다. 상태는 바꾸지 않는다 -
    /// 결과의 Placements를 IInventoryArrangement.TryApplyPlacements에 넘기는 것은 호출자 몫이다.
    /// </summary>
    public static class InventoryDropResolver
    {
        /// <param name="dragged">드래그 시작 시점의 인스턴스(원래 위치·회전·임시 보관 여부).</param>
        /// <param name="target">드롭할 좌상단 칸.</param>
        /// <param name="quarterTurns">드롭 시점의 회전(드래그 중 Q로 바뀐 값).</param>
        public static InventoryDropResult ResolveGridDrop(IInventoryReader reader, IInventoryArrangement arrangement, InventoryItemInstance dragged, GridPosition target, int quarterTurns)
        {
            var move = new ItemPlacement(dragged.InstanceId, target, quarterTurns);
            var width = quarterTurns % 2 == 1 ? dragged.Definition.FootprintHeight : dragged.Definition.FootprintWidth;
            var height = quarterTurns % 2 == 1 ? dragged.Definition.FootprintWidth : dragged.Definition.FootprintHeight;

            if (target.X < 0 || target.Y < 0 || target.X + width > reader.GridWidth || target.Y + height > reader.GridHeight)
            {
                return InventoryDropResult.Invalid;
            }

            // 목표 범위와 겹치는 다른 아이템을 모은다(드래그 중인 아이템 자신의 원래 칸은 제외).
            var overlapped = new Dictionary<string, InventoryItemInstance>();
            for (var x = target.X; x < target.X + width; x++)
            {
                for (var y = target.Y; y < target.Y + height; y++)
                {
                    if (reader.TryGetItemAt(new GridPosition(x, y), out var other) && other.InstanceId != dragged.InstanceId)
                    {
                        overlapped[other.InstanceId] = other;
                    }
                }
            }

            if (overlapped.Count == 0)
            {
                return new InventoryDropResult(InventoryDropKind.Move, new[] { move });
            }

            // 3개 이상 아이템 재배치는 결과가 불명확해 교환 대상에서 제외한다(기획 39번 §4.1).
            if (overlapped.Count > 1)
            {
                return InventoryDropResult.Invalid;
            }

            // 밀려난 아이템은 드래그 아이템의 원래 자리로 간다 - 원래 자리가 임시 보관이면 임시 보관, 그리드면
            // 원래 좌상단 좌표. 밀려난 아이템 자신의 회전은 유지한다.
            InventoryItemInstance displaced = default;
            foreach (var item in overlapped.Values) displaced = item;

            var displacedPlacement = dragged.IsStaged
                ? ItemPlacement.ToStaging(displaced.InstanceId, displaced.QuarterTurns)
                : new ItemPlacement(displaced.InstanceId, dragged.Position, displaced.QuarterTurns);

            var swap = new[] { move, displacedPlacement };
            return arrangement.CanApplyPlacements(swap)
                ? new InventoryDropResult(InventoryDropKind.Swap, swap)
                : InventoryDropResult.Invalid;
        }
    }
}
