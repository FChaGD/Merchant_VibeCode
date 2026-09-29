using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 디버그 핀 한 개(판 칸 + 정사각형 반경 - 마차와 같은 규칙, Docs/기획/59번 §4.5). 영역 계산의 선택적 입력이다.
    /// </summary>
    public readonly struct FormationAreaPin
    {
        public int SlotIndex { get; }
        public int Radius { get; }

        public FormationAreaPin(int slotIndex, int radius)
        {
            SlotIndex = slotIndex;
            Radius = radius;
        }
    }

    /// <summary>
    /// 외곽 판 위의 대열 영역(Docs/기획/59번, 설계 60번 §3). 배치가 바뀔 때마다 새로 계산하는 불변 스냅샷이다 - 판 2,500칸·유닛 수십 개
    /// 수준이라 편집 동작마다 다시 계산해도 충분하고, 캐시를 두면 배치와 어긋날 위험만 생긴다.
    /// - 대열 칸: 마차가 없으면 기준 칸(판 중앙) + 핀 영역, 있으면 마차·시설·핀을 중심으로 한 정사각형(한 변 2 × 반경 + 1)의 합집합(판 밖은
    ///   잘림). 처음 기획은 십자 반경이었으나 제작 후 정사각형으로 바뀌었다(2026-09-28 사용자 정정, 기획 59번 §3.1).
    /// - 시설 자리(자리 칸): 마차·핀 영역 안만. 시설이 넓힌 칸까지 허용하면 시설을 자기 영역 끝으로 반복 이동해 대열에서 끝없이 떨어져 나갈 수
    ///   있다(2026-09-29 실전 확인, 사용자 결정).
    /// - 연결 판정(연결 칸): 자리 칸 + 자리 칸 위 시설의 영역(2026-09-29 개정, 기획 59번 §3.3 - 시설도 마차를 잇는다). 4방향으로 맞닿은 칸을
    ///   한 덩어리로 보고, 모든 마차가 한 덩어리에 있으면 연결.
    /// - 자리 칸 밖 시설은 대열·연결 어디에도 기여하지 않는다(설계 60번 §14.2) - 곧 해제될 시설이 계산상 연결을 이어 주면 판정 순서에 따라
    ///   결과가 달라진다. 그런 시설은 FormationAreaRules.Validate가 해제한다.
    /// </summary>
    public sealed class FormationArea
    {
        private readonly HashSet<int> cells = new();
        private readonly HashSet<int> pinCells = new();
        // 마차·핀 영역 - 시설을 놓을 수 있는 칸.
        private readonly HashSet<int> hostCells = new();
        // 자리 칸 + 자리 칸 위 시설의 영역 - 마차 연결 판정.
        private readonly HashSet<int> connectCells = new();
        // 마차만 / 시설만의 정사각형 합집합 - 판정에는 쓰지 않고 디버그 외곽선 표시용으로만 노출한다.
        private readonly HashSet<int> wagonCells = new();
        private readonly HashSet<int> facilityCells = new();
        private readonly List<int> wagonSlots = new();

        public int ColumnCount { get; }
        public int RowCount { get; }
        public int AnchorSlotIndex { get; }
        public IReadOnlyCollection<int> Cells => cells;
        public int WagonCount => wagonSlots.Count;
        public bool WagonsConnected { get; }
        public RectInt Bounds { get; }
        /// <summary>마차 칸 열·행의 평균(마차가 없으면 기준 칸) - 전투 대형 중심(설계 60번 §11-4).</summary>
        public Vector2 WagonCenter { get; }

        private FormationArea(FormationLayout layout, Func<string, IFormationUnit> lookup, IReadOnlyList<FormationAreaPin> pins)
        {
            ColumnCount = layout.ColumnCount;
            RowCount = layout.RowCount;
            AnchorSlotIndex = layout.AnchorSlotIndex;

            var facilityAnchors = new List<(int slot, int radius)>();
            var wagonAnchors = new List<(int slot, int radius)>();
            for (var slot = 0; slot < layout.SlotCount; slot++)
            {
                var id = layout.GetUnitId(slot);
                if (string.IsNullOrEmpty(id)) continue;

                var unit = lookup?.Invoke(id);
                if (unit == null || unit.Kind == FormationUnitKind.Character) continue;

                var radius = unit is IAreaAnchorUnit anchor ? anchor.AreaRadius : 0;
                if (unit.Kind == FormationUnitKind.Wagon)
                {
                    wagonSlots.Add(slot);
                    wagonAnchors.Add((slot, radius));
                }
                else
                {
                    facilityAnchors.Add((slot, radius));
                }
            }

            if (pins != null)
            {
                foreach (var pin in pins)
                {
                    if (!IsOnBoard(pin.SlotIndex)) continue;
                    AddSquare(pin.SlotIndex, pin.Radius, pinCells);
                    AddSquare(pin.SlotIndex, pin.Radius, hostCells);
                }
            }

            if (wagonSlots.Count == 0)
            {
                cells.Add(AnchorSlotIndex);
            }
            else
            {
                foreach (var (slot, radius) in wagonAnchors)
                {
                    AddSquare(slot, radius, hostCells);
                    AddSquare(slot, radius, wagonCells);
                }
                // 자리 칸이 모두 정해진 뒤에 판별한다 - 시설끼리 서로를 자리로 삼지 못하게.
                foreach (var (slot, radius) in facilityAnchors)
                {
                    if (hostCells.Contains(slot)) AddSquare(slot, radius, facilityCells);
                }
                cells.UnionWith(wagonCells);
                cells.UnionWith(facilityCells);
            }
            cells.UnionWith(pinCells);
            connectCells.UnionWith(hostCells);
            connectCells.UnionWith(facilityCells);

            WagonsConnected = AreAllInOneComponent(wagonSlots, connectCells);
            Bounds = ComputeBounds();
            WagonCenter = ComputeWagonCenter();
        }

        public static FormationArea Compute(FormationLayout layout, Func<string, IFormationUnit> lookup, IReadOnlyList<FormationAreaPin> pins = null)
            => new(layout, lookup, pins);

        public bool Contains(int slotIndex) => cells.Contains(slotIndex);

        public bool IsPinCell(int slotIndex) => pinCells.Contains(slotIndex);

        public bool IsWagonCell(int slotIndex) => wagonCells.Contains(slotIndex);

        public bool IsFacilityCell(int slotIndex) => facilityCells.Contains(slotIndex);

        /// <summary>이 유닛이 이 칸에 있을 수 있는지 - 시설은 자리 칸(마차·핀 영역) 안만, 나머지는 대열 칸 전체.</summary>
        public bool CanHost(IFormationUnit unit, int slotIndex)
            => unit != null && unit.Kind == FormationUnitKind.Facility ? hostCells.Contains(slotIndex) : cells.Contains(slotIndex);

        /// <summary>
        /// 전투로 옮길 대열 범위(설계 60번 §7.2, §11-4): 중심 = 마차 중심값, 반폭 = 중심에서 대열 경계 상자 가장자리까지의 최대 거리.
        /// </summary>
        public FormationExtent ToExtent()
        {
            var halfColumns = Mathf.Max(WagonCenter.x - Bounds.xMin, Bounds.xMax - 1 - WagonCenter.x);
            var halfRows = Mathf.Max(WagonCenter.y - Bounds.yMin, Bounds.yMax - 1 - WagonCenter.y);
            return new FormationExtent(WagonCenter.x, WagonCenter.y, Mathf.Max(0f, halfColumns), Mathf.Max(0f, halfRows));
        }

        private bool IsOnBoard(int slotIndex) => slotIndex >= 0 && slotIndex < ColumnCount * RowCount;

        // 중심 칸에서 가로·세로 모두 반경 이내인 정사각형(대각선 칸 포함).
        private void AddSquare(int slotIndex, int radius, HashSet<int> target)
        {
            var column = slotIndex % ColumnCount;
            var row = slotIndex / ColumnCount;
            for (var dRow = -radius; dRow <= radius; dRow++)
            {
                for (var dColumn = -radius; dColumn <= radius; dColumn++)
                {
                    TryAdd(column + dColumn, row + dRow, target);
                }
            }
        }

        private void TryAdd(int column, int row, HashSet<int> target)
        {
            if (column < 0 || column >= ColumnCount || row < 0 || row >= RowCount) return;
            target.Add(row * ColumnCount + column);
        }

        private bool AreAllInOneComponent(List<int> anchors, HashSet<int> connectCells)
        {
            if (anchors.Count <= 1) return true;

            var visited = new HashSet<int> { anchors[0] };
            var queue = new Queue<int>();
            queue.Enqueue(anchors[0]);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var column = current % ColumnCount;
                var row = current / ColumnCount;
                Visit(column - 1, row);
                Visit(column + 1, row);
                Visit(column, row - 1);
                Visit(column, row + 1);
            }

            foreach (var anchor in anchors)
            {
                if (!visited.Contains(anchor)) return false;
            }
            return true;

            void Visit(int column, int row)
            {
                if (column < 0 || column >= ColumnCount || row < 0 || row >= RowCount) return;
                var index = row * ColumnCount + column;
                if (connectCells.Contains(index) && visited.Add(index)) queue.Enqueue(index);
            }
        }

        private RectInt ComputeBounds()
        {
            int minColumn = int.MaxValue, minRow = int.MaxValue, maxColumn = int.MinValue, maxRow = int.MinValue;
            foreach (var cell in cells)
            {
                var column = cell % ColumnCount;
                var row = cell / ColumnCount;
                minColumn = Mathf.Min(minColumn, column);
                maxColumn = Mathf.Max(maxColumn, column);
                minRow = Mathf.Min(minRow, row);
                maxRow = Mathf.Max(maxRow, row);
            }
            return new RectInt(minColumn, minRow, maxColumn - minColumn + 1, maxRow - minRow + 1);
        }

        private Vector2 ComputeWagonCenter()
        {
            if (wagonSlots.Count == 0) return new Vector2(AnchorSlotIndex % ColumnCount, AnchorSlotIndex / ColumnCount);

            var sum = Vector2.zero;
            foreach (var slot in wagonSlots) sum += new Vector2(slot % ColumnCount, slot / ColumnCount);
            return sum / wagonSlots.Count;
        }
    }
}
